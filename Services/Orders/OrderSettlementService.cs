using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Models.CustomerAccounts;
using resturanyar.Models.Receipt;
using resturanyar.Services.Receipt;
using resturanyar.Utility;
using Resturanyar.Data;

namespace resturanyar.Services.Orders
{
    public class OrderSettlementService : IOrderSettlementService
    {
        public const int PaidStatusId = 8;
        public const int ClosedStatusId = 11;
        public const int CanceledByCustomerStatusId = 9;
        public const int CanceledByRestaurantStatusId = 10;

        /// <summary>
        /// Statuses that can never receive a new payment registration.
        /// </summary>
        public static bool IsTerminalBlockedStatus(int statusId) =>
            statusId is ClosedStatusId or CanceledByCustomerStatusId or CanceledByRestaurantStatusId;

        /// <summary>
        /// True when payment/credit can still be registered for this order row.
        /// Status 8 without SettlementType is treated as incomplete (legacy UpdateOrderStatus bypass).
        /// </summary>
        public static bool CanRegisterPayment(int statusId, string? settlementType)
        {
            if (IsTerminalBlockedStatus(statusId))
                return false;

            // Status 8 without SettlementType = incomplete legacy jump; still allow registration.
            return string.IsNullOrWhiteSpace(settlementType);
        }

        /// <summary>Legacy helper used by Android adapters — excludes paid/closed/cancelled.</summary>
        public static bool IsEligibleSettlementSource(int statusId) =>
            statusId is not (
                PaidStatusId or
                ClosedStatusId or
                CanceledByCustomerStatusId or
                CanceledByRestaurantStatusId);

        private readonly AppDbContext _db;
        private readonly IReceiptService _receipts;

        public OrderSettlementService(AppDbContext db, IReceiptService receipts)
        {
            _db = db;
            _receipts = receipts;
        }

        public async Task<OrderSettlementPreviewDto> GetPreviewAsync(
            int orderId,
            int restaurantId,
            CancellationToken ct = default)
        {
            var order = await _db.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.RestaurantId == restaurantId, ct)
                ?? throw new InvalidOperationException("سفارش یافت نشد.");

            if (IsTerminalBlockedStatus(order.StatusId))
                throw new InvalidOperationException("این سفارش بسته یا لغو شده و امکان ثبت پرداخت ندارد.");

            var total = await ResolveOrderTotalAsync(order, ct);

            string? fullName = null;
            string? mobile = null;
            decimal? balance = null;

            if (order.CustomerId is int customerId and > 0)
            {
                var customer = await _db.Customers.AsNoTracking()
                    .FirstOrDefaultAsync(
                        c => c.CustomerId == customerId && c.RestaurantId == restaurantId && c.IsActive,
                        ct);

                if (customer != null)
                {
                    fullName = customer.FullName;
                    mobile = customer.Mobile;
                    var balances = await CustomerBalanceLookup.LoadAsync(
                        _db, restaurantId, new[] { customerId }, ct);
                    balance = CustomerBalanceLookup.GetBalance(balances, customerId);
                }
            }

            var alreadySettled = !string.IsNullOrWhiteSpace(order.SettlementType);

            return new OrderSettlementPreviewDto
            {
                OrderId = order.OrderId,
                RestaurantId = order.RestaurantId,
                StatusId = order.StatusId,
                OrderTotal = total,
                CustomerId = order.CustomerId,
                CustomerFullName = fullName,
                CustomerMobile = mobile,
                CustomerAccountBalance = balance,
                AlreadySettled = alreadySettled,
                SettlementType = order.SettlementType
            };
        }

        public async Task<SettleOrderPaymentResultDto> SettlePaymentAsync(
            int orderId,
            int restaurantId,
            SettleOrderPaymentRequest request,
            int? ownerId,
            int? staffUserId,
            CancellationToken ct = default)
        {
            if (request == null)
                throw new InvalidOperationException("درخواست نامعتبر است.");

            if (request.PaidAmount < 0m)
                throw new InvalidOperationException("مبلغ دریافتی نمی‌تواند منفی باشد.");

            if (request.CashReceived is < 0m)
                throw new InvalidOperationException("مبلغ دریافتی نمی‌تواند منفی باشد.");

            // Resolve total outside the write transaction (receipt preview may touch DbContext).
            // Trust DB status — client CurrentStatusId is only a soft hint and may be stale.
            var orderForTotal = await _db.Orders.AsNoTracking()
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.RestaurantId == restaurantId, ct)
                ?? throw new InvalidOperationException("سفارش یافت نشد.");

