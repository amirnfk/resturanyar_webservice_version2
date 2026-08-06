using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.Inventory
{
    [Table("InventoryCategory")]
    public class InventoryCategory
    {
        [Key]
        public int InventoryCategoryId { get; set; }

        public int RestaurantId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
