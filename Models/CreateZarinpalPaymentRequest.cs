namespace resturanyar.Models
{
    public class CreateZarinpalPaymentRequest
    {
        public int RestaurantId { get; set; }
        public int SubscriptionPlanId { get; set; }
        public string SubscriptionPeriod { get; set; } 
      
    }
}