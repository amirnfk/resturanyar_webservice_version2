namespace resturanyar.Models
{
    public class OrderDateFilterRequest
    {
        public int RestaurantId { get; set; }
        public string FromDate { get; set; } // فرمت: yyyy/MM/dd
        public string ToDate { get; set; }   // فرمت: yyyy/MM/dd
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}