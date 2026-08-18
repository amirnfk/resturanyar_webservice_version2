using Microsoft.EntityFrameworkCore;
using resturanyar.Models.SupportChat;
using Resturanyar.Data;

namespace resturanyar.Services.SupportChat
{
    public interface ISupportChatService
    {
        Task<SupportChatSettingsDto> GetSettingsAsync(CancellationToken ct = default);
        Task<SupportChatSettingsDto> UpdateSettingsAsync(SupportUpdateSettingsRequest request, CancellationToken ct = default);
        Task<int> GetTotalUnreadBySupportAsync(CancellationToken ct = default);
        Task<List<SupportConversationListItemDto>> ListConversationsAsync(CancellationToken ct = default);
        Task<SupportConversationDetailDto?> GetConversationAsync(long conversationId, int? take = 200, CancellationToken ct = default);
        Task<SupportConversation> GetOrCreateConversationAsync(SupportOpenContextRequest context, bool preferTrustedRestaurantId, CancellationToken ct = default);
        Task<(SupportMessageDto Message, SupportConversation Conversation, SupportUnreadDto Unread, bool SmsQueued)> SendCustomerMessageAsync(
            SupportSendMessageRequest request,
            bool preferTrustedRestaurantId,
            bool autoMarkReadForSupport,
            CancellationToken ct = default);
        Task<(SupportMessageDto Message, SupportConversation Conversation, SupportUnreadDto Unread)> SendSupportMessageAsync(
            long conversationId,
            string? body,
            string? imageUrl,
            Guid? clientMessageId,
            long? replyToMessageId = null,
            CancellationToken ct = default);
        Task<SupportUnreadDto> MarkConversationReadBySupportAsync(long conversationId, CancellationToken ct = default);
        Task MarkConversationReadByCustomerAsync(long conversationId, CancellationToken ct = default);
        Task TrySendOfflineSmsAsync(SupportConversation conversation, string snippet, CancellationToken ct = default);
    }

    public class SupportChatService : ISupportChatService
    {
        private readonly AppDbContext _db;
        private readonly IPayamakSmsService _sms;
        private readonly ISupportPresenceTracker _presence;

        public SupportChatService(
            AppDbContext db,
            IPayamakSmsService sms,
            ISupportPresenceTracker presence)
        {
            _db = db;
            _sms = sms;
            _presence = presence;
        }

        public async Task<SupportChatSettingsDto> GetSettingsAsync(CancellationToken ct = default)
        {
            var settings = await GetOrCreateSettingsAsync(ct);
            return ToSettingsDto(settings);
        }

        public async Task<SupportChatSettingsDto> UpdateSettingsAsync(SupportUpdateSettingsRequest request, CancellationToken ct = default)
        {
            var settings = await GetOrCreateSettingsAsync(ct);
            if (request.IsEnabled.HasValue)
                settings.IsEnabled = request.IsEnabled.Value;
            if (request.SmsNotifyWhenOffline.HasValue)
                settings.SmsNotifyWhenOffline = request.SmsNotifyWhenOffline.Value;
            if (request.SmsThrottleHours.HasValue)
                settings.SmsThrottleHours = Math.Clamp(request.SmsThrottleHours.Value, 1, 72);
            settings.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return ToSettingsDto(settings);
        }

        public Task<int> GetTotalUnreadBySupportAsync(CancellationToken ct = default)
            => _db.SupportConversations.AsNoTracking().SumAsync(c => c.UnreadBySupport, ct);

        public async Task<List<SupportConversationListItemDto>> ListConversationsAsync(CancellationToken ct = default)
        {
            var rows = await _db.SupportConversations
                .AsNoTracking()
                .OrderByDescending(c => c.LastMessageAtUtc)
                .Take(200)
                .Select(c => new
                {
                    c.Id,
                    c.RestaurantId,
                    c.GuestKey,
                    c.RestaurantName,
                    c.OwnerName,
                    c.OwnerPhone,
                    c.LastPageUrl,
                    c.LastMessageAtUtc,
                    c.UnreadBySupport
                })
                .ToListAsync(ct);

            var ids = rows.Select(r => r.Id).ToList();
            var latestMessages = await _db.SupportMessages
                .AsNoTracking()
                .Where(m => ids.Contains(m.ConversationId))
                .OrderByDescending(m => m.CreatedAtUtc)
                .Select(m => new { m.ConversationId, m.Body, m.ImageUrl, m.CreatedAtUtc })
                .ToListAsync(ct);

            var previewMap = new Dictionary<long, string>();
            foreach (var m in latestMessages)
            {
                if (previewMap.ContainsKey(m.ConversationId)) continue;
                previewMap[m.ConversationId] = string.IsNullOrWhiteSpace(m.Body)
                    ? (m.ImageUrl != null ? "🖼 تصویر" : "")
                    : m.Body!;
            }

            return rows.Select(c => new SupportConversationListItemDto
            {
                Id = c.Id,
                RestaurantId = c.RestaurantId,
                DisplayName = !string.IsNullOrWhiteSpace(c.RestaurantName)
                    ? c.RestaurantName!
                    : (!string.IsNullOrWhiteSpace(c.OwnerName) ? c.OwnerName! : (c.GuestKey != null ? "مهمان" : "گفتگو")),
                OwnerPhone = c.OwnerPhone,
                LastPageUrl = c.LastPageUrl,
                Preview = previewMap.TryGetValue(c.Id, out var p) ? Truncate(p, 80) : null,
                LastMessageAtUtc = c.LastMessageAtUtc,
                UnreadBySupport = c.UnreadBySupport,
                IsGuest = c.RestaurantId == null
            }).ToList();
        }

