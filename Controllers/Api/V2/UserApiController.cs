using Asp.Versioning;
using global::Resturanyar.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Utility;
using System.Security.Claims;
using System.Text;

namespace resturanyar.Controllers.Api.V2
{
    [ApiController]
    [ApiVersion("2.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class UserApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly TokenService _tokenService;

        public UserApiController(AppDbContext context, TokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        /// <summary>
        /// متد ورود و بررسی هویت مالک رستوران و تولید توکن JWT
        /// </summary>
        [AllowAnonymous]
        [HttpPost("generate-token")]
        public IActionResult GenerateTokenAction([FromBody] TokenRequestModel request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.PhoneNumber))
                {
                    return BadRequest(new { success = false, message = "شماره تلفن الزامی است." });
                }

                var owner = _context.Owners.FirstOrDefault(c => c.Phone == request.PhoneNumber);
                if (owner == null)
                {
                    return NotFound(new { success = false, message = "کاربری با این شماره تلفن یافت نشد." });
                }

                bool hasRestaurant = _context.Restaurants.Any(r => r.owner_id == owner.Id);
                if (!hasRestaurant)
                {
                    return BadRequest(new { success = false, message = "خطا: حساب شما به هیچ رستورانی متصل نیست یا شما مالک رستورانی نیستید." });
                }

                string jwtToken = _tokenService.GenerateOwnerToken(owner);

                return Ok(new
                {
                    success = true,
                    message = "خوش آمدید. هویت شما به عنوان مالک رستوران تایید شد.",
                    token = jwtToken,
                    expiresAt = DateTime.Now.AddDays(30)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطایی در سرور رخ داد: " + ex.Message });
            }
        }

        public class TokenRequestModel
        {
            public string PhoneNumber { get; set; }
        }

        /// <summary>
        /// دریافت لیست رستوران‌های متعلق به مالک (بر اساس توکن)
        /// در صورت ارسال پارامتر ownerId در کوئری، تطابق آن با مالک توکن بررسی می‌شود.
        /// </summary>
        [HttpGet("getrestaurants")]
        public IActionResult GetRestaurantsByOwnerV2([FromQuery] int? ownerId = null)
        {
            try
            {
                // ۱. استخراج ownerId از توکن
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int tokenOwnerId))
                {
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });
                }

                // ۲. اگر ownerId در کوئری ارسال شده، بررسی تطابق با مالک توکن
                int targetOwnerId = tokenOwnerId; // پیش‌فرض: خود مالک توکن
                if (ownerId.HasValue)
                {
                    if (ownerId.Value != tokenOwnerId)
                    {
                        return Forbid(); // یا بازگرداندن خطای دسترسی
                        // یا: return Unauthorized(new { success = false, message = "شما مجاز به مشاهده اطلاعات این مالک نیستید." });
                    }
                    targetOwnerId = ownerId.Value;
                }

                // ۳. یافتن مالک
                var owner = _context.Owners.Find(targetOwnerId);
                if (owner == null)
                {
                    return NotFound(new { success = false, message = "مالک یافت نشد." });
                }

