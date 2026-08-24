using System.ComponentModel.DataAnnotations;

namespace resturanyar.Models.DiscountCodes
{
    public class DiscountCodeDto
    {
        public int Id { get; set; }
        public int RestaurantId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string DiscountType { get; set; } = "Percentage";
        public decimal DiscountValue { get; set; }
        public decimal? MinOrderAmount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? UsageLimit { get; set; }
        public int UsedCount { get; set; }
        public int? PerCustomerUsageLimit { get; set; }
        public int? SpecificCustomerId { get; set; }
        public string? SpecificCustomerName { get; set; }
        public string? SpecificCustomerMobile { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UpsertDiscountCodeRequest
    {
        public int RestaurantId { get; set; }

        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(20)]
        public string DiscountType { get; set; } = "Percentage";

        public decimal DiscountValue { get; set; }

        public decimal? MinOrderAmount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? UsageLimit { get; set; }
        public int? PerCustomerUsageLimit { get; set; }
        /// <summary>Optional: restrict code to one restaurant customer.</summary>
        public int? SpecificCustomerId { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class ValidateDiscountCodeRequest
    {
        public int RestaurantId { get; set; }
        public string Code { get; set; } = string.Empty;
        public decimal ItemsSubtotal { get; set; }
        public int? CustomerId { get; set; }
        /// <summary>When re-validating an order that already holds this code, exclude that order from usage counts.</summary>
        public int? ExcludeOrderId { get; set; }
    }

    public class DiscountCodeValidationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? DiscountCodeId { get; set; }
        public string? Code { get; set; }
        public string? Title { get; set; }
        public string? DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal ItemsSubtotal { get; set; }
        public decimal FinalSubtotalAfterDiscount { get; set; }
    }
}
