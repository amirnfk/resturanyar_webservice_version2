using resturanyar.Models.CustomerAccounts;

namespace resturanyar.Services.Orders
{
    public interface IOrderSettlementService
    {
        Task<OrderSettlementPreviewDto> GetPreviewAsync(
            int orderId,
            int restaurantId,
            CancellationToken ct = default);

        /// <summary>
        /// Atomically records optional customer debt and moves order 7 → 8.
        /// Caller is responsible for receipt auto-issue, inventory, and SignalR.
        /// </summary>
        Task<SettleOrderPaymentResultDto> SettlePaymentAsync(
            int orderId,
            int restaurantId,
            SettleOrderPaymentRequest request,
            int? ownerId,
            int? staffUserId,
            CancellationToken ct = default);
    }
}
