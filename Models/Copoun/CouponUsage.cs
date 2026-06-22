using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.Copoun
{
    public class CouponUsage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CouponId { get; set; }

        [Required]
        public int SubscriptionId { get; set; }

        [Required]
        public int OwnerId { get; set; }

        [Required]
        public int RestaurantId { get; set; }

        [Required]
        public DateTime UsedAt { get; set; } = DateTime.Now;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AppliedPrice { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Success";   

        [MaxLength(100)]
        public string TransactionId { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(CouponId))]
        public virtual Coupon Coupon { get; set; }

        [ForeignKey(nameof(SubscriptionId))]
        public virtual Subscription Subscription { get; set; }

        [ForeignKey(nameof(OwnerId))]
        public virtual Owner Owner { get; set; }

        [ForeignKey(nameof(RestaurantId))]
        public virtual Restaurant Restaurant { get; set; }
    }
}
