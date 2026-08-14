using resturanyar.Models;
using resturanyar.Models.Receipt;

using System.Text.Json.Serialization;

namespace resturanyar.Services.Fulfillment
{
    public class AssignDriverRequest
    {
        public int DriverUserId { get; set; }
    }

    public class DeliveryFailedRequest
    {
        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;

        [JsonPropertyName("reasonCode")]
        public string? ReasonCode { get; set; }
    }

    public class DriverListItemDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public interface IDeliveryCourierService
    {
        Task<(bool Ok, int StatusCode, string Message, List<DriverListItemDto>? Drivers)> ListDriversAsync(
            int restaurantId, CancellationToken ct = default);

        Task<(bool Ok, int StatusCode, string Message)> AssignDriverAsync(
            int orderId, int restaurantId, int driverUserId, CancellationToken ct = default);

        Task<(bool Ok, int StatusCode, string Message)> UnassignDriverAsync(
            int orderId, int restaurantId, CancellationToken ct = default);

        Task<(bool Ok, int StatusCode, string Message, int NewStatusId)> ReportDeliveryFailedAsync(
            int orderId, int restaurantId, int courierUserId, string reason, string? reasonCode, CancellationToken ct = default);

        /// <summary>
        /// Courier (delivery-only staff) may only move assigned Delivery orders from status 5 to 6.
        /// Returns null when the caller is not a delivery-only courier (no restriction applied here).
        /// </summary>
        Task<(bool Allowed, string? Message)> ValidateCourierStatusChangeAsync(
            int orderId,
            int restaurantId,
            int staffUserId,
            bool isDeliveryOnlyStaff,
            int currentStatusId,
            int newStatusId,
            CancellationToken ct = default);

        void ClearFailureFields(OrderFulfillment fulfillment);

        int? GetNextRoleId(int statusId, OrderTypeKind orderType);

        Task<bool> IsEligibleDriverAsync(int restaurantId, int driverUserId, CancellationToken ct = default);

        Task<bool> TryAutoAssignDefaultDriverAsync(
            int orderId, int restaurantId, int previousStatusId, int newStatusId,
            CancellationToken ct = default);

        Task<(bool Valid, string Message)> ValidateFulfillmentDriverSettingsAsync(
            int restaurantId, bool enableDelivery, bool autoAssignDeliveryDriver,
            int? defaultDeliveryDriverUserId, CancellationToken ct = default);
    }
}
