using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.DiscountCodes
{
    public class OrderDiscountCodeUsage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DiscountCodeId { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        public int RestaurantId { get; set; }

        public int? CustomerId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ItemsSubtotalAtApply { get; set; }

        public DateTime UsedAt { get; set; }

        [ForeignKey(nameof(DiscountCodeId))]
        public virtual RestaurantDiscountCode? DiscountCode { get; set; }

        [ForeignKey(nameof(OrderId))]
        public virtual Order? Order { get; set; }
    }
}
