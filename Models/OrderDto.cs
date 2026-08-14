using resturanyar.Models.Receipt;

namespace resturanyar.Models

{
    public class OrderDto
    {
        public int OrderId { get; set; }
        public string TableNumber { get; set; }
        public int StatusId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string CreatedAtShamsi { get; set; }
        public string UpdatedAtShamsi { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerFullName { get; set; }
        public string CustomerMobile { get; set; }
        public string? Description { get; set; }

        public byte OrderType { get; set; }
        public decimal? ReceiptGrandTotal { get; set; }
        public DateTime? ReceiptIssuedAt { get; set; }
        public decimal? EstimatedReceiptGrandTotal { get; set; }

        /// <summary>Frozen delivery/takeaway address from OrderFulfillments (null for Dine-In).</summary>
        public string? AddressSnapshot { get; set; }

        /// <summary>Frozen customer phone from OrderFulfillments (null for Dine-In).</summary>
        public string? PhoneSnapshot { get; set; }

        /// <summary>Frozen customer name from OrderFulfillments when present.</summary>
        public string? CustomerNameSnapshot { get; set; }

        public int? AssignedDriverUserId { get; set; }
        public string? AssignedDriverName { get; set; }
        public string? DeliveryFailureReason { get; set; }
        public DateTime? DeliveryFailedAt { get; set; }

        // Server-calculated totals summary for receipt preview/breakdown UI.
        // If a receipt snapshot exists, this reflects the issued snapshot; otherwise it reflects the pre-receipt default preview.
        public ReceiptTotalsDto? ReceiptTotals { get; set; }

        public List<OrderItemDto> OrderItems { get; set; }
    }

}
