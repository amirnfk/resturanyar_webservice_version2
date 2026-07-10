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

                //bool hasRestaurant = _context.Restaurants.Any(r => r.owner_id == owner.Id);
                //if (!hasRestaurant)
                //{
                //    return BadRequest(new { success = false, message = "خطا: حساب شما به هیچ رستورانی متصل نیست یا شما مالک رستورانی نیستید." });
                //}

                string jwtToken = _tokenService.GenerateOwnerToken(owner);
                var expirationTime = DateTime.UtcNow.AddDays(1); // زمان انقضا باید با TokenService هماهنگ باشد

                // ۲. استراتژی امن برای وب (ست کردن کوکی HttpOnly)
                // مرورگر وب این کوکی را ذخیره میکند و جاوااسکریپت به آن دسترسی ندارد (ضد XSS)
                Response.Cookies.Append("X-Access-Token", jwtToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true, // حتما در پروداکشن فعال باشد (نیاز به HTTPS)
                    SameSite = SameSiteMode.Strict,
                    Expires = expirationTime
                });

                // ۳. خروجی مشترک (موبایل از فیلد token استفاده میکند و وب میتواند آن را نادیده بگیرد)
                return Ok(new
                {
                    success = true,
                    message = "خوش آمدید. هویت شما به عنوان مالک رستوران تایید شد.",
                    token = jwtToken, // برای مصرف در موبایل
                    expiresAt = expirationTime
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
