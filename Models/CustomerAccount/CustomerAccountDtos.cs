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
