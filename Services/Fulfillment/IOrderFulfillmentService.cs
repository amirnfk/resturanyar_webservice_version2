using resturanyar.Models;
using resturanyar.Models.Receipt;

namespace resturanyar.Services.Fulfillment
{
    public class FulfillmentValidationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int? StatusCode { get; set; }
        public OrderTypeKind ResolvedOrderType { get; set; } = OrderTypeKind.DineIn;
        public string ResolvedTableNumber { get; set; } = string.Empty;
        public OrderFulfillment? Fulfillment { get; set; }

        public static FulfillmentValidationResult Ok(
            OrderTypeKind orderType,
            string tableNumber,
            OrderFulfillment? fulfillment = null) =>
            new()
            {
                Success = true,
                ResolvedOrderType = orderType,
                ResolvedTableNumber = tableNumber,
                Fulfillment = fulfillment
            };

        public static FulfillmentValidationResult Fail(string message, int statusCode = 400) =>
            new()
            {
                Success = false,
                ErrorMessage = message,
                StatusCode = statusCode
            };
    }

    public interface IOrderFulfillmentService
    {
        bool IsGlobalEnabled();

        Task<FulfillmentValidationResult> ValidateAndPrepareAsync(
            CreateOrderRequest request,
            bool restaurantEnableTakeaway,
            bool restaurantEnableDelivery,
            CancellationToken cancellationToken = default);

        Task AttachFulfillmentAsync(Order order, OrderFulfillment fulfillment, CancellationToken cancellationToken = default);

        Task<bool> TryUpdateFulfillmentSnapshotsAsync(
            int orderId,
            int restaurantId,
            int? customerId,
            int? customerAddressId,
            string? addressText,
            CancellationToken cancellationToken = default);
    }
}
