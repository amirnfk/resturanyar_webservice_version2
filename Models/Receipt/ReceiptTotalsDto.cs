using System.Collections.Generic;

namespace resturanyar.Models.Receipt
{
    public class ReceiptTotalsDto
    {
        public decimal ItemsSubtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal FeesTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }

        public bool IsIssued { get; set; }
        public bool UsesCharges { get; set; }

        // Enabled charge lines included for breakdown UI; disabled charges are reflected via appliedCharges selection endpoint.
        public List<ReceiptChargeLineDto> ChargeLines { get; set; } = new();
    }
}

