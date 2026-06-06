namespace resturanyar.Models
{
    public class OwnerLoginWithRestaurantRequest
    {
        public string Phone { get; set; }
        public string Password { get; set; }
        public int RestaurantId { get; set; }
    }
}
