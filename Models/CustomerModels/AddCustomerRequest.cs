namespace resturanyar.Models.CustomerModels
{
    public class AddCustomerRequest
    {
        public int RestaurantId { get; set; }
        public string Mobile { get; set; }
        public string FullName { get; set; }
        public string Description { get; set; }
    }
}
