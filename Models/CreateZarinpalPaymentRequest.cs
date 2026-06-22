namespace resturanyar.Models
{
    public class CreateZarinpalPaymentRequest
    {
        public int RestaurantId { get; set; }
        public int SubscriptionPlanId { get; set; }
        public string SubscriptionPeriod { get; set; }
        public string? DiscountCode { get; set; }      // <-- جدید
        public decimal? FinalPrice { get; set; }       // <-- جدید (اختیاری، برای اعتبارسنجی)

    }
}