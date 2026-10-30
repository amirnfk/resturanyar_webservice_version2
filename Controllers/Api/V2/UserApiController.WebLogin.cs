using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using resturanyar.Utility;

namespace resturanyar.Controllers.Api.V2
{
    public partial class UserApiController
    {
        public class WebLoginTicketRequest
        {
            public int RestaurantId { get; set; }
            public string Target { get; set; } = string.Empty;
        }

        [HttpPost("web-login-ticket")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableRateLimiting("WebLoginTicketPolicy")]
        public async Task<IActionResult> CreateWebLoginTicket(
            [FromBody] WebLoginTicketRequest request,
            CancellationToken ct)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "احراز هویت نامعتبر است." });

            if (request == null || request.RestaurantId <= 0 || string.IsNullOrWhiteSpace(request.Target))
                return BadRequest(new { success = false, message = "اطلاعات ناقص است." });

            var webAccess = HttpContext.RequestServices.GetRequiredService<WebAccessService>();

            if (!webAccess.TryResolveTarget(request.Target, out _))
                return BadRequest(new { success = false, message = "مقصد نامعتبر است.", code = "INVALID_TARGET" });

            var restaurant = await GetOwnedRestaurantAsync(request.RestaurantId, ownerId);
            if (restaurant == null)
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = "دسترسی به این رستوران مجاز نیست.",
                    code = "FORBIDDEN_RESTAURANT"
                });

            if (!await webAccess.CanUseWebAsync(request.RestaurantId, ct))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    code = "NEEDS_WEB_PLAN",
                    message = "این امکان مخصوص اشتراک طلایی است"
                });
            }

            var createdIp = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                            ?? HttpContext.Connection.RemoteIpAddress?.ToString();

            var (rawToken, _) = await webAccess.CreateTicketAsync(
                ownerId,
                request.RestaurantId,
                request.Target,
                createdIp,
                ct);

            var relativeUrl = "/Auth/WebLogin?ticket=" + Uri.EscapeDataString(rawToken);

            return Ok(new
            {
                success = true,
                url = relativeUrl,
                expiresInSeconds = WebAccessService.TicketTtlSeconds
            });
        }
    }
}
