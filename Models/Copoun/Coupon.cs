using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.Copoun
{
    public class Coupon
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Code { get; set; }  // کد تخفیف

        [Required]
        [MaxLength(20)]
        public string DiscountType { get; set; }  // 'Percentage' یا 'FixedAmount'

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; }  // مقدار تخفیف

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxDiscountAmount { get; set; }  // سقف تخفیف (درصدی)

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinPurchaseAmount { get; set; }  // حداقل مبلغ خرید

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public bool IsActive { get; set; } = true;

        public int? UsageLimit { get; set; }  // تعداد کل استفاده (نامحدود = null)

        [Required]
        public int UsedCount { get; set; } = 0;

        [Required]
        public int LimitPerOwner { get; set; } = 1;  // سقف استفاده برای هر مالک

        public int? SpecificOwnerId { get; set; }  // اگر فقط برای یک مالک خاص باشد

        public int? SpecificRestaurantId { get; set; }  // اگر فقط برای یک رستوران خاص باشد

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // ========== Navigation Properties ==========
        [ForeignKey(nameof(SpecificOwnerId))]
        public virtual Owner SpecificOwner { get; set; }

        [ForeignKey(nameof(SpecificRestaurantId))]
        public virtual Restaurant SpecificRestaurant { get; set; }

        // لیست استفاده‌ها (اختیاری)
        public virtual ICollection<CouponUsage> Usages { get; set; }
    }
}
