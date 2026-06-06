namespace resturanyar.Models
{
    public class EditTableRequest
    {
        public int RestaurantId { get; set; }
        public int TableId { get; set; }
        public string TableName { get; set; }
        public int Seats { get; set; }
    }
}
