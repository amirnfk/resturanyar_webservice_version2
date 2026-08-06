using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.Inventory
{
    [Table("InventoryOrderConsumption")]
    public class InventoryOrderConsumption
    {
        [Key]
        public int ConsumptionId { get; set; }

        public int RestaurantId { get; set; }

        public int OrderId { get; set; }

        public DateTime DeductedAt { get; set; } = DateTime.UtcNow;

        public bool IsReversed { get; set; }

        public DateTime? ReversedAt { get; set; }
    }
}
