namespace resturanyar.Models
{
    public class SubscriptionData
    {
        public int Id { get; set; }
        public string RestaurantName { get; set; }
        public string PlanName { get; set; }
        public string Status { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal PricePaid { get; set; }
        public bool AutoRenew { get; set; }
    }
}
