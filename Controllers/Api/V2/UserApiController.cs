using Asp.Versioning;
using global::Resturanyar.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Models.AuthorizationModels;
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
        private readonly IConfiguration _configuration;

        public UserApiController(AppDbContext context,TokenService tokenService, IConfiguration configuration)
        {
            _context = context;
            _tokenService = tokenService;
            _configuration = configuration;
        }
        [AllowAnonymous]
        [HttpPost("generate-token")]
        public IActionResult GenerateTokenAction([FromBody] TokenRequestModel request)
        {
            try
            {
                var owner = _context.Owners.FirstOrDefault(c => c.Phone == request.PhoneNumber);
                if (owner == null) return NotFound(new { success = false, message = "کاربری یافت نشد." });

                string jwtToken = _tokenService.GenerateOwnerToken(owner);
                string refreshTokenString = _tokenService.GenerateRefreshToken();

                // Expirations
                var jwtDays = _configuration.GetValue<int>("JwtSettings:JwtExpirationDays");
                var refreshDays = _configuration.GetValue<int>("JwtSettings:RefreshExpirationDays");
                var jwtExpiration = DateTime.UtcNow.AddDays(jwtDays > 0 ? jwtDays : 1);
                var refreshExpiration = DateTime.UtcNow.AddDays(refreshDays > 0 ? refreshDays : 30);

                var newRefreshToken = new RefreshToken
                {
                    Token = refreshTokenString,
                    ExpiryTime = refreshExpiration,
                    OwnerId = owner.Id

                };
                _context.RefreshTokens.Add(newRefreshToken);
                _context.SaveChanges();

                // ❌ REMOVE this entire block:
                // Response.Cookies.Append("X-Access-Token", jwtToken, new CookieOptions { ... });

                // ✅ Keep only the JSON response for both Web & Mobile
                return Ok(new
                {
                    success = true,
                    token = jwtToken,           // Store this in localStorage
                    refreshToken = refreshTokenString, // Store this in localStorage
                    expiresAt = jwtExpiration
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا: " + ex.Message });
            }
        }


        [AllowAnonymous]
        [HttpPost("refresh")]
        public IActionResult Refresh([FromBody] RefreshTokenRequestModel request)
        {
            var owner = _context.Owners.FirstOrDefault(c => c.Phone == request.PhoneNumber);
            if (owner == null) return Unauthorized(new { success = false, message = "کاربر نامعتبر است." });

            // 1. Find the specific token for THIS device
            var existingToken = _context.RefreshTokens.FirstOrDefault(rt =>
                rt.OwnerId == owner.Id &&
                rt.Token == request.RefreshToken);

            // 2. Validate token existence and expiration
            if (existingToken == null || existingToken.ExpiryTime <= DateTime.UtcNow)
            {
                // If expired, clean it up from the DB
                if (existingToken != null)
                {
                    _context.RefreshTokens.Remove(existingToken);
                    _context.SaveChanges();
                }
                return Unauthorized(new { success = false, message = "نشست منقضی شده است. لطفا مجدد وارد شوید." });
            }

            // 3. Generate new tokens (Token Rotation)
            string newJwtToken = _tokenService.GenerateOwnerToken(owner);
            string newRefreshTokenString = _tokenService.GenerateRefreshToken();

            // 4. Update the current session with the new refresh token
            existingToken.Token = newRefreshTokenString;
            var refreshDays = _configuration.GetValue<int>("JwtSettings:RefreshExpirationDays");
            existingToken.ExpiryTime = DateTime.UtcNow.AddDays(refreshDays > 0 ? refreshDays : 30);
            var jwtDays = _configuration.GetValue<int>("JwtSettings:JwtExpirationDays");
            var jwtExpiration = DateTime.UtcNow.AddDays(jwtDays > 0 ? jwtDays : 1);
            _context.SaveChanges();

            return Ok(new
            {
                success = true,
                token = newJwtToken,
                refreshToken = newRefreshTokenString,
                expiresAt = jwtExpiration
            });
        }

        [HttpPost("logout")]
        public IActionResult Logout([FromBody] string currentRefreshToken)
        {
             
            var tokenRecord = _context.RefreshTokens.FirstOrDefault(rt => rt.Token == currentRefreshToken);

            if (tokenRecord != null)
            {
                _context.RefreshTokens.Remove(tokenRecord);
                _context.SaveChanges();
            }

             

            return Ok(new { success = true, message = "با موفقیت خارج شد." });
        }
        //[AllowAnonymous]
        //[HttpPost("generate-token")]
        //public IActionResult GenerateTokenAction([FromBody] TokenRequestModel request)
        //{
        //    try
        //    {
        //        if (request == null || string.IsNullOrEmpty(request.PhoneNumber))
        //        {
        //            return BadRequest(new { success = false, message = "شماره تلفن الزامی است." });
        //        }

        //        var owner = _context.Owners.FirstOrDefault(c => c.Phone == request.PhoneNumber);
        //        if (owner == null)
        //        {
        //            return NotFound(new { success = false, message = "کاربری با این شماره تلفن یافت نشد." });
        //        }

        //        //bool hasRestaurant = _context.Restaurants.Any(r => r.owner_id == owner.Id);
        //        //if (!hasRestaurant)
        //        //{
        //        //    return BadRequest(new { success = false, message = "خطا: حساب شما به هیچ رستورانی متصل نیست یا شما مالک رستورانی نیستید." });
        //        //}

        //        string jwtToken = _tokenService.GenerateOwnerToken(owner);
        //        var expirationTime = DateTime.UtcNow.AddDays(1); // زمان انقضا باید با TokenService هماهنگ باشد

        //        // ۲. استراتژی امن برای وب (ست کردن کوکی HttpOnly)
        //        // مرورگر وب این کوکی را ذخیره میکند و جاوااسکریپت به آن دسترسی ندارد (ضد XSS)
        //        Response.Cookies.Append("X-Access-Token", jwtToken, new CookieOptions
        //        {
        //            HttpOnly = true,
        //            Secure = true, // حتما در پروداکشن فعال باشد (نیاز به HTTPS)
        //            SameSite = SameSiteMode.Strict,
        //            Expires = expirationTime
        //        });

        //        // ۳. خروجی مشترک (موبایل از فیلد token استفاده میکند و وب میتواند آن را نادیده بگیرد)
        //        return Ok(new
        //        {
        //            success = true,
        //            message = "خوش آمدید. هویت شما به عنوان مالک رستوران تایید شد.",
        //            token = jwtToken, // برای مصرف در موبایل
        //            expiresAt = expirationTime
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { success = false, message = "خطایی در سرور رخ داد: " + ex.Message });
        //    }
        //}

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

        [HttpPost("edituser")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] // Requires valid JWT
        public IActionResult EditUser([FromBody] EditUserRequest request)
        {
            try
            {
                // 1. Extract the owner ID from the JWT token
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int ownerIdFromToken))
                {
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });
                }

                // 2. Security Check: Ensure the restaurant belongs to the authenticated owner
                bool isOwnerValid = _context.Restaurants.Any(r => r.restaurant_id == request.restaurant_id && r.owner_id == ownerIdFromToken);
                if (!isOwnerValid)
                {
                    return Unauthorized(new { success = false, message = "شما دسترسی لازم برای ویرایش کاربران این رستوران را ندارید." });
                }

                // 3. Find the user[cite: 5]
                var user = _context.Users.FirstOrDefault(u => u.user_id == request.user_id && u.restaurant_id == request.restaurant_id);

                if (user == null)
                {
                    return NotFound(new { success = false, message = "کاربر یافت نشد یا متعلق به این رستوران نیست" });
                }

                // 4. Update properties[cite: 5]
                user.name = request.name;
                user.role_id = request.role_id;
                user.password = EncodePassword(request.password); // Relies on the EncodePassword method in your V2 controller[cite: 4, 5]

                // Update permissions if provided[cite: 5]
                if (request.order_management_permission.HasValue)
                    user.order_management_permission = request.order_management_permission.Value;

                if (request.kitchen_management_permission.HasValue)
                    user.kitchen_management_permission = request.kitchen_management_permission.Value;

                if (request.payment_management_permission.HasValue)
                    user.payment_management_permission = request.payment_management_permission.Value;

                _context.SaveChanges();  

        return Ok(new { success = true, message = "کاربر با موفقیت ویرایش شد" });  
    }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.GetBaseException().Message });
            }
        }


        // Helper method missing from V2
        private static string DecodePassword(string encodedPassword)
        {
            if (string.IsNullOrEmpty(encodedPassword)) return null;
            byte[] bytes = Convert.FromBase64String(encodedPassword);
            return Encoding.UTF8.GetString(bytes);
        }

        [HttpGet("getusersbyrestaurant/{restaurantId}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public IActionResult GetUsersByRestaurant(int restaurantId)
        {
            try
            {
                // Extract owner ID from JWT[cite: 6]
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int ownerIdFromToken))
                {
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });
                }

                // Verify the restaurant belongs to this owner[cite: 6]
                var restaurant = _context.Restaurants.FirstOrDefault(r => r.restaurant_id == restaurantId && r.owner_id == ownerIdFromToken);
                if (restaurant == null)
                {
                    return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید" });
                }

                var users = _context.Users
                    .Where(u => u.restaurant_id == restaurantId)
                    .Include(u => u.Role)
                    .Select(u => new
                    {
                        u.user_id,
                        u.name,
                        password = DecodePassword(u.password), // Decodes password based on V1 logic
                        role_id = u.role_id,
                        role_name = u.Role.role_name,
                        order_management_permission = u.order_management_permission,
                        kitchen_management_permission = u.kitchen_management_permission,
                        payment_management_permission = u.payment_management_permission
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    restaurant = new { restaurant.restaurant_id, restaurant.name },
                    users = users
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.GetBaseException().Message });
            }
        }

        [HttpPost("adduser")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public IActionResult AddUser([FromBody] AddUserRequest request)
        {
            try
            {
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int ownerIdFromToken))
                {
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });
                }

               
                var restaurant = _context.Restaurants.FirstOrDefault(r => r.restaurant_id == request.restaurant_id && r.owner_id == ownerIdFromToken);
                if (restaurant == null)
                {
                    return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید" });
                }

                var existingUser = _context.Users
                    .FirstOrDefault(u => u.name == request.name && u.restaurant_id == request.restaurant_id);

                if (existingUser != null)
                {
                    return Ok(new { success = false, message = "کاربری با این نام برای این رستوران قبلاً ثبت شده است" });
                }

                var user = new User
                {
                    name = request.name,
                    role_id = request.role_id,
                    password = EncodePassword(request.password),
                    restaurant_id = request.restaurant_id
                };

                // Apply default permissions based on V1 logic
                switch (request.role_id)
                {
                    case 1:
                        user.order_management_permission = true;
                        user.kitchen_management_permission = true;
                        user.payment_management_permission = true;
                        break;
                    case 2:
                        user.order_management_permission = true;
                        user.kitchen_management_permission = false;
                        user.payment_management_permission = false;
                        break;
                    case 3:
                        user.order_management_permission = false;
                        user.kitchen_management_permission = true;
                        user.payment_management_permission = false;
                        break;
                    case 4:
                        user.order_management_permission = false;
                        user.kitchen_management_permission = false;
                        user.payment_management_permission = true;
                        break;
                }

               
                if (request.order_management_permission.HasValue) user.order_management_permission = request.order_management_permission.Value;
                if (request.kitchen_management_permission.HasValue) user.kitchen_management_permission = request.kitchen_management_permission.Value;
                if (request.payment_management_permission.HasValue) user.payment_management_permission = request.payment_management_permission.Value;

                _context.Users.Add(user);
                _context.SaveChanges();

                return Ok(new { success = true, message = "کاربر با موفقیت ثبت شد" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.GetBaseException().Message });
            }
        }

        [HttpDelete("deleteuser/{restaurantId}/{userId}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public IActionResult DeleteUser(int restaurantId, int userId)
        {
            try
            {
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int ownerIdFromToken))
                {
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });
                }

              
                bool isOwnerValid = _context.Restaurants.Any(r => r.restaurant_id == restaurantId && r.owner_id == ownerIdFromToken);
                if (!isOwnerValid)
                {
                    return Unauthorized(new { success = false, message = "شما دسترسی لازم برای حذف کاربران این رستوران را ندارید." });
                }

                var user = _context.Users.FirstOrDefault(u => u.user_id == userId && u.restaurant_id == restaurantId);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "کاربر یافت نشد یا متعلق به این رستوران نیست" });
                }

                _context.Users.Remove(user);
                _context.SaveChanges();

                return Ok(new { success = true, message = "کاربر با موفقیت حذف شد" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.GetBaseException().Message });
            }
        }

        [HttpGet("getrestaurantcode/{restaurantId}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public IActionResult GetRestaurantCode(int restaurantId)
        {
            try
            {
                // 1. Extract owner ID from JWT token
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int ownerIdFromToken))
                {
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });
                }

                // 2. Verify the restaurant belongs to this owner
                var restaurant = _context.Restaurants
                    .FirstOrDefault(r => r.restaurant_id == restaurantId && r.owner_id == ownerIdFromToken);
                if (restaurant == null)
                {
                    return NotFound(new { success = false, message = "رستوران پیدا نشد یا شما دسترسی ندارید." });
                }

                // 3. Return the restaurant code
                return Ok(new { success = true, code = restaurant.restaurant_code });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطای سرور: " + ex.Message });
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
