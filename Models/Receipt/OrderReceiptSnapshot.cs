using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.Receipt
{
    public class OrderReceiptSnapshot
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int RestaurantId { get; set; }
        public OrderTypeKind OrderType { get; set; }
        public decimal ItemsSubtotal { get; set; }
        public decimal GrandTotal { get; set; }
        public string ChargeLinesJson { get; set; } = string.Empty;
        public string ReceiptPayloadJson { get; set; } = string.Empty;
        public DateTime OrderItemsVersion { get; set; }
        public DateTime IssuedAt { get; set; }
        public int? IssuedByUserId { get; set; }

        [ForeignKey(nameof(OrderId))]
        public Order? Order { get; set; }
    }
}
