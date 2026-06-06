namespace resturanyar.Models
{
    public class TableViewModel
    {
        public int TableId { get; set; }
        public int RestaurantId { get; set; }
        public string TableName { get; set; } = "";
        public int Seats { get; set; }
        public string CreatedAt { get; set; } = "";
    }
}