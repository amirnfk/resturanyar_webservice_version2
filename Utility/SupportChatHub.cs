using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using resturanyar.Models.SupportChat;
using resturanyar.Services.SupportChat;
using resturanyar.Utility;

namespace Resturanyar.Hubs
{
    [DisableRateLimiting]
    public class SupportChatHub : Hub
    {
        public const string SupportPresenceGroup = "support";
        public const string AdminBadgeGroup = "admin-badge";

        private readonly ISupportChatService _chat;
        private readonly ISupportPresenceTracker _presence;
        private readonly ILogger<SupportChatHub> _logger;

        public SupportChatHub(
            ISupportChatService chat,
            ISupportPresenceTracker presence,
            ILogger<SupportChatHub> logger)
        {
            _chat = chat;
            _presence = presence;
            _logger = logger;
        }

        public static string ConversationGroup(long id) => $"conv:{id}";
        public static string GuestGroup(string guestKey) => $"guest:{guestKey}";
        public static string RestaurantGroup(int restaurantId) => $"restaurant:{restaurantId}";

        public async Task JoinAdminBadge()
        {
            EnsureAdmin();
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminBadgeGroup);
            var total = await _chat.GetTotalUnreadBySupportAsync();
            await Clients.Caller.SendAsync("UnreadUpdated", new SupportUnreadDto
            {
                TotalUnread = total,
                ConversationUnread = 0
            });
        }

