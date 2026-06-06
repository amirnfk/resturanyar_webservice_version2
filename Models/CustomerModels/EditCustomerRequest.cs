namespace resturanyar.Models.CustomerModels
{
    public class EditCustomerRequest
    {
        public int CustomerId { get; set; }
        public int RestaurantId { get; set; }
        public string Mobile { get; set; }
        public string FullName { get; set; }
        public string Description { get; set; }
    }
}
