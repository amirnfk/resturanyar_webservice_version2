using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Models.AuthorizationModels;
using resturanyar.Utility;
using Resturanyar.Data;
using Resturanyar.Hubs;
using System.Security.Claims;

namespace resturanyar.Controllers.Api.V2
{
    [ApiController]
    [ApiVersion("2.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Staff")]
    public partial class StaffApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly TokenService _tokenService;
        private readonly AuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly IWebHostEnvironment _env;

        public StaffApiController(
            AppDbContext context,
            TokenService tokenService,
            AuthService authService,
            IConfiguration configuration,
            IHubContext<OrderHub> hubContext,
            IWebHostEnvironment env)
        {
            _context = context;
            _tokenService = tokenService;
            _authService = authService;
            _configuration = configuration;
            _hubContext = hubContext;
            _env = env;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginUserRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.name) ||
                    string.IsNullOrWhiteSpace(request.password) ||
                    string.IsNullOrWhiteSpace(request.restaurant_code))
                {
                    return BadRequest(new { success = false, message = "نام کاربری، رمز عبور و کد رستوران الزامی است." });
                }

                var restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(r => r.restaurant_code == request.restaurant_code);

                if (restaurant == null)
                {
                    return Ok(new { success = false, message = "کد رستوران معتبر نیست" });
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.name == request.name &&
                        u.restaurant_id == restaurant.restaurant_id);

                if (user == null || AuthService.DecodePassword(user.password) != request.password)
                {
                    return Ok(new { success = false, message = "کاربری با این مشخصات یافت نشد" });
                }

                var activeSubscription = await _context.Subscriptions
                    .Include(s => s.SubscriptionPlan)
                    .Where(s => s.RestaurantId == restaurant.restaurant_id &&
                                s.Status == "Active" &&
                                s.EndDate > DateTime.Now)
                    .Select(s => new
                    {
                        plan_name = s.SubscriptionPlan.Name,
                        end_date = s.EndDate,
                        days_remaining = (s.EndDate - DateTime.Now).Days,
                        features = new
                        {
                            employee_limit = s.SubscriptionPlan.EmployeeLimit,
                            food_limit = s.SubscriptionPlan.FoodLimit,
                            can_use_web = s.SubscriptionPlan.CanUseWeb,
                            can_use_printer = s.SubscriptionPlan.CanUsePrinter
                        }
                    })
                    .FirstOrDefaultAsync();

                var tokenPair = await _authService.IssueStaffTokenPairAsync(user);

                return Ok(new
                {
                    success = true,
                    message = "ورود موفقیت‌آمیز بود",
                    token = tokenPair.Token,
                    refreshToken = tokenPair.RefreshToken,
                    expiresAt = tokenPair.ExpiresAt,
                    user = new
                    {
                        user_id = user.user_id,
                        name = user.name,
                        role = user.role_id,
                        restaurant_id = user.restaurant_id,
                        restaurant_code = restaurant.restaurant_code,
                        restaurant_name = restaurant.name,
                        order_management_permission = user.order_management_permission,
                        kitchen_management_permission = user.kitchen_management_permission,
                        payment_management_permission = user.payment_management_permission
                    },
                    subscription = activeSubscription,
                    has_active_subscription = activeSubscription != null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا: " + ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestModel request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.RefreshToken))
                {
                    return Unauthorized(new { success = false, message = "توکن نامعتبر است." });
                }

                var existingToken = await _context.StaffRefreshTokens
                    .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

                if (existingToken == null || existingToken.ExpiryTime <= DateTime.UtcNow)
                {
                    if (existingToken != null)
                    {
                        _context.StaffRefreshTokens.Remove(existingToken);
                        await _context.SaveChangesAsync();
                    }
                    return Unauthorized(new { success = false, message = "نشست منقضی شده است. لطفا مجدد وارد شوید." });
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.user_id == existingToken.UserId);
                if (user == null)
                {
                    _context.StaffRefreshTokens.Remove(existingToken);
                    await _context.SaveChangesAsync();
                    return Unauthorized(new { success = false, message = "کاربر نامعتبر است." });
                }

                string newJwt = _tokenService.GenerateStaffToken(user);
                string newRefresh = _tokenService.GenerateRefreshToken();

                existingToken.Token = newRefresh;
                existingToken.RestaurantId = user.restaurant_id;
                // TEMP: uses RefreshExpirationMinutes when set (see TokenService.GetRefreshExpirationUtc)
                existingToken.ExpiryTime = _tokenService.GetRefreshExpirationUtc();

                // TEMP: uses JwtExpirationMinutes when set (see TokenService.GetJwtExpirationUtc)
                var jwtExpiration = _tokenService.GetJwtExpirationUtc();

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    token = newJwt,
                    refreshToken = newRefresh,
                    expiresAt = jwtExpiration
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در تمدید نشست.", detail = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestModel request)
        {
            if (request != null && !string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                var tokenRecord = await _context.StaffRefreshTokens
                    .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);
                if (tokenRecord != null)
                {
                    _context.StaffRefreshTokens.Remove(tokenRecord);
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new { success = true, message = "با موفقیت خارج شد." });
        }

        private bool TryGetStaffRestaurantId(out int restaurantId)
        {
            restaurantId = 0;
            var claim = User.FindFirst("restaurant_id")?.Value;
            return !string.IsNullOrEmpty(claim) && int.TryParse(claim, out restaurantId);
        }

        private bool TryGetStaffUserId(out int userId)
        {
            userId = 0;
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return !string.IsNullOrEmpty(claim) && int.TryParse(claim, out userId);
        }

        private IActionResult? EnsureRestaurantAccess(int restaurantId)
        {
            if (!TryGetStaffRestaurantId(out int claimRestaurantId))
                return Unauthorized(new { success = false, message = "احراز هویت نامعتبر است." });
            if (claimRestaurantId != restaurantId)
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "دسترسی به این رستوران مجاز نیست." });
            return null;
        }

        private int? GetNextRoleId(int statusId)
        {
            switch (statusId)
            {
                case 3: return 3;
                case 4: return 3;
                case 5: return 2;
                case 6: return 4;
                case 7: return 4;
                case 8: return 4;
                case 9: return 4;
                case 10: return 4;
                case 11: return 4;
                case 12: return 3;
                case 99: return 3;
                default: return null;
            }
        }
    }
}
