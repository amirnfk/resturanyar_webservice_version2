using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Services.Orders;
using Resturanyar.Data;

namespace resturanyar.Utility
{
    /// <summary>
    /// Batch ledger balance lookup (Sum of SignedAmount) — source of truth for debtor status.
    /// </summary>
    public static class CustomerBalanceLookup
    {
        public static async Task<Dictionary<int, decimal>> LoadAsync(
            AppDbContext db,
            int restaurantId,
            IEnumerable<int> customerIds,
            CancellationToken ct = default)
        {
            var ids = customerIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (ids.Count == 0)
                return new Dictionary<int, decimal>();

            return await db.CustomerAccountTransactions.AsNoTracking()
                .Where(t => t.RestaurantId == restaurantId && ids.Contains(t.CustomerId))
                .GroupBy(t => t.CustomerId)
                .Select(g => new { CustomerId = g.Key, Balance = g.Sum(t => t.SignedAmount) })
                .ToDictionaryAsync(x => x.CustomerId, x => x.Balance, ct);
        }

        public static decimal GetBalance(IReadOnlyDictionary<int, decimal> balances, int customerId)
            => balances.TryGetValue(customerId, out var bal) ? bal : 0m;

        /// <summary>
        /// Sets CustomerAccountBalance and refreshes settlement paid/debt when the account
        /// was later paid off (so lists/badges/reports stop showing «بدهی»).
        /// </summary>
        public static async Task ApplyToOrdersAsync(
            AppDbContext db,
            int restaurantId,
            IList<OrderDto> orders,
            CancellationToken ct = default)
        {
            if (orders == null || orders.Count == 0)
                return;

            var ids = orders
                .Where(o => o.CustomerId is int cid && cid > 0)
                .Select(o => o.CustomerId!.Value);

            var balances = await LoadAsync(db, restaurantId, ids, ct);
            foreach (var o in orders)
            {
                if (o.CustomerId is int cid && cid > 0)
                    o.CustomerAccountBalance = GetBalance(balances, cid);
            }

            await OrderSettlementLiveResolver.OverlayOnOrderDtosAsync(db, restaurantId, orders, ct);
        }
    }
}
