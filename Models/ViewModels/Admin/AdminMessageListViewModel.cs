using resturanyar.Models.AdminMessage;

namespace resturanyar.Models.ViewModels.Admin
{
    public class AdminMessageListViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public AdminMessageType MessageType { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedByAdmin { get; set; }
        public bool IsActive { get; set; }
        public int RecipientCount { get; set; }
    }
}
