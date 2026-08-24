using Microsoft.EntityFrameworkCore;
using resturanyar.Helpers;
using resturanyar.Models;
using resturanyar.Models.DiscountCodes;
using Resturanyar.Data;

namespace resturanyar.Services.DiscountCodes
{
    public interface IDiscountCodeService
    {
        Task<List<DiscountCodeDto>> ListAsync(int restaurantId, CancellationToken ct = default);
        Task<DiscountCodeDto?> GetByIdAsync(int restaurantId, int id, CancellationToken ct = default);
        Task<(bool Success, string Message, DiscountCodeDto? Data)> CreateAsync(UpsertDiscountCodeRequest request, CancellationToken ct = default);
        Task<(bool Success, string Message, DiscountCodeDto? Data)> UpdateAsync(int id, UpsertDiscountCodeRequest request, CancellationToken ct = default);
        Task<(bool Success, string Message)> DeleteAsync(int restaurantId, int id, CancellationToken ct = default);
        Task<DiscountCodeValidationResult> ValidateAsync(ValidateDiscountCodeRequest request, CancellationToken ct = default);
        decimal CalculateAmount(RestaurantDiscountCode code, decimal itemsSubtotal);
        /// <summary>Soft-bind a code to the order without consuming usage (usage commits at receipt issue).</summary>
        Task<(bool Success, string Message)> AttachToOrderAsync(Order order, string code, CancellationToken ct = default);
        Task<(bool Success, string Message)> DetachFromOrderAsync(Order order, CancellationToken ct = default);
        Task<(bool Success, string Message)> RefreshAttachedUsageAsync(Order order, CancellationToken ct = default);
        /// <summary>Finalize usage when issuing a receipt. Idempotent if usage already exists.</summary>
        Task<(bool Success, string Message)> CommitUsageForOrderAsync(Order order, CancellationToken ct = default);
        Task<bool> HasCommittedUsageAsync(int orderId, CancellationToken ct = default);
        Task<RestaurantDiscountCode?> GetAttachedDefinitionAsync(int? discountCodeId, CancellationToken ct = default);
    }

    public class DiscountCodeService : IDiscountCodeService
    {
        public const string ChargeLineCode = "discount_code";
        public const string PercentageType = "Percentage";
        public const string FixedAmountType = "FixedAmount";

        public static string NormalizeCode(string? code)
            => (code ?? string.Empty).Trim().ToUpperInvariant();

        private readonly AppDbContext _context;

        public DiscountCodeService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<DiscountCodeDto>> ListAsync(int restaurantId, CancellationToken ct = default)
        {
            var rows = await (
                from c in _context.RestaurantDiscountCodes.AsNoTracking()
                where c.RestaurantId == restaurantId
                join cust in _context.Customers.AsNoTracking()
                    on c.SpecificCustomerId equals cust.CustomerId into gj
                from cust in gj.DefaultIfEmpty()
                orderby c.CreatedAt descending
                select new { Code = c, Customer = cust }
            ).ToListAsync(ct);

            return rows.Select(r => MapDto(r.Code, r.Customer?.FullName, r.Customer?.Mobile)).ToList();
        }

        public async Task<DiscountCodeDto?> GetByIdAsync(int restaurantId, int id, CancellationToken ct = default)
        {
            var row = await (
                from c in _context.RestaurantDiscountCodes.AsNoTracking()
                where c.Id == id && c.RestaurantId == restaurantId
                join cust in _context.Customers.AsNoTracking()
                    on c.SpecificCustomerId equals cust.CustomerId into gj
                from cust in gj.DefaultIfEmpty()
                select new { Code = c, Customer = cust }
            ).FirstOrDefaultAsync(ct);

            return row == null ? null : MapDto(row.Code, row.Customer?.FullName, row.Customer?.Mobile);
        }

