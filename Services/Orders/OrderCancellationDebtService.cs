using Microsoft.EntityFrameworkCore;
using resturanyar.Models.CustomerAccounts;
using Resturanyar.Data;

namespace resturanyar.Services.Orders
{
    public class OrderCancellationDebtService : IOrderCancellationDebtService
    {
        public const string ReversalNotePrefix = "برگشت بدهی به‌خاطر لغو سفارش";

        private readonly AppDbContext _db;

        public OrderCancellationDebtService(AppDbContext db)
        {
            _db = db;
        }

        public async Task HandleStatusChangeAsync(
            int orderId,
            int restaurantId,
            int previousStatusId,
            int newStatusId,
            int? ownerId = null,
            int? staffUserId = null,
            CancellationToken ct = default)
        {
            if (!IsCancelStatus(newStatusId) || IsCancelStatus(previousStatusId))
                return;

            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.RestaurantId == restaurantId, ct);
            if (order == null)
                return;

            var alreadyReversed = await _db.CustomerAccountTransactions.AsNoTracking()
                .AnyAsync(
                    t => t.OrderId == orderId
                         && t.RestaurantId == restaurantId
                         && t.Type == CustomerAccountTransactionTypes.Adjustment
                         && t.Note != null
                         && t.Note.StartsWith(ReversalNotePrefix),
                    ct);
            if (alreadyReversed)
            {
                await ClearOrderSettlementDebtAsync(order, ct);
                return;
            }

            var debtTx = await _db.CustomerAccountTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.OrderId == orderId
                         && t.RestaurantId == restaurantId
                         && t.Type == CustomerAccountTransactionTypes.Debt,
                    ct);

            if (debtTx == null)
            {
                // No ledger debt for this order (cancelled before settlement, or cash-only).
                await ClearOrderSettlementDebtAsync(order, ct);
                return;
            }

            var remaining = await OrderSettlementLiveResolver.ResolveRemainingDebtAsync(_db, order, ct);
            remaining = decimal.Round(Math.Max(0m, remaining), 2, MidpointRounding.AwayFromZero);

            if (remaining <= 0m)
            {
                await ClearOrderSettlementDebtAsync(order, ct);
                return;
            }

            var customerId = order.CustomerId ?? debtTx.CustomerId;
            if (customerId <= 0)
            {
                await ClearOrderSettlementDebtAsync(order, ct);
                return;
            }

            var account = await _db.CustomerAccounts
                .FirstOrDefaultAsync(a => a.RestaurantId == restaurantId && a.CustomerId == customerId, ct);
            if (account == null)
            {
                await ClearOrderSettlementDebtAsync(order, ct);
                return;
            }

            var newBalance = account.CurrentBalance - remaining;
            var entry = new CustomerAccountTransaction
            {
                AccountId = account.AccountId,
                RestaurantId = restaurantId,
                CustomerId = customerId,
                Type = CustomerAccountTransactionTypes.Adjustment,
                Amount = remaining,
                SignedAmount = -remaining,
                BalanceAfter = newBalance,
                Note = $"{ReversalNotePrefix} #{orderId}",
                OrderId = orderId,
                PaidAmount = null,
                CreatedAt = DateTime.UtcNow,
                CreatedByOwnerId = ownerId,
                CreatedByStaffUserId = staffUserId
            };

            _db.CustomerAccountTransactions.Add(entry);
            account.CurrentBalance = newBalance;
            account.UpdatedAt = DateTime.UtcNow;

            order.SettlementDebtAmount = 0m;
            order.SettlementType = OrderSettlementTypes.Resolve(
                order.SettlementPaidAmount ?? 0m,
                0m);
            order.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync(ct);
        }

        private async Task ClearOrderSettlementDebtAsync(
            Models.Order order,
            CancellationToken ct)
        {
            if ((order.SettlementDebtAmount ?? 0m) <= 0m
                && (string.IsNullOrWhiteSpace(order.SettlementType)
                    || order.SettlementType.Equals(OrderSettlementTypes.Cash, StringComparison.OrdinalIgnoreCase)))
                return;

            order.SettlementDebtAmount = 0m;
            if (!string.IsNullOrWhiteSpace(order.SettlementType)
                || order.SettlementPaidAmount.HasValue)
            {
                order.SettlementType = OrderSettlementTypes.Resolve(
                    order.SettlementPaidAmount ?? 0m,
                    0m);
            }

            order.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync(ct);
        }

        private static bool IsCancelStatus(int statusId) =>
            statusId is OrderSettlementService.CanceledByCustomerStatusId
                or OrderSettlementService.CanceledByRestaurantStatusId;
    }
}