                // ۴. واکشی لیست رستوران‌های این مالک
                var restaurants = _context.Restaurants
                    .Where(r => r.owner_id == targetOwnerId)
                    .Select(r => new { r.restaurant_id, r.name })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    owner = new { owner.Id, owner.Name },
                    restaurants = restaurants
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطای سرور در واکشی لیست رستوران‌ها" });
            }
        }

        /// <summary>
        /// حذف کاربر با اعمال سطح دسترسی مالکیت رستوران
        /// </summary>
        [HttpDelete("deleteuser/{restaurantId}/{userId}")]
        public IActionResult DeleteUserV2(int restaurantId, int userId)
        {
            var claimValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(claimValue) || !int.TryParse(claimValue, out int currentOwnerId))
            {
                return Unauthorized(new { success = false, message = "توکن نامعتبر است." });
            }

            bool isOwnerOfRestaurant = _context.Restaurants
                .Any(r => r.restaurant_id == restaurantId && r.owner_id == currentOwnerId);

            if (!isOwnerOfRestaurant)
            {
                return Forbid();
            }

            var user = _context.Users.FirstOrDefault(u => u.user_id == userId && u.restaurant_id == restaurantId);
            if (user == null) return NotFound(new { success = false, message = "کاربر یافت نشد" });

            _context.Users.Remove(user);
            _context.SaveChanges();

            return Ok(new { success = true, message = "کاربر با موفقیت حذف شد" });
        }


        [HttpPost("addrestaurant")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] // حتماً این ویژگی را اضافه کنید
        public IActionResult AddRestaurant(AddRestaurantRequest request)
        {
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int ownerIdFromToken))
                {
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });
                }

                // ۲. استفاده از ownerId استخراج شده به جای request.owner_id
                var owner = _context.Owners.Find(ownerIdFromToken);
                if (owner == null)
                    return NotFound(new { success = false, message = "مالک با این شناسه یافت نشد" });

                // ۳. بررسی اشتراک طلایی فعال
                bool hasActiveGold = _context.Subscriptions
                    .Include(s => s.SubscriptionPlan)
                    .Any(s =>
                        s.OwnerId == ownerIdFromToken &&
                        s.Status == "Active" &&
                        s.EndDate > DateTime.Now &&
                        s.SubscriptionPlan.Name == "طلایی"
                    );

                // ۴. تعداد رستوران‌های فعلی مالک
                int restaurantCount = _context.Restaurants.Count(r => r.owner_id == ownerIdFromToken);

                // ۵. محدودیت تعداد رستوران برای غیرطلایی
                if (restaurantCount > 0 && !hasActiveGold)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "برای افزودن رستوران جدید، باید حداقل یک اشتراک طلایی فعال داشته باشید."
                    });
                }

                // ۶. بررسی تکراری نبودن نام
                bool isDuplicate = _context.Restaurants.Any(r =>
                    r.owner_id == ownerIdFromToken &&
                    r.name.ToLower().Trim() == request.name.ToLower().Trim()
                );

                if (isDuplicate)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "رستورانی با این نام قبلاً برای این مالک ثبت شده است."
                    });
                }

                // ۷. ساخت رستوران جدید
                var restaurant = new Restaurant
                {
                    name = request.name.Trim(),
                    owner_id = ownerIdFromToken, // استفاده از ownerId از توکن
                    restaurant_code = GenerateUniqueRestaurantCode(),
                    PublicMenuToken = Guid.NewGuid().ToString("N"),
                };
                _context.Restaurants.Add(restaurant);
                _context.SaveChanges();

                // ۸. افزودن کاربران پیش‌فرض
                var defaultUsers = new List<User>
        {
            new User { name = "waiter1", role_id = 2, password = EncodePassword("123456"), restaurant_id = restaurant.restaurant_id, order_management_permission = true },
            new User { name = "chief1", role_id = 3, password = EncodePassword("123456"), restaurant_id = restaurant.restaurant_id, kitchen_management_permission = true },
            new User { name = "cashier1", role_id = 4, password = EncodePassword("123456"), restaurant_id = restaurant.restaurant_id, payment_management_permission = true }
        };
                _context.Users.AddRange(defaultUsers);
                _context.SaveChanges();

                // ۹. افزودن میز پیش‌فرض
                _context.RestaurantTables.Add(new RestaurantTable
                {
                    TableName = "میز اصلی",
                    Seats = 1,
                    RestaurantId = restaurant.restaurant_id,
                    CreatedAt = DateTime.Now
                });
                _context.SaveChanges();

                // ۱۰. منطق اعطای اشتراک طلایی رایگان برای اولین رستوران
                bool isFirstRestaurantAndFreeTrialGiven = false;
                if (restaurantCount == 0)
                {
                    var goldPlan = _context.SubscriptionPlans
                        .FirstOrDefault(p => p.Name == "طلایی" || p.Id == 4);

                    if (goldPlan != null)
                    {
                        var freeSubscription = new Subscription
                        {
                            RestaurantId = restaurant.restaurant_id,
                            OwnerId = owner.Id,
                            SubscriptionPlanId = goldPlan.Id,
                            SubscriptionPeriod = "3 روز",
                            Status = "Active",
                            StartDate = DateTime.Now,
                            EndDate = DateTime.Now.AddDays(3),
                            PurchaseDate = DateTime.Now,
                            PricePaid = 0,
                            DiscountApplied = 0,
                            PaymentMethod = "FreeTrial",
                            TransactionId = "",
                            IsPaid = true,
                            CafeBazarPurchaseToken = "",
                            CafeBazarOrderId = "",
                            AutoRenew = false,
                            NextRenewalDate = null,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now,
                            CanceledAt = null
                        };

                        _context.Subscriptions.Add(freeSubscription);
                        _context.SaveChanges();
                        isFirstRestaurantAndFreeTrialGiven = true;
                    }
                }

                transaction.Commit();

                string responseMessage = "رستوران جدید با موفقیت ثبت شد.";
                if (isFirstRestaurantAndFreeTrialGiven)
                {
                    responseMessage = "تبریک! رستوران شما با موفقیت ثبت شد و یک اشتراک طلایی 3 روزه رایگان به حساب شما اضافه گردید.";
                }

                return Ok(new
                {
                    success = true,
                    message = responseMessage,
                    restaurant_id = restaurant.restaurant_id,
                    restaurant_code = restaurant.restaurant_code,
                    has_free_trial = isFirstRestaurantAndFreeTrialGiven
                });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return Ok(new
                {
                    success = false,
                    message = "خطا در سرور: " + ex.GetBaseException().Message
                });
            }
        }

       
        private string GenerateUniqueRestaurantCode()
        {
            Random rnd = new Random();
            string code;

            do
            {
                code = rnd.Next(100000, 999999).ToString();
            }
            while (_context.Restaurants.Any(r => r.restaurant_code == code));

            return code;
        }
        private static string EncodePassword(string plainPassword)
        {
            if (string.IsNullOrEmpty(plainPassword)) return null;
            byte[] bytes = Encoding.UTF8.GetBytes(plainPassword);
            return Convert.ToBase64String(bytes);
        }

       
    }
}
 