        public async Task<(bool Success, string Message, DiscountCodeDto? Data)> CreateAsync(
            UpsertDiscountCodeRequest request,
            CancellationToken ct = default)
        {
            var normalize = NormalizeRequest(request);
            if (!normalize.Success)
                return (false, normalize.Message, null);

            var customerCheck = await ResolveSpecificCustomerAsync(request.RestaurantId, request.SpecificCustomerId, ct);
            if (!customerCheck.Success)
                return (false, customerCheck.Message, null);

            var code = DiscountCodeService.NormalizeCode(request.Code);
            var exists = await _context.RestaurantDiscountCodes
                .AnyAsync(c => c.RestaurantId == request.RestaurantId && c.Code == code, ct);
            if (exists)
                return (false, "این کد تخفیف قبلاً برای این رستوران ثبت شده است.", null);

            var now = DateTime.Now;
            var entity = new RestaurantDiscountCode
            {
                RestaurantId = request.RestaurantId,
                Code = code,
                Title = request.Title.Trim(),
                DiscountType = normalize.DiscountType!,
                DiscountValue = request.DiscountValue,
                MinOrderAmount = request.MinOrderAmount,
                MaxDiscountAmount = request.MaxDiscountAmount,
                StartDate = NormalizeStartDate(request.StartDate),
                EndDate = NormalizeEndDate(request.EndDate),
                UsageLimit = request.UsageLimit,
                PerCustomerUsageLimit = request.PerCustomerUsageLimit,
                SpecificCustomerId = request.SpecificCustomerId,
                IsActive = request.IsActive,
                UsedCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.RestaurantDiscountCodes.Add(entity);
            await _context.SaveChangesAsync(ct);
            return (true, "کد تخفیف با موفقیت ایجاد شد.",
                MapDto(entity, customerCheck.FullName, customerCheck.Mobile));
        }

        public async Task<(bool Success, string Message, DiscountCodeDto? Data)> UpdateAsync(
            int id,
            UpsertDiscountCodeRequest request,
            CancellationToken ct = default)
        {
            var normalize = NormalizeRequest(request);
            if (!normalize.Success)
                return (false, normalize.Message, null);

            var customerCheck = await ResolveSpecificCustomerAsync(request.RestaurantId, request.SpecificCustomerId, ct);
            if (!customerCheck.Success)
                return (false, customerCheck.Message, null);

            var entity = await _context.RestaurantDiscountCodes
                .FirstOrDefaultAsync(c => c.Id == id && c.RestaurantId == request.RestaurantId, ct);
            if (entity == null)
                return (false, "کد تخفیف یافت نشد.", null);

            var code = DiscountCodeService.NormalizeCode(request.Code);
            var hasUsages = await _context.OrderDiscountCodeUsages
                .AnyAsync(u => u.DiscountCodeId == id, ct);
            var isUsed = hasUsages || entity.UsedCount > 0;

            if (isUsed)
            {
                if (!IsSameEditablePayload(entity, request, normalize.DiscountType!, code))
                    return (false, "این کد قبلاً استفاده شده و قابل ویرایش نیست. فقط می‌توانید آن را فعال یا غیرفعال کنید.", null);

                if (entity.IsActive == request.IsActive)
                    return (true, "تغییری ثبت نشد.",
                        MapDto(entity, customerCheck.FullName, customerCheck.Mobile));

                entity.IsActive = request.IsActive;
                entity.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync(ct);
                return (true, request.IsActive ? "کد تخفیف فعال شد." : "کد تخفیف غیرفعال شد.",
                    MapDto(entity, customerCheck.FullName, customerCheck.Mobile));
            }

            var duplicate = await _context.RestaurantDiscountCodes
                .AnyAsync(c => c.RestaurantId == request.RestaurantId && c.Code == code && c.Id != id, ct);
            if (duplicate)
                return (false, "این کد تخفیف قبلاً برای این رستوران ثبت شده است.", null);

            entity.Code = code;
            entity.Title = request.Title.Trim();
            entity.DiscountType = normalize.DiscountType!;
            entity.DiscountValue = request.DiscountValue;
            entity.MinOrderAmount = request.MinOrderAmount;
            entity.MaxDiscountAmount = request.MaxDiscountAmount;
            entity.StartDate = NormalizeStartDate(request.StartDate);
            entity.EndDate = NormalizeEndDate(request.EndDate);
            entity.UsageLimit = request.UsageLimit;
            entity.PerCustomerUsageLimit = request.PerCustomerUsageLimit;
            entity.SpecificCustomerId = request.SpecificCustomerId;
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync(ct);
            return (true, "کد تخفیف به‌روزرسانی شد.",
                MapDto(entity, customerCheck.FullName, customerCheck.Mobile));
        }

        public async Task<(bool Success, string Message)> DeleteAsync(int restaurantId, int id, CancellationToken ct = default)
        {
            var entity = await _context.RestaurantDiscountCodes
                .FirstOrDefaultAsync(c => c.Id == id && c.RestaurantId == restaurantId, ct);
            if (entity == null)
                return (false, "کد تخفیف یافت نشد.");

            var hasUsages = await _context.OrderDiscountCodeUsages
                .AnyAsync(u => u.DiscountCodeId == id, ct);
            if (hasUsages || entity.UsedCount > 0)
                return (false, "این کد قبلاً روی سفارش استفاده شده است. به‌جای حذف، آن را غیرفعال کنید.");

            _context.RestaurantDiscountCodes.Remove(entity);
            await _context.SaveChangesAsync(ct);
            return (true, "کد تخفیف حذف شد.");
        }

        public async Task<DiscountCodeValidationResult> ValidateAsync(
            ValidateDiscountCodeRequest request,
            CancellationToken ct = default)
        {
            var codeText = DiscountCodeService.NormalizeCode(request.Code);
            if (string.IsNullOrWhiteSpace(codeText))
                return FailValidation("کد تخفیف وارد نشده است.");

            if (request.ItemsSubtotal < 0)
                return FailValidation("مبلغ سفارش نامعتبر است.");

            var code = await _context.RestaurantDiscountCodes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.RestaurantId == request.RestaurantId && c.Code == codeText,
                    ct);

            if (code == null)
                return FailValidation("کد تخفیف نامعتبر است.");

            var check = await CheckEligibilityAsync(code, request.ItemsSubtotal, request.CustomerId, request.ExcludeOrderId, ct);
            if (!check.Success)
                return FailValidation(check.Message);

            var amount = CalculateAmount(code, request.ItemsSubtotal);
            var final = request.ItemsSubtotal - amount;
            if (final < 0) final = 0;

            return new DiscountCodeValidationResult
            {
                Success = true,
                Message = "کد تخفیف معتبر است.",
                DiscountCodeId = code.Id,
                Code = code.Code,
                Title = code.Title,
                DiscountType = code.DiscountType,
                DiscountValue = code.DiscountValue,
                DiscountAmount = amount,
                ItemsSubtotal = RoundMoney(request.ItemsSubtotal),
                FinalSubtotalAfterDiscount = RoundMoney(final)
            };
        }

