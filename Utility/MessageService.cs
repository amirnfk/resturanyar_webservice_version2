using Microsoft.EntityFrameworkCore;
using resturanyar.Models.AdminMessage;
using Resturanyar.Data;

namespace resturanyar.Utility
{
    public class MessageService
    {
        private readonly AppDbContext _context;

        public MessageService(AppDbContext context)
        {
            _context = context;
        }

        public bool ValidateRestaurantOwnership(int restaurantId, int ownerId)
        {
            return _context.Restaurants.Any(r => r.restaurant_id == restaurantId && r.owner_id == ownerId);
        }

        public async Task<List<RestaurantMessageDto>> GetMessagesForRestaurantAsync(int restaurantId, bool unreadOnly = false)
        {
            var readMessageIds = await _context.AdminMessageReads
                .Where(r => r.RestaurantId == restaurantId)
                .Select(r => r.MessageId)
                .ToListAsync();

            var messages = await _context.AdminMessages
                .AsNoTracking()
                .Where(m => m.IsActive)
                .Where(m =>
                    m.MessageType == AdminMessageType.Public ||
                    m.Recipients.Any(r => r.RestaurantId == restaurantId))
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new RestaurantMessageDto
                {
                    Id = m.Id,
                    Title = m.Title,
                    Body = m.Body,
                    MessageType = m.MessageType,
                    CreatedAt = m.CreatedAt,
                    IsRead = readMessageIds.Contains(m.Id)
                })
                .ToListAsync();

            if (unreadOnly)
                messages = messages.Where(m => !m.IsRead).ToList();

            return messages;
        }

        public async Task<int> GetUnreadCountAsync(int restaurantId)
        {
            var messages = await GetMessagesForRestaurantAsync(restaurantId, unreadOnly: true);
            return messages.Count;
        }

        public async Task<bool> MarkAsReadAsync(int messageId, int restaurantId)
        {
            var isEligible = await _context.AdminMessages
                .AnyAsync(m => m.Id == messageId && m.IsActive &&
                    (m.MessageType == AdminMessageType.Public ||
                     m.Recipients.Any(r => r.RestaurantId == restaurantId)));

            if (!isEligible)
                return false;

            var exists = await _context.AdminMessageReads
                .AnyAsync(r => r.MessageId == messageId && r.RestaurantId == restaurantId);

            if (exists)
                return true;

            _context.AdminMessageReads.Add(new AdminMessageRead
            {
                MessageId = messageId,
                RestaurantId = restaurantId,
                ReadAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<AdminMessage> CreateMessageAsync(
            string title,
            string body,
            AdminMessageType messageType,
            int[] selectedRestaurantIds,
            string? createdByAdmin)
        {
            var message = new AdminMessage
            {
                Title = title.Trim(),
                Body = body.Trim(),
                MessageType = messageType,
                CreatedByAdmin = createdByAdmin,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            _context.AdminMessages.Add(message);
            await _context.SaveChangesAsync();

            if (messageType == AdminMessageType.Private && selectedRestaurantIds.Length > 0)
            {
                var recipients = selectedRestaurantIds
                    .Distinct()
                    .Select(id => new AdminMessageRecipient
                    {
                        MessageId = message.Id,
                        RestaurantId = id
                    });

                _context.AdminMessageRecipients.AddRange(recipients);
                await _context.SaveChangesAsync();
            }

            return message;
        }

        public async Task DeactivateMessageAsync(int messageId)
        {
            var message = await _context.AdminMessages.FindAsync(messageId);
            if (message != null)
            {
                message.IsActive = false;
                await _context.SaveChangesAsync();
            }
        }
    }
}
