using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.Receipt
{
    public class RestaurantChargeDefinition
    {
        public int Id { get; set; }
        public int RestaurantId { get; set; }

        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        public ChargeCategory ChargeCategory { get; set; }
        public ChargeCalculationType CalculationType { get; set; }
        public decimal Value { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsTaxable { get; set; }
        public PercentageBaseKind PercentageBase { get; set; }
        public int DisplayOrder { get; set; }
        public OrderTypeFlags AppliesToOrderTypes { get; set; } = OrderTypeFlags.All;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        [ForeignKey(nameof(RestaurantId))]
        public Restaurant? Restaurant { get; set; }
    }
}