        public decimal CalculateAmount(RestaurantDiscountCode code, decimal itemsSubtotal)
        {
            var subtotal = RoundMoney(itemsSubtotal);
            if (subtotal <= 0 || code.DiscountValue <= 0)
                return 0;

            decimal amount;
            if (string.Equals(code.DiscountType, FixedAmountType, StringComparison.OrdinalIgnoreCase))
            {
                amount = code.DiscountValue;
            }
            else
            {
                amount = subtotal * (code.DiscountValue / 100m);
                if (code.MaxDiscountAmount.HasValue)
                    amount = Math.Min(amount, code.MaxDiscountAmount.Value);
            }

            if (amount > subtotal)
                amount = subtotal;
            if (amount < 0)
                amount = 0;

            return RoundMoney(amount);
        }

        public async Task<(bool Success, string Message)> AttachToOrderAsync(
            Order order,
            string code,
            CancellationToken ct = default)
        {
            if (order == null)
                return (false, "سفارش نامعتبر است.");

            var subtotal = ComputeItemsSubtotal(order);
            var validation = await ValidateAsync(new ValidateDiscountCodeRequest
            {
                RestaurantId = order.RestaurantId,
                Code = code,
                ItemsSubtotal = subtotal,
                CustomerId = order.CustomerId,
                ExcludeOrderId = order.OrderId
            }, ct);

            if (!validation.Success || !validation.DiscountCodeId.HasValue)
                return (false, validation.Message);

            // Same code already soft-attached — re-check eligibility only.
            if (order.DiscountCodeId == validation.DiscountCodeId.Value)
                return await RefreshAttachedUsageAsync(order, ct);

            if (order.DiscountCodeId.HasValue)
            {
                var detach = await DetachFromOrderAsync(order, ct);
                if (!detach.Success)
                    return detach;
            }

            var entity = await _context.RestaurantDiscountCodes
                .FirstOrDefaultAsync(c => c.Id == validation.DiscountCodeId.Value, ct);
            if (entity == null)
                return (false, "کد تخفیف یافت نشد.");

            var recheck = await CheckEligibilityAsync(entity, subtotal, order.CustomerId, order.OrderId, ct);
            if (!recheck.Success)
                return recheck;

            // Soft bind only — usage is consumed when the receipt is issued.
            order.DiscountCodeId = entity.Id;
            await _context.SaveChangesAsync(ct);
            return (true, "کد تخفیف روی سفارش ثبت شد.");
        }

