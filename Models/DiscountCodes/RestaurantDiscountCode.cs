using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.DiscountCodes
{
    public class RestaurantDiscountCode
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int RestaurantId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        /// <summary>Percentage | FixedAmount</summary>
        [Required]
        [MaxLength(20)]
        public string DiscountType { get; set; } = "Percentage";

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinOrderAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxDiscountAmount { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public int? UsageLimit { get; set; }

        public int UsedCount { get; set; }

        public int? PerCustomerUsageLimit { get; set; }

        /// <summary>When set, only this restaurant customer may redeem the code.</summary>
        public int? SpecificCustomerId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        [ForeignKey(nameof(SpecificCustomerId))]
        public virtual resturanyar.Models.CustomerModels.Customer? SpecificCustomer { get; set; }

        public virtual ICollection<OrderDiscountCodeUsage> Usages { get; set; } = new List<OrderDiscountCodeUsage>();
    }
}
