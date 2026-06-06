namespace resturanyar.Models
{
    public class UpdateSubscriptionRequest
    {
        public string Status { get; set; }
        public bool AutoRenew { get; set; }
        public DateTime? NextRenewalDate { get; set; }
        public bool IsPaid { get; set; }
    }
}