        public async Task<(bool Success, string Message)> DetachFromOrderAsync(Order order, CancellationToken ct = default)
        {
            if (order == null)
                return (false, "سفارش نامعتبر است.");

            if (!order.DiscountCodeId.HasValue)
                return (true, "کد تخفیفی روی سفارش نیست.");

            var usage = await _context.OrderDiscountCodeUsages
                .FirstOrDefaultAsync(u => u.OrderId == order.OrderId, ct);

            var codeId = order.DiscountCodeId.Value;
            var entity = await _context.RestaurantDiscountCodes
                .FirstOrDefaultAsync(c => c.Id == codeId, ct);

            if (usage != null)
            {
                _context.OrderDiscountCodeUsages.Remove(usage);
                if (entity != null && entity.UsedCount > 0)
                {
                    entity.UsedCount -= 1;
                    entity.UpdatedAt = DateTime.Now;
                }
            }

            order.DiscountCodeId = null;
            await _context.SaveChangesAsync(ct);
            return (true, "کد تخفیف از سفارش حذف شد.");
        }

        public async Task<(bool Success, string Message)> RefreshAttachedUsageAsync(Order order, CancellationToken ct = default)
        {
            if (order?.DiscountCodeId == null)
                return (true, "کد تخفیفی روی سفارش نیست.");

            var entity = await _context.RestaurantDiscountCodes
                .FirstOrDefaultAsync(c => c.Id == order.DiscountCodeId.Value, ct);
            if (entity == null)
            {
                await DetachFromOrderAsync(order, ct);
                return (false, "کد تخفیف دیگر موجود نیست و از سفارش حذف شد.");
            }

            var subtotal = ComputeItemsSubtotal(order);
            var check = await CheckEligibilityAsync(entity, subtotal, order.CustomerId, order.OrderId, ct);
            if (!check.Success)
                return check;

            var amount = CalculateAmount(entity, subtotal);
            var usage = await _context.OrderDiscountCodeUsages
                .FirstOrDefaultAsync(u => u.OrderId == order.OrderId, ct);

            // Soft-attached only: nothing to update until receipt commit.
            if (usage == null)
                return (true, "کد تخفیف هنوز روی سفارش ثبت است.");

            usage.DiscountAmount = amount;
            usage.ItemsSubtotalAtApply = RoundMoney(subtotal);
            usage.CustomerId = order.CustomerId;
            await _context.SaveChangesAsync(ct);
            return (true, "مبلغ تخفیف به‌روزرسانی شد.");
        }

