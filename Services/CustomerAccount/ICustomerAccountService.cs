using resturanyar.Models.CustomerAccounts;

namespace resturanyar.Services.CustomerAccounts
{
    public interface ICustomerAccountService
    {
        Task<CustomerAccountSummaryDto> GetAccountSummaryAsync(
            int restaurantId,
            int customerId,
            CancellationToken ct = default);

        Task<CustomerAccountTransactionListDto> ListTransactionsAsync(
            int restaurantId,
            int customerId,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default);

        Task<CustomerAccountTransactionDto> AddTransactionAsync(
            AddCustomerAccountTransactionRequest request,
            int? ownerId,
            int? staffUserId,
            CancellationToken ct = default);

        Task<CustomerPurchaseHistoryListDto> ListPurchaseHistoryAsync(
            int restaurantId,
            int customerId,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default);
    }
}
