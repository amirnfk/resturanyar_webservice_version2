using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace resturanyar.Models
{
    public class Subscription
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int RestaurantId { get; set; }

        [Required]
        public int OwnerId { get; set; }

        [Required]
        public int SubscriptionPlanId { get; set; }

        [Required]
        [StringLength(20)]
        public string SubscriptionPeriod { get; set; } // "Monthly", "3Monthly", "6Monthly", "12Monthly"

        [Required]
        [StringLength(20)]
        public string Status { get; set; } // "Active", "Expired", "Canceled", "Pending", "Suspended"

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public DateTime PurchaseDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal PricePaid { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? DiscountApplied { get; set; }

        [StringLength(50)]
        public string PaymentMethod { get; set; }

        [StringLength(100)]
        public string TransactionId { get; set; }

        [Required]
        public bool IsPaid { get; set; }

        [StringLength(500)]
        public string CafeBazarPurchaseToken { get; set; }

        [StringLength(100)]
        public string CafeBazarOrderId { get; set; }

        [Required]
        public bool AutoRenew { get; set; }

        public DateTime? NextRenewalDate { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? CanceledAt { get; set; }

        [Required]
        public DateTime UpdatedAt { get; set; }

        // Navigation Properties
        [ForeignKey("RestaurantId")]
        public virtual Restaurant Restaurant { get; set; }

        [ForeignKey("OwnerId")]
        public virtual Owner Owner { get; set; }

        [ForeignKey("SubscriptionPlanId")]
        public virtual SubscriptionPlan SubscriptionPlan { get; set; }
    }
}
 