        public async Task<(bool Success, string Message)> CommitUsageForOrderAsync(
            Order order,
            CancellationToken ct = default)
        {
            if (order == null)
                return (false, "سفارش نامعتبر است.");

            if (!order.DiscountCodeId.HasValue)
                return (true, "ok");

            var entity = await _context.RestaurantDiscountCodes
                .FirstOrDefaultAsync(c => c.Id == order.DiscountCodeId.Value, ct);
            if (entity == null)
            {
                order.DiscountCodeId = null;
                await _context.SaveChangesAsync(ct);
                return (false, "کد تخفیف دیگر موجود نیست و نمی‌توان فاکتور را با آن صادر کرد.");
            }

            var subtotal = ComputeItemsSubtotal(order);
            var check = await CheckEligibilityAsync(entity, subtotal, order.CustomerId, order.OrderId, ct);
            if (!check.Success)
                return (false, check.Message + " کد را از سفارش حذف کنید یا اقلام را اصلاح کنید.");

            var amount = CalculateAmount(entity, subtotal);
            var usage = await _context.OrderDiscountCodeUsages
                .FirstOrDefaultAsync(u => u.OrderId == order.OrderId, ct);

            if (usage != null)
            {
                usage.DiscountAmount = amount;
                usage.ItemsSubtotalAtApply = RoundMoney(subtotal);
                usage.CustomerId = order.CustomerId;
                usage.UsedAt = DateTime.Now;
                await _context.SaveChangesAsync(ct);
                return (true, "مصرف کد تخفیف به‌روزرسانی شد.");
            }

            entity.UsedCount += 1;
            entity.UpdatedAt = DateTime.Now;
            _context.OrderDiscountCodeUsages.Add(new OrderDiscountCodeUsage
            {
                DiscountCodeId = entity.Id,
                OrderId = order.OrderId,
                RestaurantId = order.RestaurantId,
                CustomerId = order.CustomerId,
                DiscountAmount = amount,
                ItemsSubtotalAtApply = RoundMoney(subtotal),
                UsedAt = DateTime.Now
            });

            await _context.SaveChangesAsync(ct);
            return (true, "مصرف کد تخفیف هنگام صدور فاکتور ثبت شد.");
        }

        public Task<bool> HasCommittedUsageAsync(int orderId, CancellationToken ct = default)
            => _context.OrderDiscountCodeUsages.AsNoTracking()
                .AnyAsync(u => u.OrderId == orderId, ct);

        public Task<RestaurantDiscountCode?> GetAttachedDefinitionAsync(int? discountCodeId, CancellationToken ct = default)
        {
            if (!discountCodeId.HasValue)
                return Task.FromResult<RestaurantDiscountCode?>(null);

            return _context.RestaurantDiscountCodes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == discountCodeId.Value, ct);
        }