        public async Task<SupportConversationDetailDto?> GetConversationAsync(long conversationId, int? take = 200, CancellationToken ct = default)
        {
            var conv = await _db.SupportConversations.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conversationId, ct);
            if (conv == null) return null;

            var limit = Math.Clamp(take ?? 200, 1, 500);
            var recent = await _db.SupportMessages.AsNoTracking()
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.CreatedAtUtc)
                .Take(limit)
                .ToListAsync(ct);

            var parentIds = recent
                .Where(m => m.ReplyToMessageId.HasValue)
                .Select(m => m.ReplyToMessageId!.Value)
                .Distinct()
                .ToList();
            var knownIds = recent.Select(m => m.Id).ToHashSet();
            var missingIds = parentIds.Where(id => !knownIds.Contains(id)).ToList();
            var extraParents = missingIds.Count == 0
                ? new List<SupportMessage>()
                : await _db.SupportMessages.AsNoTracking()
                    .Where(m => m.ConversationId == conversationId && missingIds.Contains(m.Id))
                    .ToListAsync(ct);

            var parentMap = recent.Concat(extraParents).GroupBy(m => m.Id).ToDictionary(g => g.Key, g => g.First());
            var messages = recent
                .OrderBy(m => m.CreatedAtUtc)
                .Select(m => ToMessageDto(
                    m,
                    m.ReplyToMessageId is long pid && parentMap.TryGetValue(pid, out var parent) ? parent : null))
                .ToList();

