using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.Inventory
{
    [Table("InventoryItem")]
    public class InventoryItem
    {
        [Key]
        public int InventoryItemId { get; set; }

        public int RestaurantId { get; set; }

        public int? CategoryId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Legacy string unit (nullable after migration). Prefer BaseUnitId.</summary>
        [MaxLength(20)]
        public string? Unit { get; set; }

        public int BaseUnitId { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal CurrentQuantity { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal MinimumQuantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? LastPurchasePrice { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(CategoryId))]
        public InventoryCategory? Category { get; set; }

        [ForeignKey(nameof(BaseUnitId))]
        public InventoryUnit? BaseUnit { get; set; }
    }
}
