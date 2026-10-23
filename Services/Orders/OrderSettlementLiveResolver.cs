using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Models.CustomerAccounts;
using resturanyar.Utility;
using Resturanyar.Data;

namespace resturanyar.Services.Orders
{
    /// <summary>
    /// Recomputes remaining order نسیه after later customer-account payments (FIFO),
    /// without requiring a DB write. Used by receipts, exports, and order lists.
    /// </summary>
    public static class OrderSettlementLiveResolver
    {
        public readonly record struct LiveSettlement(decimal Paid, decimal Debt, string Type);

        /// <summary>
        /// Overlays live remaining debt onto in-memory <see cref="Order"/> rows (no SaveChanges).
        /// </summary>
        public static async Task OverlayOnOrdersAsync(
            AppDbContext db,
            int restaurantId,
            IList<Order> orders,
            CancellationToken ct = default)
        {
            if (db == null || orders == null || orders.Count == 0 || restaurantId <= 0)
                return;

            var remainingByOrderId = await ResolveRemainingDebtByOrderIdAsync(db, restaurantId, orders, ct);
            if (remainingByOrderId.Count == 0)
                return;

            foreach (var order in orders)
            {
                if (!remainingByOrderId.TryGetValue(order.OrderId, out var remainingDebt))
                    continue;

                var originalDebt = order.SettlementDebtAmount ?? 0m;
                if (originalDebt <= 0m)
                    continue;

                remainingDebt = decimal.Round(Math.Max(0m, remainingDebt), 2, MidpointRounding.AwayFromZero);
                var cleared = decimal.Round(originalDebt - remainingDebt, 2, MidpointRounding.AwayFromZero);
                var newPaid = decimal.Round((order.SettlementPaidAmount ?? 0m) + cleared, 2, MidpointRounding.AwayFromZero);

                order.SettlementDebtAmount = remainingDebt;
                order.SettlementPaidAmount = newPaid;
                order.SettlementType = OrderSettlementTypes.Resolve(newPaid, remainingDebt);
            }
        }

        /// <summary>
        /// Overlays live remaining debt onto order DTOs used by manager/cashier lists and APIs.
        /// </summary>
        public static async Task OverlayOnOrderDtosAsync(
            AppDbContext db,
            int restaurantId,
            IList<OrderDto> orders,
            CancellationToken ct = default)
        {
            if (db == null || orders == null || orders.Count == 0 || restaurantId <= 0)
                return;

            var stubs = orders
                .Where(o => (o.SettlementDebtAmount ?? 0m) > 0m && o.CustomerId is int cid && cid > 0)
                .Select(o => new Order
                {
                    OrderId = o.OrderId,
                    RestaurantId = restaurantId,
                    CustomerId = o.CustomerId,
                    SettlementDebtAmount = o.SettlementDebtAmount,
                    SettlementPaidAmount = o.SettlementPaidAmount,
                    SettlementType = o.SettlementType,
                    CreatedAt = o.CreatedAt
                })
                .ToList();

            if (stubs.Count == 0)
                return;

            var remainingByOrderId = await ResolveRemainingDebtByOrderIdAsync(db, restaurantId, stubs, ct);
            if (remainingByOrderId.Count == 0)
                return;

            foreach (var order in orders)
            {
                if (!remainingByOrderId.TryGetValue(order.OrderId, out var remainingDebt))
                    continue;

                var originalDebt = order.SettlementDebtAmount ?? 0m;
                if (originalDebt <= 0m)
                    continue;

                remainingDebt = decimal.Round(Math.Max(0m, remainingDebt), 2, MidpointRounding.AwayFromZero);
                var cleared = decimal.Round(originalDebt - remainingDebt, 2, MidpointRounding.AwayFromZero);
                var newPaid = decimal.Round((order.SettlementPaidAmount ?? 0m) + cleared, 2, MidpointRounding.AwayFromZero);

                order.SettlementDebtAmount = remainingDebt;
                order.SettlementPaidAmount = newPaid;
                order.SettlementType = OrderSettlementTypes.Resolve(newPaid, remainingDebt);
            }
        }

        /// <summary>Remaining debt for a single order (FIFO vs customer ledger balance).</summary>
        public static async Task<decimal> ResolveRemainingDebtAsync(
            AppDbContext db,
            Order order,
            CancellationToken ct = default)
        {
            if (order == null)
                return 0m;

            var map = await ResolveRemainingDebtByOrderIdAsync(
                db,
                order.RestaurantId,
                new[] { order },
                ct);

            return map.TryGetValue(order.OrderId, out var remaining)
                ? remaining
                : (order.SettlementDebtAmount ?? 0m);
        }

        private static async Task<Dictionary<int, decimal>> ResolveRemainingDebtByOrderIdAsync(
            AppDbContext db,
            int restaurantId,
            IEnumerable<Order> orders,
            CancellationToken ct)
        {
            var candidates = orders
                .Where(o => (o.SettlementDebtAmount ?? 0m) > 0m && o.CustomerId is int cid && cid > 0)
                .ToList();

            if (candidates.Count == 0)
                return new Dictionary<int, decimal>();

            var customerIds = candidates.Select(o => o.CustomerId!.Value).Distinct().ToList();
            var balances = await CustomerBalanceLookup.LoadAsync(db, restaurantId, customerIds, ct);

            var allDebtOrders = await db.Orders.AsNoTracking()
                .Where(o => o.RestaurantId == restaurantId
                            && o.CustomerId != null
                            && customerIds.Contains(o.CustomerId.Value)
                            && o.SettlementDebtAmount != null
                            && o.SettlementDebtAmount > 0m)
                .OrderBy(o => o.CreatedAt)
                .ThenBy(o => o.OrderId)
                .Select(o => new
                {
                    o.OrderId,
                    CustomerId = o.CustomerId!.Value,
                    Debt = o.SettlementDebtAmount!.Value
                })
                .ToListAsync(ct);

            var remainingByOrderId = new Dictionary<int, decimal>();

            foreach (var customerId in customerIds)
            {
                var balance = CustomerBalanceLookup.GetBalance(balances, customerId);
                var customerDebts = allDebtOrders.Where(o => o.CustomerId == customerId).ToList();
                if (customerDebts.Count == 0)
                    continue;

                if (balance <= 0m)
                {
                    foreach (var o in customerDebts)
                        remainingByOrderId[o.OrderId] = 0m;
                    continue;
                }

                var totalRecordedDebt = customerDebts.Sum(o => o.Debt);
                var paidOff = Math.Max(0m, totalRecordedDebt - balance);
                var remainingPaidOff = paidOff;

                foreach (var o in customerDebts)
                {
                    var covered = Math.Min(o.Debt, remainingPaidOff);
                    remainingByOrderId[o.OrderId] = o.Debt - covered;
                    remainingPaidOff -= covered;
                }
            }

            return remainingByOrderId;
        }
    }
}
