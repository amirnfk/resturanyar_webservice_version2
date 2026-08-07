using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.SupportChat
{
    [Table("SupportChatSettings")]
    public class SupportChatSettings
    {
        [Key]
        public int Id { get; set; } = 1;

        public bool IsEnabled { get; set; }

        public bool SmsNotifyWhenOffline { get; set; } = true;

        public int SmsThrottleHours { get; set; } = 3;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
