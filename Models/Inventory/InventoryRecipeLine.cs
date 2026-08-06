using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.Inventory
{
    [Table("InventoryRecipeLine")]
    public class InventoryRecipeLine
    {
        [Key]
        public int RecipeLineId { get; set; }

        public int RecipeId { get; set; }

        public int InventoryItemId { get; set; }

        public int UnitId { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal Quantity { get; set; }

        [ForeignKey(nameof(RecipeId))]
        public InventoryRecipe? Recipe { get; set; }

        [ForeignKey(nameof(InventoryItemId))]
        public InventoryItem? InventoryItem { get; set; }

        [ForeignKey(nameof(UnitId))]
        public InventoryUnit? Unit { get; set; }
    }
}
