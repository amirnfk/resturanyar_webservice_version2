namespace resturanyar.Services.Inventory
{
    public interface IOrderInventoryConsumptionService
    {
        /// <summary>
        /// After a successful status change: deduct when entering auto-deduct status; reverse on cancel.
        /// Never throws to callers that wrap it; returns false on no-op/failure.
        /// </summary>
        Task HandleStatusChangeAsync(int orderId, int restaurantId, int previousStatusId, int newStatusId, CancellationToken ct = default);

        Task<bool> TryDeductForOrderAsync(int orderId, int restaurantId, CancellationToken ct = default);
        Task<bool> TryReverseForOrderAsync(int orderId, int restaurantId, CancellationToken ct = default);
    }
}
