using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Models.CustomerAccounts;
using Resturanyar.Data;

namespace resturanyar.Services.Orders
{
    /// <summary>
    /// Applies customer-account credits onto order Settlement* debt fields (FIFO).
    /// Keeps receipts / badges / reports in sync after the customer pays off نسیه.
    /// </summary>
    public static class OrderDebtCreditApplicator
    {
        public static async Task ApplyAsync(
            AppDbContext db,
            int restaurantId,
            int customerId,
            decimal creditAmount,
            int? preferOrderId = null,
            int? excludeOrderId = null,
            CancellationToken ct = default)
        {
            if (db == null || restaurantId <= 0 || customerId <= 0 || creditAmount <= 0m)
                return;

            var query = db.Orders
                .Where(o => o.RestaurantId == restaurantId
                            && o.CustomerId == customerId
                            && o.SettlementDebtAmount != null
                            && o.SettlementDebtAmount > 0m);

            if (excludeOrderId is int ex && ex > 0)
                query = query.Where(o => o.OrderId != ex);

            var orders = await query
                .OrderBy(o => o.CreatedAt)
                .ThenBy(o => o.OrderId)
                .ToListAsync(ct);

            if (orders.Count == 0)
                return;

            if (preferOrderId is int oid && oid > 0)
            {
                var preferred = orders.FirstOrDefault(o => o.OrderId == oid);
                if (preferred != null)
                {
                    orders.Remove(preferred);
                    orders.Insert(0, preferred);
                }
            }

            var remaining = decimal.Round(creditAmount, 2, MidpointRounding.AwayFromZero);
            foreach (var order in orders)
            {
                if (remaining <= 0m)
                    break;

                var debt = order.SettlementDebtAmount ?? 0m;
                var apply = Math.Min(debt, remaining);
                if (apply <= 0m)
                    continue;

                var newDebt = decimal.Round(debt - apply, 2, MidpointRounding.AwayFromZero);
                var newPaid = decimal.Round((order.SettlementPaidAmount ?? 0m) + apply, 2, MidpointRounding.AwayFromZero);

                order.SettlementDebtAmount = newDebt;
                order.SettlementPaidAmount = newPaid;
                order.SettlementType = OrderSettlementTypes.Resolve(newPaid, newDebt);
                order.UpdatedAt = DateTime.Now;
                remaining -= apply;
            }
        }
    }
}