            if (!string.IsNullOrWhiteSpace(orderForTotal.SettlementType))
            {
                return new SettleOrderPaymentResultDto
                {
                    OrderId = orderForTotal.OrderId,
                    RestaurantId = orderForTotal.RestaurantId,
                    PreviousStatusId = orderForTotal.StatusId,
                    NewStatusId = PaidStatusId,
                    OrderTotal = decimal.Round(await ResolveOrderTotalAsync(orderForTotal, ct), 2, MidpointRounding.AwayFromZero),
                    PaidAmount = orderForTotal.SettlementPaidAmount ?? 0m,
                    DebtAmount = orderForTotal.SettlementDebtAmount ?? 0m,
                    SettlementType = orderForTotal.SettlementType!,
                    CustomerId = orderForTotal.CustomerId,
                    AlreadySettled = true
                };
            }

            if (IsTerminalBlockedStatus(orderForTotal.StatusId))
                throw new InvalidOperationException("این سفارش بسته یا لغو شده و امکان ثبت پرداخت ندارد.");

            if (!CanRegisterPayment(orderForTotal.StatusId, orderForTotal.SettlementType))
                throw new InvalidOperationException("برای این سفارش امکان ثبت پرداخت وجود ندارد.");

            var orderTotal = decimal.Round(await ResolveOrderTotalAsync(orderForTotal, ct), 2, MidpointRounding.AwayFromZero);
            var includePrior = request.IncludePriorBalancePayment;
            var cashReceived = decimal.Round(
                includePrior
                    ? (request.CashReceived ?? request.PaidAmount)
                    : request.PaidAmount,
                2,
                MidpointRounding.AwayFromZero);

            int? resolvedCustomerIdHint = request.CustomerId ?? orderForTotal.CustomerId;
            decimal priorBalance = 0m;

            if (includePrior)
            {
                if (resolvedCustomerIdHint is null or <= 0)
                    throw new InvalidOperationException("برای تسویه مانده قبلی، انتخاب مشتری الزامی است.");

                await RequireActiveCustomerAsync(restaurantId, resolvedCustomerIdHint.Value, ct);
                var balances = await CustomerBalanceLookup.LoadAsync(
                    _db, restaurantId, new[] { resolvedCustomerIdHint.Value }, ct);
                priorBalance = CustomerBalanceLookup.GetBalance(balances, resolvedCustomerIdHint.Value);
                if (priorBalance < 0m) priorBalance = 0m;
            }

            var maxCash = includePrior
                ? orderTotal + priorBalance
                : orderTotal;

            if (cashReceived > maxCash)
                throw new InvalidOperationException(
                    includePrior
                        ? "مبلغ دریافتی نمی‌تواند از مجموع مبلغ سفارش و مانده قبلی بیشتر باشد."
                        : "مبلغ دریافتی نمی‌تواند از مبلغ سفارش بیشتر باشد.");

            // Order-first split
            var paidAmount = Math.Min(cashReceived, orderTotal);
            var priorPaid = includePrior
                ? Math.Min(cashReceived - paidAmount, priorBalance)
                : 0m;
            paidAmount = decimal.Round(paidAmount, 2, MidpointRounding.AwayFromZero);
            priorPaid = decimal.Round(priorPaid, 2, MidpointRounding.AwayFromZero);

            var debtAmount = decimal.Round(orderTotal - paidAmount, 2, MidpointRounding.AwayFromZero);
            var settlementType = OrderSettlementTypes.Resolve(paidAmount, debtAmount);
            var expectedStatusId = orderForTotal.StatusId;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var order = await _db.Orders
                    .FirstOrDefaultAsync(o => o.OrderId == orderId && o.RestaurantId == restaurantId, ct)
                    ?? throw new InvalidOperationException("سفارش یافت نشد.");

                // Concurrent settle won the race — treat as success (do not write prior Payment again).
                if (!string.IsNullOrWhiteSpace(order.SettlementType))
                {
                    await tx.RollbackAsync(ct);
                    return new SettleOrderPaymentResultDto
                    {
                        OrderId = order.OrderId,
                        RestaurantId = order.RestaurantId,
                        PreviousStatusId = order.StatusId,
                        NewStatusId = PaidStatusId,
                        OrderTotal = orderTotal,
                        PaidAmount = order.SettlementPaidAmount ?? paidAmount,
                        DebtAmount = order.SettlementDebtAmount ?? debtAmount,
                        SettlementType = order.SettlementType!,
                        CustomerId = order.CustomerId,
                        AlreadySettled = true
                    };
                }

