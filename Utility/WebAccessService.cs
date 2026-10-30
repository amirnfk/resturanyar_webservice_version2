using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Models.AuthorizationModels;
using Resturanyar.Data;
using System.Security.Cryptography;
using System.Text;

namespace resturanyar.Utility
{
    public class WebAccessService
    {
        public const int TicketTtlSeconds = 60;

        public static readonly IReadOnlyDictionary<string, string> AllowedTargets =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["customers"] = "/Home/CustomersList",
                ["dashboard"] = "/Home/Dashboard",
                ["menu"] = "/Home/MenuSettings",
                ["restaurant"] = "/Home/RestaurantSetting",
                ["invoice"] = "/Receipt/ChargeSettings",
                ["inventory_movements"] = "/Home/InventoryMovements",
                ["subscription"] = "/Home/RestaurantSubscription"
            };

        private readonly AppDbContext _context;

        public WebAccessService(AppDbContext context)
        {
            _context = context;
        }

        public bool TryResolveTarget(string? targetKey, out string targetUrl)
        {
            targetUrl = string.Empty;
            if (string.IsNullOrWhiteSpace(targetKey))
                return false;

            if (!AllowedTargets.TryGetValue(targetKey.Trim(), out var url))
                return false;

            targetUrl = url;
            return true;
        }

        public string NormalizeTargetKey(string targetKey)
        {
            foreach (var key in AllowedTargets.Keys)
            {
                if (string.Equals(key, targetKey?.Trim(), StringComparison.OrdinalIgnoreCase))
                    return key;
            }

            return targetKey?.Trim() ?? string.Empty;
        }

        public async Task<bool> IsOwnerOfRestaurantAsync(int ownerId, int restaurantId, CancellationToken ct = default)
        {
            return await _context.Restaurants
                .AsNoTracking()
                .AnyAsync(r => r.restaurant_id == restaurantId && r.owner_id == ownerId, ct);
        }

        public async Task<Subscription?> GetActiveWebSubscriptionAsync(int restaurantId, CancellationToken ct = default)
        {
            return await _context.Subscriptions
                .Include(s => s.SubscriptionPlan)
                .Where(s =>
                    s.RestaurantId == restaurantId &&
                    s.Status == "Active" &&
                    s.EndDate >= DateTime.Now)
                .OrderByDescending(s => s.EndDate)
                .ThenByDescending(s => s.Id)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<bool> CanUseWebAsync(int restaurantId, CancellationToken ct = default)
        {
            var subscription = await GetActiveWebSubscriptionAsync(restaurantId, ct);
            return subscription?.SubscriptionPlan != null && subscription.SubscriptionPlan.CanUseWeb;
        }

        public async Task<(string RawToken, WebLoginTicket Ticket)> CreateTicketAsync(
            int ownerId,
            int restaurantId,
            string targetKey,
            string? createdIp,
            CancellationToken ct = default)
        {
            var normalizedTarget = NormalizeTargetKey(targetKey);
            if (!TryResolveTarget(normalizedTarget, out _))
                throw new ArgumentException("Invalid target.", nameof(targetKey));

            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            var ticket = new WebLoginTicket
            {
                TokenHash = HashToken(rawToken),
                OwnerId = ownerId,
                RestaurantId = restaurantId,
                Target = normalizedTarget,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(TicketTtlSeconds),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedIp = Truncate(createdIp, 64)
            };

            _context.WebLoginTickets.Add(ticket);
            await _context.SaveChangesAsync(ct);
            return (rawToken, ticket);
        }

        public async Task<WebLoginTicket?> ConsumeTicketAsync(string? rawToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
                return null;

            var hash = HashToken(rawToken.Trim());
            var now = DateTime.UtcNow;

            var affected = await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE dbo.WebLoginTickets
                  SET UsedAtUtc = {0}
                  WHERE TokenHash = {1}
                    AND UsedAtUtc IS NULL
                    AND ExpiresAtUtc > {2}",
                new object[] { now, hash, now },
                ct);

            if (affected != 1)
                return null;

            return await _context.WebLoginTickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        }

        public static string HashToken(string rawToken)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToBase64String(bytes);
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