        private async Task<(bool Success, string Message)> CheckEligibilityAsync(
            RestaurantDiscountCode code,
            decimal itemsSubtotal,
            int? customerId,
            int? excludeOrderId,
            CancellationToken ct)
        {
            if (!code.IsActive)
                return (false, "این کد تخفیف غیرفعال است.");

            var now = DateTime.Now;
            if (now < code.StartDate || now > code.EndDate)
                return (false, "کد تخفیف منقضی شده یا هنوز فعال نشده است.");

            if (code.MinOrderAmount.HasValue && itemsSubtotal < code.MinOrderAmount.Value)
                return (false, $"حداقل مبلغ سفارش برای این کد {code.MinOrderAmount.Value:N0} تومان است.");

            if (code.SpecificCustomerId.HasValue)
            {
                if (!customerId.HasValue)
                    return (false, "این کد مخصوص یک مشتری خاص است؛ ابتدا مشتری را انتخاب کنید.");

                if (customerId.Value != code.SpecificCustomerId.Value)
                    return (false, "این کد تخفیف مخصوص مشتری دیگری است.");
            }

            if (code.UsageLimit.HasValue)
            {
                var used = await _context.OrderDiscountCodeUsages
                    .CountAsync(u => u.DiscountCodeId == code.Id
                                     && (!excludeOrderId.HasValue || u.OrderId != excludeOrderId.Value), ct);
                if (used >= code.UsageLimit.Value)
                    return (false, "تعداد استفاده از این کد به پایان رسیده است.");
            }

            if (code.PerCustomerUsageLimit.HasValue && code.PerCustomerUsageLimit.Value > 0)
            {
                // Per-customer caps only apply when the order has an identifiable customer.
                // Public codes must still work on guest/walk-in orders (no customer selected).
                if (customerId.HasValue)
                {
                    var customerUsed = await _context.OrderDiscountCodeUsages
                        .CountAsync(u => u.DiscountCodeId == code.Id
                                         && u.CustomerId == customerId.Value
                                         && (!excludeOrderId.HasValue || u.OrderId != excludeOrderId.Value), ct);
                    if (customerUsed >= code.PerCustomerUsageLimit.Value)
                        return (false, "سقف استفاده این مشتری از کد تخفیف تکمیل شده است.");
                }
            }

            return (true, "ok");
        }

        private async Task<(bool Success, string Message, string? FullName, string? Mobile)> ResolveSpecificCustomerAsync(
            int restaurantId,
            int? specificCustomerId,
            CancellationToken ct)
        {
            if (!specificCustomerId.HasValue)
                return (true, "ok", null, null);

            var customer = await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.CustomerId == specificCustomerId.Value
                         && c.RestaurantId == restaurantId
                         && c.IsActive,
                    ct);

            if (customer == null)
                return (false, "مشتری انتخاب‌شده برای این رستوران یافت نشد.", null, null);

