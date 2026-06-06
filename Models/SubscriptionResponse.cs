namespace resturanyar.Models
{
    public class SubscriptionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public SubscriptionData Data { get; set; }
    }
}
