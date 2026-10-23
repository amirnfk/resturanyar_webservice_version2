using System.ComponentModel.DataAnnotations;

namespace resturanyar.Models.CustomerAccounts
{
    public class CustomerAccountSummaryDto
    {
        public int AccountId { get; set; }
        public int RestaurantId { get; set; }
        public int CustomerId { get; set; }
        public string? CustomerFullName { get; set; }
        public string CustomerMobile { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }
        public DateTime UpdatedAt { get; set; }

        /// <summary>Sum of positive ledger effects (debts + increasing adjustments).</summary>
        public decimal TotalDebt { get; set; }

        /// <summary>Sum of negative ledger effects as a positive number (payments + decreasing adjustments).</summary>
        public decimal TotalPayment { get; set; }

        public int TransactionCount { get; set; }

        public DateTime? LastTransactionAt { get; set; }
    }

    public class CustomerAccountTransactionDto
    {
        public int TransactionId { get; set; }
        public int AccountId { get; set; }
        public int CustomerId { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal SignedAmount { get; set; }
        public decimal BalanceAfter { get; set; }
        public string? Note { get; set; }
        public int? OrderId { get; set; }
        public decimal? PaidAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CreatedByOwnerId { get; set; }
        public int? CreatedByStaffUserId { get; set; }
    }

    public class CustomerAccountTransactionListDto
    {
        public IReadOnlyList<CustomerAccountTransactionDto> Items { get; set; } = Array.Empty<CustomerAccountTransactionDto>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class AddCustomerAccountTransactionRequest
    {
        [Required]
        public int RestaurantId { get; set; }

        [Required]
        public int CustomerId { get; set; }

        /// <summary>Debt | Payment | Adjustment</summary>
        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = string.Empty;

        /// <summary>Absolute amount; must be &gt; 0.</summary>
        [Required]
        public decimal Amount { get; set; }

        /// <summary>
        /// For Adjustment only: +1 increases debt, -1 decreases debt.
        /// Ignored for Debt/Payment.
        /// </summary>
        public int? AdjustmentSign { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        /// <summary>Optional; set only by order-settlement flow (not manual UI).</summary>
        public int? OrderId { get; set; }

        /// <summary>Cash collected with order debt; optional.</summary>
        public decimal? PaidAmount { get; set; }
    }

    public class SettleOrderPaymentRequest
    {
        [Required]
        public int CurrentStatusId { get; set; }

        /// <summary>
        /// Cash applied to this order only when IncludePriorBalancePayment is false.
        /// When prior settlement is included, prefer CashReceived (total cash from customer);
        /// PaidAmount is still accepted as a fallback for CashReceived.
        /// </summary>
        [Required]
        public decimal PaidAmount { get; set; }

        /// <summary>
        /// Total cash received from the customer. Used when IncludePriorBalancePayment is true.
        /// Split server-side: order first, remainder to prior balance (capped).
        /// </summary>
        public decimal? CashReceived { get; set; }

        /// <summary>
        /// When true, cash beyond order total (up to prior ledger balance) is written as a Payment.
        /// Requires a valid CustomerId.
        /// </summary>
        public bool IncludePriorBalancePayment { get; set; }

        /// <summary>Required when order debt &gt; 0 or IncludePriorBalancePayment is true.</summary>
        public int? CustomerId { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }
    }

    public class OrderSettlementPreviewDto
    {
        public int OrderId { get; set; }
        public int RestaurantId { get; set; }
        public int StatusId { get; set; }
        public decimal OrderTotal { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerFullName { get; set; }
        public string? CustomerMobile { get; set; }
        public decimal? CustomerAccountBalance { get; set; }
        /// <summary>True when SettlementType is already set (payment already registered).</summary>
        public bool AlreadySettled { get; set; }
        public string? SettlementType { get; set; }
    }

    public class SettleOrderPaymentResultDto
    {
        public int OrderId { get; set; }
        public int NewStatusId { get; set; }
        public decimal OrderTotal { get; set; }
        /// <summary>Cash applied to this order only (not prior balance).</summary>
        public decimal PaidAmount { get; set; }
        public decimal DebtAmount { get; set; }
        public string SettlementType { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public int? DebtTransactionId { get; set; }
        public int PreviousStatusId { get; set; }
        public int RestaurantId { get; set; }
        /// <summary>True when this call did not write again because settlement already existed.</summary>
        public bool AlreadySettled { get; set; }

        /// <summary>Cash applied to prior club balance as Payment (0 when not included).</summary>
        public decimal PriorBalancePaidAmount { get; set; }

        /// <summary>Prior balance remaining after this Payment (before new order debt effect on display is separate).</summary>
        public decimal PriorBalanceRemaining { get; set; }

        public int? PriorPaymentTransactionId { get; set; }
    }

    public class CustomerAccountLookupRequest
    {
        [Required]
        public int RestaurantId { get; set; }

        [Required]
        public int CustomerId { get; set; }
    }

    public class CustomerAccountPagedLookupRequest : CustomerAccountLookupRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    /// <summary>Closed-order purchase row for customer account page (read-only; not ledger).</summary>
    public class CustomerPurchaseHistoryItemDto
    {
        public int OrderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedAtShamsi { get; set; }
        public byte OrderType { get; set; }
        public string OrderTypeLabel { get; set; } = string.Empty;
        public string? TableNumber { get; set; }
        public int ItemCount { get; set; }
        public decimal Amount { get; set; }
        public bool HasReceipt { get; set; }
        public string? Description { get; set; }
    }

    public class CustomerPurchaseHistoryListDto
    {
        public IReadOnlyList<CustomerPurchaseHistoryItemDto> Items { get; set; } = Array.Empty<CustomerPurchaseHistoryItemDto>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public decimal TotalSpent { get; set; }
    }
}
