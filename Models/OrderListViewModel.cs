namespace resturanyar.Models
{
    public class OrderListViewModel
    {
        public List<OrderDto> Orders { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);

        // filters
        public int? FilterStatusId { get; set; } = null; // null => default active
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Search { get; set; }

        // maps for view
        public Dictionary<int, string> StatusMap { get; set; } = new();
        public Dictionary<int, string> StatusColors { get; set; } = new();
    }

    

    
}
