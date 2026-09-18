using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Models.Receipt;
using Resturanyar.Data;

namespace resturanyar.Helpers
{
    /// <summary>
    /// Shared validation for full order content updates (items/customer/address).
    /// </summary>
    public static class OrderEditGuard
    {
        public static readonly HashSet<int> EditableStatusIds = new() { 1, 2, 3, 4, 5, 12 };
        public static readonly HashSet<int> LockedStatusIds = new() { 6, 7, 8, 9, 10, 11 };

        public sealed class ValidationResult
        {
            public bool Success { get; init; }
            public IActionResult? Error { get; init; }
            public List<OrderItemDto> NormalizedItems { get; init; } = new();
            public int ResolvedStatusId { get; init; }
        }

        public static async Task<ValidationResult> ValidateAsync(
            AppDbContext context,
            Order order,
            UpdateOrderRequest request,
            CancellationToken ct = default)
        {
            if (request == null)
            {
                return Fail(new BadRequestObjectResult(new { success = false, message = "درخواست نامعتبر است." }));
            }

            if (request.ExpectedStatusId.HasValue && order.StatusId != request.ExpectedStatusId.Value)
            {
                return Fail(new ConflictObjectResult(new
                {
                    success = false,
                    message = "وضعیت سفارش توسط کاربر دیگری تغییر کرده است. صفحه را تازه کنید."
                }));
            }

            if (LockedStatusIds.Contains(order.StatusId) || !EditableStatusIds.Contains(order.StatusId))
            {
                return Fail(new BadRequestObjectResult(new
                {
                    success = false,
                    message = "این سفارش در وضعیت فعلی قابل ویرایش نیست."
                }));
            }

            var hasIssuedReceipt = await context.OrderReceiptSnapshots
                .AsNoTracking()
                .AnyAsync(s => s.OrderId == order.OrderId, ct);

            if (hasIssuedReceipt)
            {
                return Fail(new BadRequestObjectResult(new
                {
                    success = false,
                    message = "برای سفارش دارای فاکتور صادرشده، ویرایش آیتم‌ها مجاز نیست. از ویرایش فاکتور استفاده کنید."
                }));
            }

            if (request.RestaurantId != 0 && request.RestaurantId != order.RestaurantId)
            {
                return Fail(new BadRequestObjectResult(new
                {
                    success = false,
                    message = "رستوران سفارش قابل تغییر نیست."
                }));
            }

            var items = (request.Items ?? new List<OrderItemDto>())
                .Where(i => i != null && i.FoodItemId > 0)
                .Select(i => new OrderItemDto
                {
                    FoodItemId = i.FoodItemId,
                    Quantity = i.Quantity
                })
                .Where(i => i.Quantity > 0)
                .GroupBy(i => i.FoodItemId)
                .Select(g => new OrderItemDto
                {
                    FoodItemId = g.Key,
                    Quantity = Math.Min(g.Sum(x => x.Quantity), 999)
                })
                .ToList();

            if (items.Count == 0)
            {
                return Fail(new BadRequestObjectResult(new
                {
                    success = false,
                    message = "سفارش باید حداقل یک آیتم با تعداد معتبر داشته باشد."
                }));
            }

            if (items.Any(i => i.Quantity < 1))
            {
                return Fail(new BadRequestObjectResult(new
                {
                    success = false,
                    message = "تعداد آیتم‌ها باید حداقل ۱ باشد."
                }));
            }

            var foodIds = items.Select(i => i.FoodItemId).Distinct().ToList();
            var foods = await context.FoodItems
                .AsNoTracking()
                .Where(f => foodIds.Contains(f.FoodItemId))
                .Select(f => new { f.FoodItemId, f.RestaurantId, f.IsActive })
                .ToListAsync(ct);

            if (foods.Count != foodIds.Count)
            {
                return Fail(new BadRequestObjectResult(new
                {
                    success = false,
                    message = "یکی از غذاهای انتخاب‌شده یافت نشد."
                }));
            }

            if (foods.Any(f => f.RestaurantId != order.RestaurantId))
            {
                return Fail(new BadRequestObjectResult(new
                {
                    success = false,
                    message = "غذای انتخاب‌شده متعلق به این رستوران نیست."
                }));
            }

            if (order.OrderType == OrderTypeKind.Delivery)
            {
                if (!request.CustomerId.HasValue || request.CustomerId.Value <= 0)
                {
                    return Fail(new BadRequestObjectResult(new
                    {
                        success = false,
                        message = "برای سفارش ارسال، انتخاب مشتری الزامی است."
                    }));
                }

                var hasAddress = request.CustomerAddressId.HasValue && request.CustomerAddressId.Value > 0
                    || !string.IsNullOrWhiteSpace(request.AddressText);

                if (!hasAddress)
                {
                    return Fail(new BadRequestObjectResult(new
                    {
                        success = false,
                        message = "برای سفارش ارسال، آدرس الزامی است."
                    }));
                }
            }

            if (order.OrderType == OrderTypeKind.DineIn && string.IsNullOrWhiteSpace(request.TableNumber))
            {
                return Fail(new BadRequestObjectResult(new
                {
                    success = false,
                    message = "برای سفارش حضوری، انتخاب میز الزامی است."
                }));
            }

            // Status 12 (waiting for edit) returns to approved (3) after save, matching Android.
            // Content edits must not regress kitchen progress for other statuses.
            var resolvedStatusId = order.StatusId == 12 ? 3 : order.StatusId;

            return new ValidationResult
            {
                Success = true,
                NormalizedItems = items,
                ResolvedStatusId = resolvedStatusId
            };
        }

        private static ValidationResult Fail(IActionResult error) => new()
        {
            Success = false,
            Error = error
        };
    }
}
