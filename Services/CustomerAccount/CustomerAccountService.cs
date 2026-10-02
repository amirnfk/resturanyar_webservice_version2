using Microsoft.EntityFrameworkCore;
using resturanyar.Models.CustomerAccounts;
using resturanyar.Models.Receipt;
using Resturanyar.Data;

namespace resturanyar.Services.CustomerAccounts
{
    public class CustomerAccountService : ICustomerAccountService
    {
        private readonly AppDbContext _db;

        public CustomerAccountService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<CustomerAccountSummaryDto> GetAccountSummaryAsync(
            int restaurantId,
            int customerId,
            CancellationToken ct = default)
        {
            var customer = await RequireActiveCustomerAsync(restaurantId, customerId, ct);
            var account = await GetOrCreateAccountAsync(restaurantId, customerId, ct);

            var computedBalance = await _db.CustomerAccountTransactions.AsNoTracking()
                .Where(t => t.AccountId == account.AccountId)
                .SumAsync(t => (decimal?)t.SignedAmount, ct) ?? 0m;

            // Heal stale CurrentBalance if ledger and cached balance diverged
            if (account.CurrentBalance != computedBalance)
            {
                account.CurrentBalance = computedBalance;
                account.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            var totals = await _db.CustomerAccountTransactions.AsNoTracking()
                .Where(t => t.AccountId == account.AccountId)
                .GroupBy(t => 1)
                .Select(g => new
                {
                    TotalDebt = g.Where(t => t.SignedAmount > 0).Sum(t => (decimal?)t.SignedAmount) ?? 0m,
                    TotalPayment = g.Where(t => t.SignedAmount < 0).Sum(t => (decimal?)t.SignedAmount) ?? 0m,
                    Count = g.Count(),
                    LastAt = g.Max(t => (DateTime?)t.CreatedAt)
                })
                .FirstOrDefaultAsync(ct);

            return new CustomerAccountSummaryDto
            {
                AccountId = account.AccountId,
                RestaurantId = account.RestaurantId,
                CustomerId = account.CustomerId,
                CustomerFullName = customer.FullName,
                CustomerMobile = customer.Mobile,
                CurrentBalance = computedBalance,
                UpdatedAt = account.UpdatedAt,
                TotalDebt = totals?.TotalDebt ?? 0m,
                TotalPayment = Math.Abs(totals?.TotalPayment ?? 0m),
                TransactionCount = totals?.Count ?? 0,
                LastTransactionAt = totals?.LastAt
            };
        }

        public async Task<CustomerAccountTransactionListDto> ListTransactionsAsync(
            int restaurantId,
            int customerId,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default)
        {
            await RequireActiveCustomerAsync(restaurantId, customerId, ct);
            var account = await GetOrCreateAccountAsync(restaurantId, customerId, ct);

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var query = _db.CustomerAccountTransactions.AsNoTracking()
                .Where(t => t.AccountId == account.AccountId);

            var totalCount = await query.CountAsync(ct);
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.TransactionId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new CustomerAccountTransactionDto
                {
                    TransactionId = t.TransactionId,
                    AccountId = t.AccountId,
                    CustomerId = t.CustomerId,
                    Type = t.Type,
                    Amount = t.Amount,
                    SignedAmount = t.SignedAmount,
                    BalanceAfter = t.BalanceAfter,
                    Note = t.Note,
                    CreatedAt = t.CreatedAt,
                    CreatedByOwnerId = t.CreatedByOwnerId,
                    CreatedByStaffUserId = t.CreatedByStaffUserId
                })
                .ToListAsync(ct);

            return new CustomerAccountTransactionListDto
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };
        }

