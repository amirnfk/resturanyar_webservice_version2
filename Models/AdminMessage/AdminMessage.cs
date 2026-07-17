using System.ComponentModel.DataAnnotations;

namespace resturanyar.Models.AdminMessage
{
    public class AdminMessage
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Body { get; set; } = string.Empty;

        public AdminMessageType MessageType { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string? CreatedByAdmin { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<AdminMessageRecipient> Recipients { get; set; } = new List<AdminMessageRecipient>();
        public ICollection<AdminMessageRead> Reads { get; set; } = new List<AdminMessageRead>();
    }
}
