using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Models.Receipt;
using resturanyar.Utility;
using Resturanyar.Data;

namespace resturanyar.Services.Fulfillment
{
    public class DeliveryCourierService : IDeliveryCourierService
    {
        private readonly AppDbContext _db;

        public DeliveryCourierService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<(bool Ok, int StatusCode, string Message, List<DriverListItemDto>? Drivers)> ListDriversAsync(
            int restaurantId, CancellationToken ct = default)
        {
            var drivers = await _db.Users.AsNoTracking()
                .Where(u => u.restaurant_id == restaurantId
                            && (u.role_id == 5 || u.delivery_management_permission))
                .OrderBy(u => u.name)
                .Select(u => new DriverListItemDto
                {
                    UserId = u.user_id,
                    Name = u.name
                })
                .ToListAsync(ct);

            return (true, 200, "ok", drivers);
        }

        public async Task<(bool Ok, int StatusCode, string Message)> AssignDriverAsync(
            int orderId, int restaurantId, int driverUserId, CancellationToken ct = default)
        {
            var order = await _db.Orders
                .Include(o => o.Fulfillment)
                .FirstOrDefaultAsync(o => o.OrderId == orderId, ct);

            if (order == null || order.RestaurantId != restaurantId)
                return (false, 404, "سفارش یافت نشد.");

            if (order.OrderType != OrderTypeKind.Delivery)
                return (false, 400, "تخصیص پیک فقط برای سفارش‌های ارسال مجاز است.");

            if (!CanChangeCourierAssignment(order.StatusId))
                return (false, 400, "پس از تحویل سفارش، تغییر پیک مجاز نیست.");

            var driver = await _db.Users.FirstOrDefaultAsync(
                u => u.user_id == driverUserId && u.restaurant_id == restaurantId, ct);

            if (driver == null || !(driver.role_id == 5 || driver.delivery_management_permission))
                return (false, 400, "پیک معتبر برای این رستوران یافت نشد.");

            if (order.Fulfillment == null)
            {
                order.Fulfillment = new OrderFulfillment
                {
                    OrderId = order.OrderId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.OrderFulfillments.Add(order.Fulfillment);
            }

            order.Fulfillment.AssignedDriverUserId = driverUserId;
            order.Fulfillment.AssignedAt = DateTime.UtcNow;
            ClearFailureFields(order.Fulfillment);
            order.Fulfillment.UpdatedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.Now;

            UpsertOrderUpdate(order.OrderId, order.RestaurantId, targetRoleId: 5);

            await _db.SaveChangesAsync(ct);
            return (true, 200, "پیک با موفقیت تخصیص داده شد.");
        }

        public async Task<(bool Ok, int StatusCode, string Message)> UnassignDriverAsync(
            int orderId, int restaurantId, CancellationToken ct = default)
        {
            var order = await _db.Orders
                .Include(o => o.Fulfillment)
                .FirstOrDefaultAsync(o => o.OrderId == orderId, ct);

            if (order == null || order.RestaurantId != restaurantId)
                return (false, 404, "سفارش یافت نشد.");

            if (order.OrderType != OrderTypeKind.Delivery)
                return (false, 400, "حذف تخصیص پیک فقط برای سفارش‌های ارسال مجاز است.");

            if (!CanChangeCourierAssignment(order.StatusId))
                return (false, 400, "پس از تحویل سفارش، تغییر پیک مجاز نیست.");

            if (order.Fulfillment == null)
                return (true, 200, "پیکی تخصیص داده نشده است.");

            order.Fulfillment.AssignedDriverUserId = null;
            order.Fulfillment.AssignedAt = null;
            order.Fulfillment.UpdatedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.Now;

            UpsertOrderUpdate(order.OrderId, order.RestaurantId, targetRoleId: 5);

            await _db.SaveChangesAsync(ct);
            return (true, 200, "تخصیص پیک برداشته شد.");
        }

        public async Task<(bool Ok, int StatusCode, string Message, int NewStatusId)> ReportDeliveryFailedAsync(
            int orderId, int restaurantId, int courierUserId, string reason, string? reasonCode, CancellationToken ct = default)
        {
            reason = (reason ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(reason))
                return (false, 400, "دلیل ناموفق بودن تحویل الزامی است.", 0);
            if (reason.Length > 500)
                reason = reason.Substring(0, 500);

            var order = await _db.Orders
                .Include(o => o.Fulfillment)
                .AsTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId, ct);

            if (order == null || order.RestaurantId != restaurantId)
                return (false, 404, "سفارش یافت نشد.", 0);

            if (order.OrderType != OrderTypeKind.Delivery)
                return (false, 400, "گزارش ناموفق فقط برای سفارش ارسال مجاز است.", 0);

            if (order.StatusId != 5)
                return (false, 400, "فقط سفارش‌های آماده ارسال قابل گزارش ناموفق هستند.", 0);

            if (order.Fulfillment == null || order.Fulfillment.AssignedDriverUserId != courierUserId)
                return (false, 403, "این سفارش به شما تخصیص داده نشده است.", 0);

            int newStatusId = ResolveFailedDeliveryStatusId(reasonCode, reason);
            var now = DateTime.Now;

            order.StatusId = newStatusId;
            order.UpdatedAt = now;
            order.UpdatedAtShamsi = DateHelper.ToShamsi(now);
            _db.Entry(order).Property(o => o.StatusId).IsModified = true;

            order.Fulfillment.DeliveryFailureReason = reason;
            order.Fulfillment.DeliveryFailedAt = DateTime.UtcNow;
            order.Fulfillment.AssignedDriverUserId = null;
            order.Fulfillment.AssignedAt = null;
            order.Fulfillment.UpdatedAt = DateTime.UtcNow;

            UpsertOrderUpdate(order.OrderId, order.RestaurantId, targetRoleId: 5);
            int? nextRoleId = GetNextRoleId(newStatusId, order.OrderType);
            if (nextRoleId.HasValue)
                UpsertOrderUpdate(order.OrderId, order.RestaurantId, nextRoleId.Value);

            await _db.SaveChangesAsync(ct);

            await _db.Orders
                .Where(o => o.OrderId == orderId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.StatusId, newStatusId)
                    .SetProperty(o => o.UpdatedAt, now), ct);

