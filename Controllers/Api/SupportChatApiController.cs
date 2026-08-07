using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using resturanyar.Models.SupportChat;
using resturanyar.Services.SupportChat;
using resturanyar.Utility;

namespace resturanyar.Controllers.Api
{
    [ApiController]
    [Route("api/support-chat")]
    public class SupportChatApiController : ControllerBase
    {
        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif"
        };

        private const long MaxImageBytes = 3 * 1024 * 1024;

        private readonly ISupportChatService _chat;
        private readonly IWebHostEnvironment _env;

        public SupportChatApiController(ISupportChatService chat, IWebHostEnvironment env)
        {
            _chat = chat;
            _env = env;
        }

        private bool IsAdminLoggedIn()
            => HttpContext.Session.GetString("AdminLoggedIn") == "true";

        [HttpGet("settings")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSettings(CancellationToken ct)
        {
            try
            {
                var settings = await _chat.GetSettingsAsync(ct);
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        settings.IsEnabled,
                        settings.IsSupportOnline
                    }
                });
            }
            catch
            {
                return Ok(new
                {
                    success = true,
                    data = new { isEnabled = false, isSupportOnline = false }
                });
            }
        }

        [HttpGet("admin/settings")]
        public async Task<IActionResult> GetAdminSettings(CancellationToken ct)
        {
            if (!IsAdminLoggedIn()) return Unauthorized();
            var settings = await _chat.GetSettingsAsync(ct);
            return Ok(new { success = true, data = settings });
        }

        [HttpPost("admin/settings")]
        public async Task<IActionResult> UpdateAdminSettings([FromBody] SupportUpdateSettingsRequest request, CancellationToken ct)
        {
            if (!IsAdminLoggedIn()) return Unauthorized();
            var settings = await _chat.UpdateSettingsAsync(request, ct);
            return Ok(new { success = true, data = settings });
        }

        [HttpGet("admin/unread-total")]
        public async Task<IActionResult> GetUnreadTotal(CancellationToken ct)
        {
            if (!IsAdminLoggedIn()) return Unauthorized();
            var total = await _chat.GetTotalUnreadBySupportAsync(ct);
            return Ok(new { success = true, data = new { totalUnread = total } });
        }

        [HttpGet("admin/conversations")]
        public async Task<IActionResult> ListConversations(CancellationToken ct)
        {
            if (!IsAdminLoggedIn()) return Unauthorized();
            var list = await _chat.ListConversationsAsync(ct);
            return Ok(new { success = true, data = list });
        }

        [HttpGet("admin/conversations/{id:long}")]
        public async Task<IActionResult> GetConversation(long id, CancellationToken ct)
        {
            if (!IsAdminLoggedIn()) return Unauthorized();
            var detail = await _chat.GetConversationAsync(id, 200, ct);
            if (detail == null) return NotFound(new { success = false, message = "گفتگو یافت نشد." });
            return Ok(new { success = true, data = detail });
        }

        [HttpPost("admin/conversations/{id:long}/read")]
        public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
        {
            if (!IsAdminLoggedIn()) return Unauthorized();
            var unread = await _chat.MarkConversationReadBySupportAsync(id, ct);
            return Ok(new { success = true, data = unread });
        }

        [HttpPost("open")]
        [AllowAnonymous]
        public async Task<IActionResult> Open([FromBody] SupportOpenContextRequest request, CancellationToken ct)
        {
            try
            {
                var trustedRestaurantId = User.GetRestaurantId();
                var trustedOwnerId = User.GetOwnerId();
                var preferTrusted = trustedRestaurantId.HasValue;
                if (preferTrusted)
                {
                    request.RestaurantId = trustedRestaurantId;
                    request.OwnerId = trustedOwnerId ?? request.OwnerId;
                    request.GuestKey = null;
                }

                request.UserAgent ??= Request.Headers.UserAgent.ToString();
                var conv = await _chat.GetOrCreateConversationAsync(request, preferTrusted, ct);
                await _chat.MarkConversationReadByCustomerAsync(conv.Id, ct);
                var detail = await _chat.GetConversationAsync(conv.Id, 200, ct);
                var settings = await _chat.GetSettingsAsync(ct);
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        conversation = detail,
                        isSupportOnline = settings.IsSupportOnline
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("messages")]
        [AllowAnonymous]
        public async Task<IActionResult> SendCustomerMessage([FromBody] SupportSendMessageRequest request, CancellationToken ct)
        {
            try
            {
                var trustedRestaurantId = User.GetRestaurantId();
                var trustedOwnerId = User.GetOwnerId();
                var preferTrusted = trustedRestaurantId.HasValue;
                if (preferTrusted)
                {
                    request.RestaurantId = trustedRestaurantId;
                    request.OwnerId = trustedOwnerId ?? request.OwnerId;
                    request.GuestKey = null;
                }

                request.UserAgent ??= Request.Headers.UserAgent.ToString();
                var result = await _chat.SendCustomerMessageAsync(request, preferTrusted, false, ct);
                return Ok(new { success = true, data = result.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("upload")]
        [AllowAnonymous]
        [RequestSizeLimit(MaxImageBytes + 4096)]
        public async Task<IActionResult> UploadImage(IFormFile? file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "فایل تصویر الزامی است." });

            if (file.Length > MaxImageBytes)
                return BadRequest(new { success = false, message = "حداکثر حجم تصویر ۳ مگابایت است." });

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext) || !AllowedImageExtensions.Contains(ext))
            {
                // Mobile WebViews sometimes omit the filename extension; derive from Content-Type.
                ext = file.ContentType?.ToLowerInvariant() switch
                {
                    "image/jpeg" or "image/jpg" => ".jpg",
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    "image/gif" => ".gif",
                    _ => ""
                };
            }

            if (string.IsNullOrWhiteSpace(ext) || !AllowedImageExtensions.Contains(ext))
                return BadRequest(new { success = false, message = "فرمت تصویر مجاز نیست." });

            var year = DateTime.UtcNow.Year.ToString();
            var relativeDir = Path.Combine("uploads", "support", year);
            var absoluteDir = Path.Combine(_env.WebRootPath, relativeDir);
            Directory.CreateDirectory(absoluteDir);

            var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var absolutePath = Path.Combine(absoluteDir, fileName);
            await using (var stream = System.IO.File.Create(absolutePath))
            {
                await file.CopyToAsync(stream, ct);
            }

            var url = $"/uploads/support/{year}/{fileName}";
            return Ok(new { success = true, data = new { imageUrl = url } });
        }
    }
}
