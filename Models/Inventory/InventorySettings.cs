using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.Inventory
{
    [Table("InventorySettings")]
    public class InventorySettings
    {
        [Key]
        public int RestaurantId { get; set; }

        public bool IsEnabled { get; set; }

        /// <summary>Order status id that triggers auto-deduction (default Preparing = 4).</summary>
        public int AutoDeductStatusId { get; set; } = 4;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
