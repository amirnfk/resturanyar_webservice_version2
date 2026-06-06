namespace resturanyar.Models
{
    public class AddTableRequest
    {
        public int RestaurantId { get; set; }
        public string TableName { get; set; }
        public int Seats { get; set; }
    }
}
