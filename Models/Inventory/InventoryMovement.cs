using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.Inventory
{
    [Table("InventoryMovement")]
    public class InventoryMovement
    {
        [Key]
        public int MovementId { get; set; }

        public int RestaurantId { get; set; }

        public int InventoryItemId { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal DeltaQuantity { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal QuantityAfter { get; set; }

        [Required]
        [MaxLength(30)]
        public string Reason { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? UnitPrice { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? CreatedByOwnerId { get; set; }

        [ForeignKey(nameof(InventoryItemId))]
        public InventoryItem? Item { get; set; }
    }
}