                if (order.StatusId != expectedStatusId)
                    throw new InvalidOperationException("وضعیت سفارش توسط کاربر دیگری تغییر کرده است. صفحه را تازه کنید و دوباره تلاش کنید.");

                if (!CanRegisterPayment(order.StatusId, order.SettlementType))
                    throw new InvalidOperationException("برای این سفارش امکان ثبت پرداخت وجود ندارد.");

                int? debtTransactionId = null;
                int? priorPaymentTransactionId = null;
                int? resolvedCustomerId = order.CustomerId;
                var priorRemaining = priorBalance;

                if (debtAmount > 0m || priorPaid > 0m || includePrior)
                {
                    var customerId = request.CustomerId ?? order.CustomerId;
                    if (customerId is null or <= 0)
                    {
                        if (debtAmount > 0m)
                            throw new InvalidOperationException("برای ثبت نسیه / بدهی، انتخاب مشتری الزامی است.");
                        if (includePrior || priorPaid > 0m)
                            throw new InvalidOperationException("برای تسویه مانده قبلی، انتخاب مشتری الزامی است.");
                    }
                    else
                    {
                        await RequireActiveCustomerAsync(restaurantId, customerId.Value, ct);
                        if (order.CustomerId != customerId.Value)
                            order.CustomerId = customerId.Value;
                        resolvedCustomerId = customerId.Value;
                    }
                }

                if (debtAmount > 0m)
                {
                    var customerId = resolvedCustomerId!.Value;

                    var alreadyLinked = await _db.CustomerAccountTransactions
                        .AnyAsync(
                            t => t.OrderId == orderId &&
                                 t.Type == CustomerAccountTransactionTypes.Debt,
                            ct);
                    if (alreadyLinked)
                        throw new InvalidOperationException("بدهی این سفارش قبلاً ثبت شده است.");

                    var account = await GetOrCreateAccountAsync(restaurantId, customerId, ct);
                    var note = string.IsNullOrWhiteSpace(request.Note)
                        ? BuildDefaultDebtNote(orderId, paidAmount, debtAmount)
                        : request.Note.Trim();

                    var newBalance = account.CurrentBalance + debtAmount;
                    var entry = new CustomerAccountTransaction
                    {
                        AccountId = account.AccountId,
                        RestaurantId = restaurantId,
                        CustomerId = customerId,
                        Type = CustomerAccountTransactionTypes.Debt,
                        Amount = debtAmount,
                        SignedAmount = debtAmount,
                        BalanceAfter = newBalance,
                        Note = note,
                        OrderId = orderId,
                        PaidAmount = paidAmount,
                        CreatedAt = DateTime.UtcNow,
                        CreatedByOwnerId = ownerId,
                        CreatedByStaffUserId = staffUserId
                    };

                    _db.CustomerAccountTransactions.Add(entry);
                    account.CurrentBalance = newBalance;
                    account.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                    debtTransactionId = entry.TransactionId;
                }

                if (priorPaid > 0m)
                {
                    var customerId = resolvedCustomerId!.Value;
                    // Cap against the pre-settle prior balance (exclude this order's new Debt).
                    priorPaid = decimal.Round(Math.Min(priorPaid, priorBalance), 2, MidpointRounding.AwayFromZero);
                    priorRemaining = decimal.Round(Math.Max(0m, priorBalance - priorPaid), 2, MidpointRounding.AwayFromZero);

                    if (priorPaid > 0m)
                    {
                        var account = await GetOrCreateAccountAsync(restaurantId, customerId, ct);
                        var payNote = $"تسویه مانده هنگام ثبت پرداخت سفارش #{orderId}";
                        var newBalance = account.CurrentBalance - priorPaid;
                        var payEntry = new CustomerAccountTransaction
                        {
                            AccountId = account.AccountId,
                            RestaurantId = restaurantId,
                            CustomerId = customerId,
                            Type = CustomerAccountTransactionTypes.Payment,
                            Amount = priorPaid,
                            SignedAmount = -priorPaid,
                            BalanceAfter = newBalance,
                            Note = payNote,
                            OrderId = orderId,
                            PaidAmount = null,
                            CreatedAt = DateTime.UtcNow,
                            CreatedByOwnerId = ownerId,
                            CreatedByStaffUserId = staffUserId
                        };

                        _db.CustomerAccountTransactions.Add(payEntry);
                        account.CurrentBalance = newBalance;
                        account.UpdatedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync(ct);
                        priorPaymentTransactionId = payEntry.TransactionId;

                        // Reduce older orders' recorded نسیه so their reprints show paid.
                        await OrderDebtCreditApplicator.ApplyAsync(
                            _db,
                            restaurantId,
                            customerId,
                            priorPaid,
                            preferOrderId: null,
                            excludeOrderId: orderId,
                            ct);
                    }
                }

