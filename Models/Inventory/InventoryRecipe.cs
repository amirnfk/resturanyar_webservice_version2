using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.Inventory
{
    [Table("InventoryRecipe")]
    public class InventoryRecipe
    {
        [Key]
        public int RecipeId { get; set; }

        public int RestaurantId { get; set; }

        public int FoodItemId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<InventoryRecipeLine> Lines { get; set; } = new();
    }
}