            return new SupportConversationDetailDto
            {
                Id = conv.Id,
                RestaurantId = conv.RestaurantId,
                OwnerId = conv.OwnerId,
                GuestKey = conv.GuestKey,
                RestaurantName = conv.RestaurantName,
                OwnerName = conv.OwnerName,
                OwnerPhone = conv.OwnerPhone,
                LastPageUrl = conv.LastPageUrl,
                UserAgent = conv.UserAgent,
                LastMessageAtUtc = conv.LastMessageAtUtc,
                UnreadBySupport = conv.UnreadBySupport,
                UnreadByCustomer = conv.UnreadByCustomer,
                Messages = messages
            };
        }

        public async Task<SupportConversation> GetOrCreateConversationAsync(
            SupportOpenContextRequest context,
            bool preferTrustedRestaurantId,
            CancellationToken ct = default)
        {
            SupportConversation? conv = null;
            var restaurantId = preferTrustedRestaurantId ? context.RestaurantId : context.RestaurantId;

            if (restaurantId is > 0)
            {
                conv = await _db.SupportConversations
                    .FirstOrDefaultAsync(c => c.RestaurantId == restaurantId, ct);
            }
            else if (!string.IsNullOrWhiteSpace(context.GuestKey))
            {
                var guestKey = context.GuestKey.Trim();
                conv = await _db.SupportConversations
                    .FirstOrDefaultAsync(c => c.GuestKey == guestKey, ct);
            }
            else
            {
                throw new InvalidOperationException("RestaurantId or GuestKey is required.");
            }

            if (conv == null)
            {
                conv = new SupportConversation
                {
                    RestaurantId = restaurantId is > 0 ? restaurantId : null,
                    GuestKey = restaurantId is > 0 ? null : context.GuestKey?.Trim(),
                    OwnerId = context.OwnerId,
                    RestaurantName = Truncate(context.RestaurantName, 200),
                    OwnerName = Truncate(context.OwnerName, 200),
                    OwnerPhone = Truncate(context.OwnerPhone, 20),
                    LastPageUrl = Truncate(context.PageUrl, 500),
                    UserAgent = Truncate(context.UserAgent, 500),
                    CreatedAtUtc = DateTime.UtcNow,
                    LastMessageAtUtc = DateTime.UtcNow
                };
                _db.SupportConversations.Add(conv);
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                ApplyContext(conv, context);
                await _db.SaveChangesAsync(ct);
            }

            return conv;
        }

        public async Task<(SupportMessageDto Message, SupportConversation Conversation, SupportUnreadDto Unread, bool SmsQueued)> SendCustomerMessageAsync(
            SupportSendMessageRequest request,
            bool preferTrustedRestaurantId,
            bool autoMarkReadForSupport,
            CancellationToken ct = default)
        {
            ValidateContent(request.Body, request.ImageUrl);

            SupportConversation conv;
            if (request.ConversationId is > 0)
            {
                conv = await _db.SupportConversations.FirstOrDefaultAsync(c => c.Id == request.ConversationId, ct)
                       ?? throw new InvalidOperationException("Conversation not found.");
                EnsureCustomerOwnsConversation(conv, request, preferTrustedRestaurantId);
            }
            else
            {
                conv = await GetOrCreateConversationAsync(new SupportOpenContextRequest
                {
                    GuestKey = request.GuestKey,
                    RestaurantId = request.RestaurantId,
                    OwnerId = request.OwnerId,
                    RestaurantName = request.RestaurantName,
                    OwnerName = request.OwnerName,
                    OwnerPhone = request.OwnerPhone,
                    PageUrl = request.PageUrl,
                    UserAgent = request.UserAgent
                }, preferTrustedRestaurantId, ct);
            }

            ApplyContext(conv, new SupportOpenContextRequest
            {
                RestaurantName = request.RestaurantName,
                OwnerName = request.OwnerName,
                OwnerPhone = request.OwnerPhone,
                PageUrl = request.PageUrl,
                UserAgent = request.UserAgent,
                OwnerId = request.OwnerId
            });

            if (request.ClientMessageId.HasValue)
            {
                var existing = await _db.SupportMessages.AsNoTracking()
                    .FirstOrDefaultAsync(m =>
                        m.ConversationId == conv.Id && m.ClientMessageId == request.ClientMessageId, ct);
                if (existing != null)
                {
                    var total = await GetTotalUnreadBySupportAsync(ct);
                    return (await ToMessageDtoAsync(existing, ct), conv, new SupportUnreadDto
                    {
                        ConversationId = conv.Id,
                        ConversationUnread = conv.UnreadBySupport,
                        TotalUnread = total
                    }, false);
                }
            }

            var replyParent = await ResolveReplyTargetAsync(conv.Id, request.ReplyToMessageId, ct);
            var message = new SupportMessage
            {
                ConversationId = conv.Id,
                SenderType = SupportSenderType.Customer,
                Body = Truncate(request.Body?.Trim(), 2000),
                ImageUrl = Truncate(request.ImageUrl, 500),
                ClientMessageId = request.ClientMessageId,
                ReplyToMessageId = replyParent?.Id,
                CreatedAtUtc = DateTime.UtcNow
            };

            var now = DateTime.UtcNow;
            conv.LastMessageAtUtc = now;
            conv.LastCustomerMessageAtUtc = now;
            conv.UnreadByCustomer = 0;

            if (autoMarkReadForSupport)
                conv.UnreadBySupport = 0;
            else
                conv.UnreadBySupport += 1;

            _db.SupportMessages.Add(message);
            await _db.SaveChangesAsync(ct);

            var unread = new SupportUnreadDto
            {
                ConversationId = conv.Id,
                ConversationUnread = conv.UnreadBySupport,
                TotalUnread = await GetTotalUnreadBySupportAsync(ct)
            };

            var smsQueued = false;
            if (!_presence.IsSupportOnline)
            {
                var snippet = !string.IsNullOrWhiteSpace(message.Body)
                    ? message.Body!
                    : "تصویر ارسال شد";
                await TrySendOfflineSmsAsync(conv, snippet, ct);
                smsQueued = true;
            }

            return (ToMessageDto(message, replyParent), conv, unread, smsQueued);
        }

        public async Task<(SupportMessageDto Message, SupportConversation Conversation, SupportUnreadDto Unread)> SendSupportMessageAsync(
            long conversationId,
            string? body,
            string? imageUrl,
            Guid? clientMessageId,
            long? replyToMessageId = null,
            CancellationToken ct = default)
        {
            ValidateContent(body, imageUrl);

            var conv = await _db.SupportConversations.FirstOrDefaultAsync(c => c.Id == conversationId, ct)
                       ?? throw new InvalidOperationException("Conversation not found.");

            if (clientMessageId.HasValue)
            {
                var existing = await _db.SupportMessages.AsNoTracking()
                    .FirstOrDefaultAsync(m =>
                        m.ConversationId == conversationId && m.ClientMessageId == clientMessageId, ct);
                if (existing != null)
                {
                    return (await ToMessageDtoAsync(existing, ct), conv, new SupportUnreadDto
                    {
                        ConversationId = conv.Id,
                        ConversationUnread = conv.UnreadBySupport,
                        TotalUnread = await GetTotalUnreadBySupportAsync(ct)
                    });
                }
            }

            var replyParent = await ResolveReplyTargetAsync(conversationId, replyToMessageId, ct);
            var message = new SupportMessage
            {
                ConversationId = conversationId,
                SenderType = SupportSenderType.Support,
                Body = Truncate(body?.Trim(), 2000),
                ImageUrl = Truncate(imageUrl, 500),
                ClientMessageId = clientMessageId,
                ReplyToMessageId = replyParent?.Id,
                CreatedAtUtc = DateTime.UtcNow
            };

            conv.LastMessageAtUtc = DateTime.UtcNow;
            conv.UnreadBySupport = 0;
            conv.UnreadByCustomer += 1;

            _db.SupportMessages.Add(message);
            await _db.SaveChangesAsync(ct);

            return (ToMessageDto(message, replyParent), conv, new SupportUnreadDto
            {
                ConversationId = conv.Id,
                ConversationUnread = 0,
                TotalUnread = await GetTotalUnreadBySupportAsync(ct)
            });
        }

        public async Task<SupportUnreadDto> MarkConversationReadBySupportAsync(long conversationId, CancellationToken ct = default)
        {
            var conv = await _db.SupportConversations.FirstOrDefaultAsync(c => c.Id == conversationId, ct);
            if (conv == null)
                throw new InvalidOperationException("Conversation not found.");

            if (conv.UnreadBySupport != 0)
            {
                conv.UnreadBySupport = 0;
                await _db.SaveChangesAsync(ct);
            }

            return new SupportUnreadDto
            {
                ConversationId = conversationId,
                ConversationUnread = 0,
                TotalUnread = await GetTotalUnreadBySupportAsync(ct)
            };
        }

        public async Task MarkConversationReadByCustomerAsync(long conversationId, CancellationToken ct = default)
        {
            var conv = await _db.SupportConversations.FirstOrDefaultAsync(c => c.Id == conversationId, ct);
            if (conv == null) return;
            if (conv.UnreadByCustomer == 0) return;
            conv.UnreadByCustomer = 0;
            await _db.SaveChangesAsync(ct);
        }

        public async Task TrySendOfflineSmsAsync(SupportConversation conversation, string snippet, CancellationToken ct = default)
        {
            var settings = await GetOrCreateSettingsAsync(ct);
            if (!settings.SmsNotifyWhenOffline)
                return;

            if (_presence.IsSupportOnline)
                return;

            var throttle = TimeSpan.FromHours(Math.Clamp(settings.SmsThrottleHours, 1, 72));
            if (conversation.LastSmsSentAtUtc.HasValue &&
                DateTime.UtcNow - conversation.LastSmsSentAtUtc.Value < throttle)
                return;

            // Payload is fixed inside SMS service (pricelist pattern: امیر - 091)
            var ok = await _sms.NotifyAdminSupportChatAsync(string.Empty, ct);
            if (ok)
            {
                conversation.LastSmsSentAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
        }

        private async Task<SupportChatSettings> GetOrCreateSettingsAsync(CancellationToken ct)
        {
            var settings = await _db.SupportChatSettings.FirstOrDefaultAsync(s => s.Id == 1, ct);
            if (settings != null) return settings;

            settings = new SupportChatSettings
            {
                Id = 1,
                IsEnabled = true,
                SmsNotifyWhenOffline = true,
                SmsThrottleHours = 3,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.SupportChatSettings.Add(settings);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch
            {
                // race: another request inserted row 1
                _db.Entry(settings).State = EntityState.Detached;
                settings = await _db.SupportChatSettings.FirstAsync(s => s.Id == 1, ct);
            }

            return settings;
        }

        private SupportChatSettingsDto ToSettingsDto(SupportChatSettings settings) => new()
        {
            IsEnabled = settings.IsEnabled,
            SmsNotifyWhenOffline = settings.SmsNotifyWhenOffline,
            SmsThrottleHours = settings.SmsThrottleHours,
            IsSupportOnline = _presence.IsSupportOnline
        };

        private static SupportMessageDto ToMessageDto(SupportMessage m, SupportMessage? replyTo = null) => new()
        {
            Id = m.Id,
            ConversationId = m.ConversationId,
            SenderType = (byte)m.SenderType,
            Body = m.Body,
            ImageUrl = m.ImageUrl,
            ClientMessageId = m.ClientMessageId,
            CreatedAtUtc = m.CreatedAtUtc,
            ReplyToMessageId = m.ReplyToMessageId,
            ReplyToSenderType = replyTo != null ? (byte)replyTo.SenderType : null,
            ReplyToBody = replyTo == null ? null : Truncate(replyTo.Body, 120),
            ReplyToHasImage = replyTo != null && !string.IsNullOrWhiteSpace(replyTo.ImageUrl)
        };

        private async Task<SupportMessageDto> ToMessageDtoAsync(SupportMessage m, CancellationToken ct)
        {
            var parent = await ResolveReplyTargetAsync(m.ConversationId, m.ReplyToMessageId, ct);
            return ToMessageDto(m, parent);
        }

        private async Task<SupportMessage?> ResolveReplyTargetAsync(long conversationId, long? replyToMessageId, CancellationToken ct)
        {
            if (replyToMessageId is not > 0) return null;
            return await _db.SupportMessages.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == replyToMessageId && m.ConversationId == conversationId, ct);
        }

        private static void ApplyContext(SupportConversation conv, SupportOpenContextRequest context)
        {
            if (context.OwnerId.HasValue)
                conv.OwnerId = context.OwnerId;
            if (!string.IsNullOrWhiteSpace(context.RestaurantName))
                conv.RestaurantName = Truncate(context.RestaurantName, 200);
            if (!string.IsNullOrWhiteSpace(context.OwnerName))
                conv.OwnerName = Truncate(context.OwnerName, 200);
            if (!string.IsNullOrWhiteSpace(context.OwnerPhone))
                conv.OwnerPhone = Truncate(context.OwnerPhone, 20);
            if (!string.IsNullOrWhiteSpace(context.PageUrl))
                conv.LastPageUrl = Truncate(context.PageUrl, 500);
            if (!string.IsNullOrWhiteSpace(context.UserAgent))
                conv.UserAgent = Truncate(context.UserAgent, 500);
        }

        private static void EnsureCustomerOwnsConversation(
            SupportConversation conv,
            SupportSendMessageRequest request,
            bool preferTrustedRestaurantId)
        {
            if (preferTrustedRestaurantId)
            {
                if (request.RestaurantId is not > 0 || conv.RestaurantId != request.RestaurantId)
                    throw new InvalidOperationException("Conversation access denied.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(conv.GuestKey))
            {
                if (string.IsNullOrWhiteSpace(request.GuestKey)
                    || !string.Equals(conv.GuestKey, request.GuestKey.Trim(), StringComparison.Ordinal))
                    throw new InvalidOperationException("Conversation access denied.");
                return;
            }

            if (conv.RestaurantId is > 0)
            {
                if (request.RestaurantId is not > 0 || conv.RestaurantId != request.RestaurantId)
                    throw new InvalidOperationException("Conversation access denied.");
                return;
            }

            throw new InvalidOperationException("Conversation access denied.");
        }

        private static void ValidateContent(string? body, string? imageUrl)
        {
            var hasBody = !string.IsNullOrWhiteSpace(body);
            var hasImage = !string.IsNullOrWhiteSpace(imageUrl);
            if (!hasBody && !hasImage)
                throw new InvalidOperationException("Message body or image is required.");
            if (hasImage && !IsAllowedSupportImageUrl(imageUrl))
                throw new InvalidOperationException("Invalid image URL.");
        }

        /// <summary>
        /// Only relative upload paths under /uploads/support/ are allowed (blocks XSS / off-site URLs).
        /// </summary>
        private static bool IsAllowedSupportImageUrl(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return false;
            var url = imageUrl.Trim();
            if (url.Length > 500) return false;
            if (url.Contains("..", StringComparison.Ordinal)) return false;
            if (url.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '"', '\'', '<', '>', '\\' }) >= 0) return false;
            if (url.Contains(':', StringComparison.Ordinal)) return false; // no schemes
            return url.StartsWith("/uploads/support/", StringComparison.OrdinalIgnoreCase);
        }

        private static string? Truncate(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            value = value.Trim();
            return value.Length <= max ? value : value[..max];
        }
    }
}
