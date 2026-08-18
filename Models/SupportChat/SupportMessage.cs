using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.SupportChat
{
    public enum SupportSenderType : byte
    {
        Customer = 0,
        Support = 1
    }

    [Table("SupportMessages")]
    public class SupportMessage
    {
        [Key]
        public long Id { get; set; }

        public long ConversationId { get; set; }

        public SupportSenderType SenderType { get; set; }

        [MaxLength(2000)]
        public string? Body { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        public Guid? ClientMessageId { get; set; }

        public long? ReplyToMessageId { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(ConversationId))]
        public SupportConversation? Conversation { get; set; }
    }
}