        public async Task JoinSupportPresence()
        {
            EnsureAdmin();
            await Groups.AddToGroupAsync(Context.ConnectionId, SupportPresenceGroup);
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminBadgeGroup);
            _presence.AddPresence(Context.ConnectionId);
            await Clients.All.SendAsync("SupportStatusChanged", new { isOnline = true });
        }

        public async Task LeaveSupportPresence()
        {
            EnsureAdmin();
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, SupportPresenceGroup);
            _presence.RemovePresence(Context.ConnectionId);
            await Clients.All.SendAsync("SupportStatusChanged", new { isOnline = _presence.IsSupportOnline });
        }

        public async Task JoinConversation(long conversationId)
        {
            EnsureAdmin();
            if (conversationId <= 0) return;
            await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));
        }

        public async Task JoinCustomer(string? guestKey, int? restaurantId)
        {
            if (restaurantId is > 0)
                await Groups.AddToGroupAsync(Context.ConnectionId, RestaurantGroup(restaurantId.Value));
            if (!string.IsNullOrWhiteSpace(guestKey))
                await Groups.AddToGroupAsync(Context.ConnectionId, GuestGroup(guestKey.Trim()));
        }

        public async Task OpenConversation(SupportOpenContextRequest request)
        {
            var trustedRestaurantId = Context.User.GetRestaurantId();
            var trustedOwnerId = Context.User.GetOwnerId();
            var preferTrusted = trustedRestaurantId.HasValue;

            if (preferTrusted)
            {
                request.RestaurantId = trustedRestaurantId;
                request.OwnerId = trustedOwnerId ?? request.OwnerId;
                request.GuestKey = null;
            }

            request.UserAgent ??= Context.GetHttpContext()?.Request.Headers.UserAgent.ToString();

            var conv = await _chat.GetOrCreateConversationAsync(request, preferTrusted, Context.ConnectionAborted);
            await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conv.Id));
            if (conv.RestaurantId is > 0)
                await Groups.AddToGroupAsync(Context.ConnectionId, RestaurantGroup(conv.RestaurantId.Value));
            if (!string.IsNullOrWhiteSpace(conv.GuestKey))
                await Groups.AddToGroupAsync(Context.ConnectionId, GuestGroup(conv.GuestKey));

            await _chat.MarkConversationReadByCustomerAsync(conv.Id, Context.ConnectionAborted);
            var detail = await _chat.GetConversationAsync(conv.Id, 200, Context.ConnectionAborted);
            var settings = await _chat.GetSettingsAsync(Context.ConnectionAborted);

            await Clients.Caller.SendAsync("ConversationOpened", new
            {
                conversation = detail,
                isSupportOnline = settings.IsSupportOnline
            });
        }

        public async Task SendCustomerMessage(SupportSendMessageRequest request)
        {
            var trustedRestaurantId = Context.User.GetRestaurantId();
            var trustedOwnerId = Context.User.GetOwnerId();
            var preferTrusted = trustedRestaurantId.HasValue;

            if (preferTrusted)
            {
                request.RestaurantId = trustedRestaurantId;
                request.OwnerId = trustedOwnerId ?? request.OwnerId;
                request.GuestKey = null;
            }

            request.UserAgent ??= Context.GetHttpContext()?.Request.Headers.UserAgent.ToString();

            // If admin has this conversation open, auto-mark read is handled by separate MarkConversationRead from admin.
            // Customer send never auto-marks for support unless we pass a flag from admin (we don't).
            var result = await _chat.SendCustomerMessageAsync(
                request,
                preferTrusted,
                autoMarkReadForSupport: false,
                Context.ConnectionAborted);

            await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(result.Conversation.Id));

            await Clients.Group(ConversationGroup(result.Conversation.Id))
                .SendAsync("ReceiveMessage", result.Message);

            // Admin inbox (presence) must hear all customer messages for sound + open pane updates
            await Clients.Group(SupportPresenceGroup)
                .SendAsync("ReceiveMessage", result.Message);

            await Clients.Group(AdminBadgeGroup)
                .SendAsync("UnreadUpdated", result.Unread);

            await Clients.Group(AdminBadgeGroup)
                .SendAsync("ConversationUpdated", new
                {
                    conversationId = result.Conversation.Id,
                    lastMessageAtUtc = result.Conversation.LastMessageAtUtc,
                    unreadBySupport = result.Conversation.UnreadBySupport,
                    preview = result.Message.Body ?? (result.Message.ImageUrl != null ? "🖼 تصویر" : ""),
                    displayName = result.Conversation.RestaurantName
                                  ?? result.Conversation.OwnerName
                                  ?? (result.Conversation.GuestKey != null ? "مهمان" : "گفتگو"),
                    restaurantId = result.Conversation.RestaurantId,
                    isGuest = result.Conversation.RestaurantId == null
                });
        }

        public async Task SendSupportMessage(SupportSendMessageRequest request)
        {
            EnsureAdmin();
            if (request.ConversationId is not > 0)
                throw new HubException("ConversationId is required.");

            var result = await _chat.SendSupportMessageAsync(
                request.ConversationId.Value,
                request.Body,
                request.ImageUrl,
                request.ClientMessageId,
                request.ReplyToMessageId,
                Context.ConnectionAborted);

            await Clients.Group(ConversationGroup(result.Conversation.Id))
                .SendAsync("ReceiveMessage", result.Message);

            await Clients.Group(AdminBadgeGroup)
                .SendAsync("UnreadUpdated", result.Unread);

            // Also notify restaurant/guest groups in case they are not in conv group yet
            if (result.Conversation.RestaurantId is > 0)
            {
                await Clients.Group(RestaurantGroup(result.Conversation.RestaurantId.Value))
                    .SendAsync("ReceiveMessage", result.Message);
            }
            if (!string.IsNullOrWhiteSpace(result.Conversation.GuestKey))
            {
                await Clients.Group(GuestGroup(result.Conversation.GuestKey))
                    .SendAsync("ReceiveMessage", result.Message);
            }
        }

        public async Task MarkConversationRead(long conversationId)
        {
            EnsureAdmin();
            var unread = await _chat.MarkConversationReadBySupportAsync(conversationId, Context.ConnectionAborted);
            await Clients.Group(AdminBadgeGroup).SendAsync("UnreadUpdated", unread);
            await Clients.Group(AdminBadgeGroup).SendAsync("ConversationRead", new { conversationId });
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var wasPresence = _presence.IsSupportOnline;
            _presence.RemovePresence(Context.ConnectionId);
            if (wasPresence && !_presence.IsSupportOnline)
            {
                await Clients.All.SendAsync("SupportStatusChanged", new { isOnline = false });
            }

            await base.OnDisconnectedAsync(exception);
        }

        private void EnsureAdmin()
        {
            var http = Context.GetHttpContext();
            if (http?.Session.GetString("AdminLoggedIn") != "true")
                throw new HubException("Unauthorized");
        }
    }
}
