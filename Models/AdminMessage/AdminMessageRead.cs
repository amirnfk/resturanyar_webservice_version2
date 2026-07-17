namespace resturanyar.Models.AdminMessage
{
    public class AdminMessageRead
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public int RestaurantId { get; set; }
        public DateTime ReadAt { get; set; } = DateTime.Now;

        public AdminMessage Message { get; set; } = null!;
    }
}
