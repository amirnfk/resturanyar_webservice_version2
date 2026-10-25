namespace resturanyar.Services.Orders
{
    /// <summary>
    /// On cancel (status 9/10), reverses any remaining order نسیه on the customer ledger.
    /// </summary>
    public interface IOrderCancellationDebtService
    {
        /// <summary>
        /// No-op unless <paramref name="newStatusId"/> is cancel and previous was not.
        /// Idempotent: skips if a cancel-reversal Adjustment already exists for the order.
        /// </summary>
        Task HandleStatusChangeAsync(
            int orderId,
            int restaurantId,
            int previousStatusId,
            int newStatusId,
            int? ownerId = null,
            int? staffUserId = null,
            CancellationToken ct = default);
    }
}
