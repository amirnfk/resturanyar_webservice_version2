using System.Text.Json.Serialization;

namespace resturanyar.Models.Receipt
{
    public class ReceiptDto
    {
        public int OrderId { get; set; }
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public string TableNumber { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
        public OrderTypeKind OrderType { get; set; }
        public string OrderTypeLabel { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string? UpdatedAt { get; set; }
        public string? Description { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerMobile { get; set; }
        public List<ReceiptItemDto> Items { get; set; } = new();
        public List<ReceiptChargeLineDto> ChargeLines { get; set; } = new();
        public decimal ItemsSubtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal FeesTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public bool IsIssued { get; set; }
        public DateTime? IssuedAt { get; set; }
        public bool UsesCharges { get; set; }
    }

    public class ReceiptItemDto
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class ReceiptChargeLineDto
    {
        public int? DefinitionId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public ChargeCategory Category { get; set; }
        public ChargeCalculationType CalculationType { get; set; }
        public decimal Value { get; set; }
        public decimal CalculatedAmount { get; set; }
        public bool IsTaxable { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class ReceiptPreviewRequest
    {
        public OrderTypeKind OrderType { get; set; } = OrderTypeKind.DineIn;
        public List<ReceiptChargeSelectionDto> Charges { get; set; } = new();
    }

    public class ReceiptChargeSelectionDto
    {
        public int? DefinitionId { get; set; }
        public string? Code { get; set; }
        public bool IsEnabled { get; set; }
        public decimal? Value { get; set; }
    }

    public class ChargeDefinitionDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public ChargeCategory ChargeCategory { get; set; }
        public ChargeCalculationType CalculationType { get; set; }
        public decimal Value { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsTaxable { get; set; }
        public PercentageBaseKind PercentageBase { get; set; }
        public int DisplayOrder { get; set; }
        public OrderTypeFlags AppliesToOrderTypes { get; set; }
    }

    public class SaveChargeDefinitionsRequest
    {
        [JsonPropertyName("definitions")]
        public List<ChargeDefinitionDto> Definitions { get; set; } = new();
    }
}
