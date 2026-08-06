using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.Inventory
{
    [Table("InventoryUnit")]
    public class InventoryUnit
    {
        [Key]
        public int UnitId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string NameFa { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Dimension { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,6)")]
        public decimal ToDimensionBaseFactor { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; }

        public bool AllowsCrossUnitConversion { get; set; } = true;
    }

    public static class InventoryUnitDimensions
    {
        public const string Mass = "Mass";
        public const string Volume = "Volume";
        public const string Count = "Count";
    }
}