            return (true, "ok", customer.FullName, customer.Mobile);
        }

        private static DateTime NormalizeStartDate(DateTime value)
            => value.Date;

        private static DateTime NormalizeEndDate(DateTime value)
            => value.Date.AddDays(1).AddTicks(-1);

        /// <summary>
        /// True when request matches the stored code on every editable field except IsActive.
        /// Used to allow activate/deactivate on already-redeemed codes without permitting edits.
        /// </summary>
        private static bool IsSameEditablePayload(
            RestaurantDiscountCode entity,
            UpsertDiscountCodeRequest request,
            string discountType,
            string code)
        {
            if (!string.Equals(entity.Code, code, StringComparison.Ordinal))
                return false;
            if (!string.Equals(entity.Title, (request.Title ?? string.Empty).Trim(), StringComparison.Ordinal))
                return false;
            if (!string.Equals(entity.DiscountType, discountType, StringComparison.OrdinalIgnoreCase))
                return false;
            if (entity.DiscountValue != request.DiscountValue)
                return false;
            if (entity.MinOrderAmount != request.MinOrderAmount)
                return false;
            if (entity.MaxDiscountAmount != request.MaxDiscountAmount)
                return false;
            if (entity.StartDate != NormalizeStartDate(request.StartDate))
                return false;
            if (entity.EndDate != NormalizeEndDate(request.EndDate))
                return false;
            if (entity.UsageLimit != request.UsageLimit)
                return false;
            if (entity.PerCustomerUsageLimit != request.PerCustomerUsageLimit)
                return false;
            if (entity.SpecificCustomerId != request.SpecificCustomerId)
                return false;
            return true;
        }

        private static (bool Success, string Message, string? DiscountType) NormalizeRequest(UpsertDiscountCodeRequest request)
        {
            if (request == null)
                return (false, "اطلاعات نامعتبر است.", null);

            if (string.IsNullOrWhiteSpace(request.Code))
                return (false, "کد تخفیف الزامی است.", null);

            var normalizedCode = NormalizeCode(request.Code);
            if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedCode, @"^[A-Z0-9]+$"))
                return (false, "کد تخفیف فقط می‌تواند شامل حروف انگلیسی و اعداد باشد.", null);

            if (string.IsNullOrWhiteSpace(request.Title))
                return (false, "عنوان الزامی است.", null);

            var type = (request.DiscountType ?? string.Empty).Trim();
            if (string.Equals(type, "percentage", StringComparison.OrdinalIgnoreCase)
                || type == "درصد")
                type = PercentageType;
            else if (string.Equals(type, "fixedamount", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(type, "fixed", StringComparison.OrdinalIgnoreCase)
                     || type == "مبلغ ثابت")
                type = FixedAmountType;

            if (type != PercentageType && type != FixedAmountType)
                return (false, "نوع تخفیف باید Percentage یا FixedAmount باشد.", null);

            if (request.DiscountValue < 0)
                return (false, "مقدار تخفیف نامعتبر است.", null);

            if (type == PercentageType && request.DiscountValue > 100)
                return (false, "درصد تخفیف نمی‌تواند بیشتر از ۱۰۰ باشد.", null);

            if (request.EndDate < request.StartDate)
                return (false, "تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد.", null);

            if (request.MinOrderAmount.HasValue && request.MinOrderAmount.Value < 0)
                return (false, "حداقل مبلغ سفارش نامعتبر است.", null);

            if (request.MaxDiscountAmount.HasValue && request.MaxDiscountAmount.Value < 0)
                return (false, "سقف تخفیف نامعتبر است.", null);

            if (request.UsageLimit.HasValue && request.UsageLimit.Value <= 0)
                return (false, "سقف کل استفاده باید خالی یا بزرگ‌تر از صفر باشد.", null);

            if (request.PerCustomerUsageLimit.HasValue && request.PerCustomerUsageLimit.Value <= 0)
                return (false, "سقف استفاده هر مشتری باید خالی یا بزرگ‌تر از صفر باشد.", null);

            return (true, "ok", type);
        }

        public static decimal ComputeItemsSubtotal(Order order)
        {
            if (order.OrderItems == null || order.OrderItems.Count == 0)
                return 0;

            decimal sum = 0;
            foreach (var item in order.OrderItems)
            {
                var unit = FoodItemPricing.GetEffectiveSellingPrice(item.UnitPrice, item.UnitPriceWithDiscount);
                sum += unit * item.Quantity;
            }

            return RoundMoney(sum);
        }

        private static decimal RoundMoney(decimal value)
            => Math.Round(value, 0, MidpointRounding.AwayFromZero);

        private static DiscountCodeValidationResult FailValidation(string message) => new()
        {
            Success = false,
            Message = message
        };

        private static DiscountCodeDto MapDto(
            RestaurantDiscountCode c,
            string? customerName = null,
            string? customerMobile = null) => new()
        {
            Id = c.Id,
            RestaurantId = c.RestaurantId,
            Code = c.Code,
            Title = c.Title,
            DiscountType = c.DiscountType,
            DiscountValue = c.DiscountValue,
            MinOrderAmount = c.MinOrderAmount,
            MaxDiscountAmount = c.MaxDiscountAmount,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            UsageLimit = c.UsageLimit,
            UsedCount = c.UsedCount,
            PerCustomerUsageLimit = c.PerCustomerUsageLimit,
            SpecificCustomerId = c.SpecificCustomerId,
            SpecificCustomerName = customerName,
            SpecificCustomerMobile = customerMobile,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
    }
}
