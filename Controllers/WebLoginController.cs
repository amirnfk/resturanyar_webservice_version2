using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using resturanyar.Utility;
using Resturanyar.Data;

namespace resturanyar.Controllers
{
    [AllowAnonymous]
    [Route("Auth")]
    public class WebLoginController : Controller
    {
        private readonly WebAccessService _webAccess;
        private readonly AuthService _authService;
        private readonly AppDbContext _context;

        public WebLoginController(
            WebAccessService webAccess,
            AuthService authService,
            AppDbContext context)
        {
            _webAccess = webAccess;
            _authService = authService;
            _context = context;
        }

        [HttpGet("WebLogin")]
        public async Task<IActionResult> WebLogin([FromQuery] string? ticket, CancellationToken ct)
        {
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Referrer-Policy"] = "no-referrer";

            var consumed = await _webAccess.ConsumeTicketAsync(ticket, ct);
            if (consumed == null)
            {
                TempData["Error"] = "لینک ورود منقضی یا نامعتبر است. لطفاً دوباره از اپلیکیشن وارد شوید.";
                return RedirectToAction("ManagerLogin", "Home");
            }

            if (!_webAccess.TryResolveTarget(consumed.Target, out var targetUrl))
            {
                TempData["Error"] = "مقصد ورود نامعتبر است.";
                return RedirectToAction("ManagerLogin", "Home");
            }

            if (!await _webAccess.CanUseWebAsync(consumed.RestaurantId, ct))
            {
                HttpContext.Session.SetInt32("UpgradeRestaurantId", consumed.RestaurantId);
                var restaurantName = await _context.Restaurants
                    .AsNoTracking()
                    .Where(r => r.restaurant_id == consumed.RestaurantId)
                    .Select(r => r.name)
                    .FirstOrDefaultAsync(ct);
                if (!string.IsNullOrEmpty(restaurantName))
                    HttpContext.Session.SetString("UpgradeRestaurantName", restaurantName);

                return RedirectToAction("Upgrade", "Home");
            }

            var owner = await _context.Owners
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == consumed.OwnerId, ct);

            if (owner == null)
            {
                TempData["Error"] = "حساب کاربری یافت نشد.";
                return RedirectToAction("ManagerLogin", "Home");
            }

            if (!await _webAccess.IsOwnerOfRestaurantAsync(consumed.OwnerId, consumed.RestaurantId, ct))
            {
                TempData["Error"] = "دسترسی به این رستوران مجاز نیست.";
                return RedirectToAction("ManagerLogin", "Home");
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await _authService.SignInOwnerCookieAsync(owner, consumed.RestaurantId);

            var tokenPair = await _authService.IssueTokenPairAsync(owner);

            var model = new WebLoginBootstrapViewModel
            {
                AccessToken = tokenPair.Token,
                RefreshToken = tokenPair.RefreshToken,
                Phone = owner.Phone ?? string.Empty,
                TargetUrl = targetUrl
            };

            return View("Bootstrap", model);
        }
    }

    public class WebLoginBootstrapViewModel
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string TargetUrl { get; set; } = string.Empty;
    }
}