                var previousStatusId = order.StatusId;
                order.SettlementPaidAmount = paidAmount;
                order.SettlementDebtAmount = debtAmount;
                order.SettlementType = settlementType;
                order.StatusId = PaidStatusId;
                order.UpdatedAt = DateTime.Now;

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return new SettleOrderPaymentResultDto
                {
                    OrderId = order.OrderId,
                    RestaurantId = order.RestaurantId,
                    PreviousStatusId = previousStatusId,
                    NewStatusId = PaidStatusId,
                    OrderTotal = orderTotal,
                    PaidAmount = paidAmount,
                    DebtAmount = debtAmount,
                    SettlementType = settlementType,
                    CustomerId = resolvedCustomerId,
                    DebtTransactionId = debtTransactionId,
                    AlreadySettled = false,
                    PriorBalancePaidAmount = priorPaid,
                    PriorBalanceRemaining = priorRemaining,
                    PriorPaymentTransactionId = priorPaymentTransactionId
                };
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        private async Task<decimal> ResolveOrderTotalAsync(Order order, CancellationToken ct)
        {
            // Prefer issued receipt snapshot
            var snapshotTotal = await _db.OrderReceiptSnapshots.AsNoTracking()
                .Where(s => s.OrderId == order.OrderId)
                .OrderByDescending(s => s.IssuedAt)
                .Select(s => (decimal?)s.GrandTotal)
                .FirstOrDefaultAsync(ct);

            if (snapshotTotal.HasValue)
                return snapshotTotal.Value;

            // Live receipt preview (includes charges / discounts when configured)
            try
            {
                var preview = await _receipts.PreviewAsync(
                    order.OrderId,
                    order.RestaurantId,
                    new ReceiptPreviewRequest { OrderType = order.OrderType });

                if (preview.Success && preview.Receipt != null)
                    return preview.Receipt.GrandTotal;
            }
            catch
            {
                // Fall through to items sum
            }

            if (order.OrderItems != null && order.OrderItems.Count > 0)
            {
                return order.OrderItems.Sum(oi =>
                    (oi.UnitPriceWithDiscount ?? oi.UnitPrice) * oi.Quantity);
            }

            var itemsTotal = await _db.OrderItems.AsNoTracking()
                .Where(oi => oi.OrderId == order.OrderId)
                .SumAsync(
                    oi => (oi.UnitPriceWithDiscount ?? oi.UnitPrice) * oi.Quantity,
                    ct);

            return itemsTotal;
        }

        private static string BuildDefaultDebtNote(int orderId, decimal paidAmount, decimal debtAmount)
        {
            if (paidAmount <= 0m)
                return $"نسیه کامل سفارش #{orderId}";

            return $"نسیه سفارش #{orderId} — نقدی: {paidAmount:0.##} — بدهی: {debtAmount:0.##}";
        }

        private async Task RequireActiveCustomerAsync(int restaurantId, int customerId, CancellationToken ct)
        {
            var ok = await _db.Customers.AsNoTracking()
                .AnyAsync(
                    c => c.CustomerId == customerId &&
                         c.RestaurantId == restaurantId &&
                         c.IsActive,
                    ct);

            if (!ok)
                throw new InvalidOperationException("مشتری یافت نشد یا غیرفعال است.");
        }

        private async Task<CustomerAccount> GetOrCreateAccountAsync(
            int restaurantId,
            int customerId,
            CancellationToken ct)
        {
            var account = await _db.CustomerAccounts
                .FirstOrDefaultAsync(
                    a => a.RestaurantId == restaurantId && a.CustomerId == customerId,
                    ct);

            if (account != null)
                return account;

            account = new CustomerAccount
            {
                RestaurantId = restaurantId,
                CustomerId = customerId,
                CurrentBalance = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.CustomerAccounts.Add(account);
            await _db.SaveChangesAsync(ct);
            return account;
        }
    }
}