        public async Task<CustomerAccountTransactionDto> AddTransactionAsync(
            AddCustomerAccountTransactionRequest request,
            int? ownerId,
            int? staffUserId,
            CancellationToken ct = default)
        {
            if (request == null)
                throw new InvalidOperationException("درخواست نامعتبر است.");

            var type = (request.Type ?? string.Empty).Trim();
            if (!CustomerAccountTransactionTypes.IsValid(type))
                throw new InvalidOperationException("نوع تراکنش نامعتبر است. مقادیر مجاز: Debt، Payment، Adjustment.");

            if (request.Amount <= 0)
                throw new InvalidOperationException("مبلغ باید بزرگ‌تر از صفر باشد.");

            // Normalize canonical casing
            type = CustomerAccountTransactionTypes.All
                .First(t => t.Equals(type, StringComparison.OrdinalIgnoreCase));

            var signedAmount = ResolveSignedAmount(type, request.Amount, request.AdjustmentSign);

            await RequireActiveCustomerAsync(request.RestaurantId, request.CustomerId, ct);

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var account = await _db.CustomerAccounts
                    .FirstOrDefaultAsync(
                        a => a.RestaurantId == request.RestaurantId && a.CustomerId == request.CustomerId,
                        ct);

                if (account == null)
                {
                    account = new CustomerAccount
                    {
                        RestaurantId = request.RestaurantId,
                        CustomerId = request.CustomerId,
                        CurrentBalance = 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.CustomerAccounts.Add(account);
                    await _db.SaveChangesAsync(ct);
                }

                var newBalance = account.CurrentBalance + signedAmount;
                var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

                var entry = new CustomerAccountTransaction
                {
                    AccountId = account.AccountId,
                    RestaurantId = request.RestaurantId,
                    CustomerId = request.CustomerId,
                    Type = type,
                    Amount = request.Amount,
                    SignedAmount = signedAmount,
                    BalanceAfter = newBalance,
                    Note = note,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByOwnerId = ownerId,
                    CreatedByStaffUserId = staffUserId
                };

                _db.CustomerAccountTransactions.Add(entry);
                account.CurrentBalance = newBalance;
                account.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return new CustomerAccountTransactionDto
                {
                    TransactionId = entry.TransactionId,
                    AccountId = entry.AccountId,
                    CustomerId = entry.CustomerId,
                    Type = entry.Type,
                    Amount = entry.Amount,
                    SignedAmount = entry.SignedAmount,
                    BalanceAfter = entry.BalanceAfter,
                    Note = entry.Note,
                    CreatedAt = entry.CreatedAt,
                    CreatedByOwnerId = entry.CreatedByOwnerId,
                    CreatedByStaffUserId = entry.CreatedByStaffUserId
                };
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<CustomerPurchaseHistoryListDto> ListPurchaseHistoryAsync(
            int restaurantId,
            int customerId,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default)
        {
            await RequireActiveCustomerAsync(restaurantId, customerId, ct);

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            // Same rule as باشگاه مشتریان stats: closed orders only (StatusId == 11)
            var ordersQuery = _db.Orders.AsNoTracking()
                .Where(o =>
                    o.RestaurantId == restaurantId &&
                    o.CustomerId == customerId &&
                    o.StatusId == 11);

            var totalCount = await ordersQuery.CountAsync(ct);
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

            var pageOrders = await ordersQuery
                .OrderByDescending(o => o.CreatedAt)
                .ThenByDescending(o => o.OrderId)
                .Skip((page - 1) * pageSize)
                .Select(o => new
                {
                    o.OrderId,
                    o.CreatedAt,
                    o.CreatedAtShamsi,
                    o.OrderType,
                    o.TableNumber,
                    o.Description,
                    ItemCount = o.OrderItems.Sum(oi => oi.Quantity),
                    ItemsTotal = o.OrderItems.Sum(oi =>
                        (oi.UnitPriceWithDiscount ?? oi.UnitPrice) * oi.Quantity)
                })
                .ToListAsync(ct);

            var orderIds = pageOrders.Select(o => o.OrderId).ToList();
            var receiptRows = orderIds.Count == 0
                ? new List<(int OrderId, decimal GrandTotal, DateTime IssuedAt)>()
                : (await _db.OrderReceiptSnapshots.AsNoTracking()
                    .Where(s => orderIds.Contains(s.OrderId))
                    .Select(s => new { s.OrderId, s.GrandTotal, s.IssuedAt })
                    .ToListAsync(ct))
                    .Select(s => (s.OrderId, s.GrandTotal, s.IssuedAt))
                    .ToList();

            var receiptByOrder = receiptRows
                .GroupBy(r => r.OrderId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.IssuedAt).First().GrandTotal);

            // Same basis as باشگاه مشتریان (sum of line items on closed orders)
            var totalSpent = totalCount == 0
                ? 0m
                : await ordersQuery
                    .Select(o => o.OrderItems.Sum(oi =>
                        (oi.UnitPriceWithDiscount ?? oi.UnitPrice) * oi.Quantity))
                    .SumAsync(ct);

            // Prefer receipt totals for the current page amounts when available
            var items = pageOrders.Select(o =>
            {
                var hasReceipt = receiptByOrder.TryGetValue(o.OrderId, out var receiptTotal);
                return new CustomerPurchaseHistoryItemDto
                {
                    OrderId = o.OrderId,
                    CreatedAt = o.CreatedAt,
                    CreatedAtShamsi = o.CreatedAtShamsi,
                    OrderType = (byte)o.OrderType,
                    OrderTypeLabel = OrderTypeLabel(o.OrderType),
                    TableNumber = o.TableNumber,
                    ItemCount = o.ItemCount,
                    Amount = hasReceipt ? receiptTotal : o.ItemsTotal,
                    HasReceipt = hasReceipt,
                    Description = o.Description
                };
            }).ToList();

            return new CustomerPurchaseHistoryListDto
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalSpent = totalSpent
            };
        }

        private static string OrderTypeLabel(OrderTypeKind type) => type switch
        {
            OrderTypeKind.Takeaway => "بیرون‌بر",
            OrderTypeKind.Delivery => "ارسال",
            _ => "سالن"
        };

        private static decimal ResolveSignedAmount(string type, decimal amount, int? adjustmentSign)
        {
            if (type.Equals(CustomerAccountTransactionTypes.Debt, StringComparison.OrdinalIgnoreCase))
                return amount;

            if (type.Equals(CustomerAccountTransactionTypes.Payment, StringComparison.OrdinalIgnoreCase))
                return -amount;

            // Adjustment
            if (adjustmentSign is not (1 or -1))
                throw new InvalidOperationException("برای اصلاح حساب، علامت AdjustmentSign باید ۱ (افزایش بدهی) یا ۱- (کاهش بدهی) باشد.");

            return amount * adjustmentSign.Value;
        }

        private async Task<CustomerAccount> GetOrCreateAccountAsync(
            int restaurantId,
            int customerId,
            CancellationToken ct)
        {
            var account = await _db.CustomerAccounts
                .FirstOrDefaultAsync(a => a.RestaurantId == restaurantId && a.CustomerId == customerId, ct);

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

        private async Task<Models.CustomerModels.Customer> RequireActiveCustomerAsync(
            int restaurantId,
            int customerId,
            CancellationToken ct)
        {
            if (restaurantId <= 0 || customerId <= 0)
                throw new InvalidOperationException("شناسه رستوران و مشتری الزامی است.");

            var customer = await _db.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.RestaurantId == restaurantId, ct);

            if (customer == null)
                throw new InvalidOperationException("مشتری یافت نشد.");

            if (!customer.IsActive)
                throw new InvalidOperationException("این مشتری غیرفعال است.");

            return customer;
        }
    }
}