            string message = newStatusId == 9
                ? "سفارش به دلیل مشتری لغو شد."
                : "سفارش توسط رستوران لغو شد.";
            return (true, 200, message, newStatusId);
        }

        /// <summary>
        /// Customer-side delivery failures → 9. Restaurant/ops and custom → 10.
        /// </summary>
        public static int ResolveFailedDeliveryStatusId(string? reasonCode, string? reason = null)
        {
            switch ((reasonCode ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "customer_no_answer":
                case "customer_not_home":
                case "wrong_address":
                case "customer_refused":
                    return 9;
                case "vehicle_issue":
                case "custom":
                    return 10;
            }

            return (reason ?? string.Empty).Trim() switch
            {
                "مشتری پاسخگو نبود" => 9,
                "مشتری در آدرس حضور نداشت" => 9,
                "آدرس اشتباه یا پیدا نشد" => 9,
                "مشتری سفارش را تحویل نگرفت" => 9,
                "مشکل در مسیر یا وسیله نقلیه" => 10,
                _ => 10
            };
        }

        public async Task<(bool Allowed, string? Message)> ValidateCourierStatusChangeAsync(
            int orderId,
            int restaurantId,
            int staffUserId,
            bool isDeliveryOnlyStaff,
            int currentStatusId,
            int newStatusId,
            CancellationToken ct = default)
        {
            if (!isDeliveryOnlyStaff)
                return (true, null);

            if (currentStatusId != 5 || newStatusId != 6)
                return (false, "پیک فقط می‌تواند سفارش آماده ارسال را به تحویل‌شده تغییر دهد.");

            var order = await _db.Orders
                .Include(o => o.Fulfillment)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId, ct);

            if (order == null || order.RestaurantId != restaurantId)
                return (false, "سفارش یافت نشد.");

            if (order.OrderType != OrderTypeKind.Delivery)
                return (false, "پیک فقط به سفارش‌های ارسال دسترسی دارد.");

            if (order.Fulfillment?.AssignedDriverUserId != staffUserId)
                return (false, "این سفارش به شما تخصیص داده نشده است.");

            return (true, null);
        }

