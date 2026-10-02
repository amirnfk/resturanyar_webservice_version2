namespace resturanyar.Models.ViewModels
{
    public class CustomerStatsViewModel
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; }
        public string Mobile { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedAtShamsi { get; set; }

        public decimal? LastOrderAmount { get; set; }
        public int TotalOrders { get; set; }
        public int TotalDistinctDays { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal AverageOrderValue { get; set; }
        public DateTime? LastOrderDate { get; set; }
        public string LastOrderDateShamsi { get; set; }

        /// <summary>Ledger balance: &gt;0 debtor, &lt;0 creditor, 0 settled.</summary>
        public decimal AccountBalance { get; set; }
    }
}
