namespace resturanyar.Models.SupportChat
{
    public class SupportChatSettingsDto
    {
        public bool IsEnabled { get; set; }
        public bool SmsNotifyWhenOffline { get; set; }
        public int SmsThrottleHours { get; set; }
        public bool IsSupportOnline { get; set; }
    }

    public class SupportConversationListItemDto
    {
        public long Id { get; set; }
        public int? RestaurantId { get; set; }
        public string DisplayName { get; set; } = "";
        public string? OwnerPhone { get; set; }
        public string? LastPageUrl { get; set; }
        public string? Preview { get; set; }
        public DateTime LastMessageAtUtc { get; set; }
        public int UnreadBySupport { get; set; }
        public bool IsGuest { get; set; }
    }

    public class SupportConversationDetailDto
    {
        public long Id { get; set; }
        public int? RestaurantId { get; set; }
        public int? OwnerId { get; set; }
        public string? GuestKey { get; set; }
        public string? RestaurantName { get; set; }
        public string? OwnerName { get; set; }
        public string? OwnerPhone { get; set; }
        public string? LastPageUrl { get; set; }
        public string? UserAgent { get; set; }
        public DateTime LastMessageAtUtc { get; set; }
        public int UnreadBySupport { get; set; }
        public int UnreadByCustomer { get; set; }
        public List<SupportMessageDto> Messages { get; set; } = new();
    }

    public class SupportMessageDto
    {
        public long Id { get; set; }
        public long ConversationId { get; set; }
        public byte SenderType { get; set; }
        public string? Body { get; set; }
        public string? ImageUrl { get; set; }
        public Guid? ClientMessageId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public long? ReplyToMessageId { get; set; }
        public byte? ReplyToSenderType { get; set; }
        public string? ReplyToBody { get; set; }
        public bool ReplyToHasImage { get; set; }
    }

    public class SupportSendMessageRequest
    {
        public long? ConversationId { get; set; }
        public string? Body { get; set; }
        public string? ImageUrl { get; set; }
        public Guid? ClientMessageId { get; set; }
        public long? ReplyToMessageId { get; set; }
        public string? GuestKey { get; set; }
        public int? RestaurantId { get; set; }
        public int? OwnerId { get; set; }
        public string? RestaurantName { get; set; }
        public string? OwnerName { get; set; }
        public string? OwnerPhone { get; set; }
        public string? PageUrl { get; set; }
        public string? UserAgent { get; set; }
    }

    public class SupportOpenContextRequest
    {
        public string? GuestKey { get; set; }
        public int? RestaurantId { get; set; }
        public int? OwnerId { get; set; }
        public string? RestaurantName { get; set; }
        public string? OwnerName { get; set; }
        public string? OwnerPhone { get; set; }
        public string? PageUrl { get; set; }
        public string? UserAgent { get; set; }
    }

    public class SupportUpdateSettingsRequest
    {
        public bool? IsEnabled { get; set; }
        public bool? SmsNotifyWhenOffline { get; set; }
        public int? SmsThrottleHours { get; set; }
    }

    public class SupportUnreadDto
    {
        public long? ConversationId { get; set; }
        public int ConversationUnread { get; set; }
        public int TotalUnread { get; set; }
    }
}
