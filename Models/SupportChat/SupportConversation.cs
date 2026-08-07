using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.SupportChat
{
    [Table("SupportConversations")]
    public class SupportConversation
    {
        [Key]
        public long Id { get; set; }

        public int? RestaurantId { get; set; }

        public int? OwnerId { get; set; }

        [MaxLength(64)]
        public string? GuestKey { get; set; }

        [MaxLength(200)]
        public string? RestaurantName { get; set; }

        [MaxLength(200)]
        public string? OwnerName { get; set; }

        [MaxLength(20)]
        public string? OwnerPhone { get; set; }

        [MaxLength(500)]
        public string? LastPageUrl { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        public DateTime LastMessageAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? LastCustomerMessageAtUtc { get; set; }

        public DateTime? LastSmsSentAtUtc { get; set; }

        public int UnreadBySupport { get; set; }

        public int UnreadByCustomer { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
    }
}