        public void ClearFailureFields(OrderFulfillment fulfillment)
        {
            fulfillment.DeliveryFailureReason = null;
            fulfillment.DeliveryFailedAt = null;
        }

        /// <summary>
        /// Courier may be assigned/changed only before handoff (status 1–5).
        /// After status 6+ the assignment is historical/read-only.
        /// </summary>
        public static bool CanChangeCourierAssignment(int statusId) => statusId >= 1 && statusId <= 5;

        public int? GetNextRoleId(int statusId, OrderTypeKind orderType)
        {
            if (statusId == 5 && orderType == OrderTypeKind.Delivery)
                return null;

            return statusId switch
            {
                2 => 3,
                3 => 3,
                4 => 3,
                5 => 2,
                6 => 4,
                7 => 4,
                8 => 4,
                9 => 4,
                10 => 4,
                11 => 4,
                12 => 3,
                99 => 3,
                _ => null
            };
        }

        public async Task<bool> IsEligibleDriverAsync(
            int restaurantId, int driverUserId, CancellationToken ct = default)
        {
            return await _db.Users.AsNoTracking()
                .AnyAsync(u => u.user_id == driverUserId
                               && u.restaurant_id == restaurantId
                               && (u.role_id == 5 || u.delivery_management_permission), ct);
        }

        public async Task<bool> TryAutoAssignDefaultDriverAsync(
            int orderId, int restaurantId, int previousStatusId, int newStatusId,
            CancellationToken ct = default)
        {
            if (newStatusId != 5 || previousStatusId == 5)
                return false;

            var order = await _db.Orders.AsNoTracking()
                .Include(o => o.Fulfillment)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.RestaurantId == restaurantId, ct);

            if (order == null || order.OrderType != OrderTypeKind.Delivery)
                return false;

            if (order.Fulfillment?.AssignedDriverUserId != null)
                return false;

            var restaurant = await _db.Restaurants.AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId, ct);

            if (restaurant == null || !restaurant.AutoAssignDeliveryDriver)
                return false;

            if (!restaurant.DefaultDeliveryDriverUserId.HasValue)
                return false;

            var driverUserId = restaurant.DefaultDeliveryDriverUserId.Value;
            if (!await IsEligibleDriverAsync(restaurantId, driverUserId, ct))
                return false;

            var result = await AssignDriverAsync(orderId, restaurantId, driverUserId, ct);
            return result.Ok;
        }

        public async Task<(bool Valid, string Message)> ValidateFulfillmentDriverSettingsAsync(
            int restaurantId, bool enableDelivery, bool autoAssignDeliveryDriver,
            int? defaultDeliveryDriverUserId, CancellationToken ct = default)
        {
            if (!autoAssignDeliveryDriver || !enableDelivery)
                return (true, string.Empty);

            if (!defaultDeliveryDriverUserId.HasValue || defaultDeliveryDriverUserId.Value <= 0)
                return (false, "برای تخصیص خودکار، پیک پیش‌فرض را انتخاب کنید.");

            if (!await IsEligibleDriverAsync(restaurantId, defaultDeliveryDriverUserId.Value, ct))
                return (false, "پیک پیش‌فرض انتخاب‌شده معتبر نیست.");

            return (true, string.Empty);
        }

        private void UpsertOrderUpdate(int orderId, int restaurantId, int targetRoleId)
        {
            var existing = _db.OrderUpdates
                .FirstOrDefault(u => u.OrderId == orderId && u.TargetRoleId == targetRoleId);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (existing != null)
                existing.UpdateTime = now;
            else
            {
                _db.OrderUpdates.Add(new OrderUpdate
                {
                    OrderId = orderId,
                    RestaurantId = restaurantId,
                    TargetRoleId = targetRoleId,
                    UpdateTime = now
                });
            }
        }
    }
}
