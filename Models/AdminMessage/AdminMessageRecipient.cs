namespace resturanyar.Models.AdminMessage
{
    public class AdminMessageRecipient
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public int RestaurantId { get; set; }

        public AdminMessage Message { get; set; } = null!;
    }
}
