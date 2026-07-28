using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.Receipt
{
    public class ReceiptPrintHistory
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int OrderReceiptSnapshotId { get; set; }
        public DateTime PrintedAt { get; set; }
        public int? PrintedByUserId { get; set; }

        [MaxLength(20)]
        public string Channel { get; set; } = "Web";

        public decimal? ItemsSubtotal { get; set; }
        public decimal? GrandTotal { get; set; }
        public string? ReceiptPayloadJson { get; set; }

        [ForeignKey(nameof(OrderReceiptSnapshotId))]
        public OrderReceiptSnapshot? Snapshot { get; set; }
    }
}
