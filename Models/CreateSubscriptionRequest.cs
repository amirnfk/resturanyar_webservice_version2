namespace resturanyar.Models
{
    public class CreateSubscriptionRequest
    {
        public int RestaurantId { get; set; }
        public int OwnerId { get; set; }
        public int SubscriptionPlanId { get; set; }
        public string SubscriptionPeriod { get; set; } // "Monthly", "3Monthly", "6Monthly", "12Monthly"
        public decimal PricePaid { get; set; }
        public decimal? DiscountApplied { get; set; }
        public string PaymentMethod { get; set; }
        public string TransactionId { get; set; }
        public bool AutoRenew { get; set; } = false;
        public string CafeBazarPurchaseToken { get; set; }
        public string CafeBazarOrderId { get; set; }
    }
}
