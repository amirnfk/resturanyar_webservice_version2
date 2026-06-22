namespace resturanyar.Models.Copoun
{
    public class CreateZarinpalPaymentRequest
    {
        public int RestaurantId { get; set; }
        public int SubscriptionPlanId { get; set; }
        public string SubscriptionPeriod { get; set; }
        public string? DiscountCode { get; set; }   // اضافه شده
        public decimal? FinalPrice { get; set; }    // اضافه شده (اختیاری)
    }
}
