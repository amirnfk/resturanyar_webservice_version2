using Asp.Versioning;
using global::Resturanyar.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Models.AuthorizationModels;
using resturanyar.Models.CustomerModels;
using resturanyar.Utility;
using Resturanyar.Hubs;
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

        private readonly IHubContext<OrderHub> _hubContext;

        public UserApiController(
            AppDbContext context,
            TokenService tokenService,
            IConfiguration configuration,
            IHubContext<OrderHub> hubContext) // <-- added
        {
            _context = context;
            _tokenService = tokenService;
            _configuration = configuration;
            _hubContext = hubContext;
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
        
        [HttpPost("addfood")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> AddFood([FromForm] FoodItemCreateRequest request)
        {
            try
            {
                // 1. Validate owner from token
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int ownerIdFromToken))
                {
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });
                }

                // 2. Validate required fields
                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(new { success = false, message = "نام غذا الزامی است." });

                if (request.Price <= 0)
                    return BadRequest(new { success = false, message = "قیمت باید بیشتر از صفر باشد." });

                if (request.RestaurantId <= 0)
                    return BadRequest(new { success = false, message = "شناسه رستوران معتبر نیست." });

                if (request.CategoryId <= 0)
                    return BadRequest(new { success = false, message = "دسته‌بندی معتبر نیست." });

                // 3. Verify the restaurant belongs to this owner
                var restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(r => r.restaurant_id == request.RestaurantId && r.owner_id == ownerIdFromToken);
                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

                // 4. Verify category exists and belongs to this restaurant
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId && c.RestaurantId == request.RestaurantId);
                if (category == null)
                    return BadRequest(new { success = false, message = "دسته‌بندی یافت نشد یا غیرفعال است." });

                // 5. Handle image upload (optional)
                string imageUrl = "";
                if (request.Image != null && request.Image.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(request.Image.FileName);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await request.Image.CopyToAsync(stream);
                    }

                    imageUrl = $"/uploads/{uniqueFileName}";
                }

                // 6. Create FoodItem
                var food = new FoodItem
                {
                    Name = request.Name.Trim(),
                    Description = request.Description?.Trim(),
                    ImageUrl = imageUrl,
                    CategoryId = request.CategoryId,
                    Price = request.Price,
                    DiscountPrice = request.DiscountPrice,
                    CostPrice = request.CostPrice,
                    RestaurantId = request.RestaurantId,
                    IsAvailable = request.isAvailable ?? true,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                _context.FoodItems.Add(food);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "آیتم با موفقیت اضافه شد",
                    data = new
                    {
                        food.FoodItemId,
                        food.Name,
                        food.Price,
                        food.ImageUrl,
                        food.CategoryId
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }


        [HttpPut("updatefood/{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateFood(int id, [FromForm] FoodItemCreateRequest request)
        {
            try
            {
                // 1. Owner validation
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int ownerIdFromToken))
                {
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });
                }

                // 2. Find the food item with its restaurant
                var food = await _context.FoodItems
                    
                    .FirstOrDefaultAsync(f => f.FoodItemId == id);
                if (food == null)
                    return NotFound(new { success = false, message = "آیتم غذایی مورد نظر یافت نشد." });

                // 3. Verify owner owns the restaurant
                var restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(r => r.restaurant_id == food.RestaurantId && r.owner_id == ownerIdFromToken);
                if (restaurant == null)
                    return Unauthorized(new { success = false, message = "شما دسترسی به این آیتم ندارید." });

                // 4. Validate category
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId && c.RestaurantId == food.RestaurantId);
                if (category == null)
                    return BadRequest(new { success = false, message = "دسته‌بندی معتبر نیست یا غیرفعال است." });

                // 5. Handle image update
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                if (request.Image != null && request.Image.Length > 0)
                {
                    // Delete old image if exists
                    if (!string.IsNullOrEmpty(food.ImageUrl))
                    {
                        var oldImagePath = Path.Combine(uploadsFolder, Path.GetFileName(food.ImageUrl));
                        if (System.IO.File.Exists(oldImagePath))
                            System.IO.File.Delete(oldImagePath);
                    }

                    var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(request.Image.FileName);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await request.Image.CopyToAsync(stream);
                    }

                    food.ImageUrl = $"/uploads/{uniqueFileName}";
                }
                else if (request.RemoveImage == 2 && !string.IsNullOrEmpty(food.ImageUrl))
                {
                    var oldImagePath = Path.Combine(uploadsFolder, Path.GetFileName(food.ImageUrl));
                    if (System.IO.File.Exists(oldImagePath))
                        System.IO.File.Delete(oldImagePath);

                    food.ImageUrl = "";
                }

                // 6. Update fields
                food.Name = request.Name.Trim();
                food.Description = request.Description?.Trim();
                food.CategoryId = request.CategoryId;
                food.Price = request.Price;
                food.DiscountPrice = request.DiscountPrice;
                food.CostPrice = request.CostPrice;
                food.IsAvailable = request.isAvailable ?? true;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "آیتم با موفقیت ویرایش شد." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpDelete("deleteFood/{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeleteFoodItem(int id)
        {
            try
            {
                // 1. Owner validation
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int ownerIdFromToken))
                {
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });
                }

                // 2. Find the food item
                var item = await _context.FoodItems
                    .FirstOrDefaultAsync(f => f.FoodItemId == id);
                if (item == null)
                    return NotFound(new { success = false, message = "آیتم مورد نظر پیدا نشد." });

                // 3. Verify ownership
                var restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(r => r.restaurant_id == item.RestaurantId && r.owner_id == ownerIdFromToken);
                if (restaurant == null)
                    return Unauthorized(new { success = false, message = "شما دسترسی به این آیتم ندارید." });

                // 4. Soft delete
                item.IsActive = false;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "آیتم با موفقیت غیرفعال شد." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطای غیرمنتظره در سرور: " + ex.Message });
            }
        }

        [HttpGet("getcategoriesbyrestaurant/{restaurantId}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetCategoriesByRestaurant(int restaurantId)
        {
            try
            {
                // 1. Owner validation
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int ownerIdFromToken))
                {
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });
                }

                // 2. Verify restaurant belongs to owner
                var restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId && r.owner_id == ownerIdFromToken);
                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

                // 3. Get categories
                var categories = await _context.Categories
                    .Where(c => c.RestaurantId == restaurantId)
                    .Select(c => new
                    {
                        c.CategoryId,
                        c.CategoryName,
                        c.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    restaurant = new { restaurant.restaurant_id, restaurant.name },
                    categories
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

       
        [HttpGet("gettablesbyrestaurant/{restaurantId}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public IActionResult GetTablesByRestaurant(int restaurantId)
        {
            try
            {
                // 1. Extract owner ID from JWT
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int ownerIdFromToken))
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

                // 2. Verify the restaurant belongs to this owner
                var restaurant = _context.Restaurants
                    .FirstOrDefault(r => r.restaurant_id == restaurantId && r.owner_id == ownerIdFromToken);
                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

                // 3. Retrieve tables
                var tables = _context.RestaurantTables
                    .Where(t => t.RestaurantId == restaurantId)
                    .Select(t => new
                    {
                        t.TableId,
                        t.TableName,
                        t.Seats,
                        t.CreatedAt
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    restaurant = new { restaurant.restaurant_id, restaurant.name },
                    tables
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }


        [HttpPost("createOrder")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                // 1. Owner validation
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int ownerIdFromToken))
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

                // 2. Verify the restaurant belongs to this owner
                var restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(r => r.restaurant_id == request.RestaurantId && r.owner_id == ownerIdFromToken);
                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

                // 3. Validate customer if provided
                if (request.CustomerId.HasValue)
                {
                    var customerExists = await _context.Customers
                        .AnyAsync(c => c.CustomerId == request.CustomerId.Value && c.RestaurantId == request.RestaurantId && c.IsActive);
                    if (!customerExists)
                        return BadRequest(new { success = false, message = "مشتری با این شناسه برای این رستوران یافت نشد." });
                }

                // 4. Create order
                var order = new Order
                {
                    RestaurantId = request.RestaurantId,
                    TableNumber = request.TableNumber,
                    StatusId = request.StatusId,
                    CustomerId = request.CustomerId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    CreatedAtShamsi = DateHelper.ToShamsi(DateTime.Now),
                    UpdatedAtShamsi = DateHelper.ToShamsi(DateTime.Now),
                    Description = request.Description,
                    OrderItems = new List<OrderItem>()
                };

                // 5. Add order items
                foreach (var item in request.Items)
                {
                    var food = await _context.FoodItems.FindAsync(item.FoodItemId);
                    if (food == null)
                        return BadRequest(new { success = false, message = $"آیتم غذایی با شناسه {item.FoodItemId} یافت نشد." });

                    order.OrderItems.Add(new OrderItem
                    {
                        FoodItemId = item.FoodItemId,
                        Quantity = item.Quantity,
                        UnitPrice = food.Price,
                        UnitPriceWithDiscount = food.DiscountPrice ?? food.Price,
                        FoodName = food.Name,
                        FoodImageUrl = food.ImageUrl
                    });
                }

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // 6. Add initial OrderUpdate for the next role (e.g., Chef = 3)
                int? nextRoleId = GetNextRoleId(request.StatusId);
                if (nextRoleId.HasValue)
                {
                    var existingUpdate = await _context.OrderUpdates
                        .FirstOrDefaultAsync(u => u.OrderId == order.OrderId && u.TargetRoleId == nextRoleId.Value);
                    if (existingUpdate != null)
                        existingUpdate.UpdateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    else
                    {
                        _context.OrderUpdates.Add(new OrderUpdate
                        {
                            OrderId = order.OrderId,
                            RestaurantId = order.RestaurantId,
                            TargetRoleId = nextRoleId.Value,
                            UpdateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                // 7. Notify via SignalR
                await _hubContext.Clients.Group(order.RestaurantId.ToString())
                    .SendAsync("ReceiveOrderUpdate", new
                    {
                        orderId = order.OrderId,
                        newStatusId = order.StatusId,
                        message = $"سفارش {order.OrderId} با موفقیت ثبت شد."
                    });

                return Ok(new
                {
                    success = true,
                    message = "سفارش با موفقیت ثبت شد.",
                    orderId = order.OrderId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        // Helper method (same as V1)
        private int? GetNextRoleId(int statusId)
        {
            switch (statusId)
            {
                case 2: return 3;  // Waiting -> Chef
                case 3: return 3;  // Chef
                case 4: return 3;  // Cashier
                case 5: return 2;  // Waiter
                case 6: return 4;  // Owner
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

        [HttpGet("getcustomers/{restaurantId}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public IActionResult GetCustomers(int restaurantId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string search = null)
        {
            try
            {
                // Owner validation
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int ownerIdFromToken))
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

                // Verify restaurant ownership
                var restaurant = _context.Restaurants
                    .FirstOrDefault(r => r.restaurant_id == restaurantId && r.owner_id == ownerIdFromToken);
                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

                var query = _context.Customers
                    .Where(c => c.RestaurantId == restaurantId && c.IsActive);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(c => c.Mobile.Contains(search) ||
                                             (c.FullName != null && c.FullName.Contains(search)));
                }

                var totalCount = query.Count();
                var customers = query
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new
                    {
                        c.CustomerId,
                        c.Mobile,
                        c.FullName,
                        c.Description,
                        c.CreatedAt,
                        c.UpdatedAt
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data = customers,
                    totalCount,
                    currentPage = page,
                    pageSize,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }
       
        
        [HttpPost("addcustomer")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public IActionResult AddCustomer([FromBody] AddCustomerRequest request)
        {
            try
            {
                // Owner validation
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int ownerIdFromToken))
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

                // Verify restaurant ownership
                var restaurant = _context.Restaurants
                    .FirstOrDefault(r => r.restaurant_id == request.RestaurantId && r.owner_id == ownerIdFromToken);
                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

                // Check for existing customer (including inactive)
                var existingCustomer = _context.Customers
                    .FirstOrDefault(c => c.RestaurantId == request.RestaurantId && c.Mobile == request.Mobile);

                if (existingCustomer != null)
                {
                    if (!existingCustomer.IsActive)
                    {
                        // Reactivate and update info
                        existingCustomer.IsActive = true;
                        existingCustomer.FullName = request.FullName;
                        existingCustomer.Description = request.Description;
                        existingCustomer.UpdatedAt = DateTime.Now;
                        _context.SaveChanges();

                        return Ok(new
                        {
                            success = true,
                            message = "مشتری غیرفعال قبلی با موفقیت فعال و ویرایش شد",
                            customerId = existingCustomer.CustomerId,
                            wasReactivated = true
                        });
                    }
                    else
                    {
                        return Ok(new { success = false, message = "این شماره موبایل قبلاً برای این رستوران ثبت شده است." });
                    }
                }

                // Create new customer
                var customer = new Customer
                {
                    RestaurantId = request.RestaurantId,
                    Mobile = request.Mobile,
                    FullName = request.FullName,
                    Description = request.Description,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Customers.Add(customer);
                _context.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "مشتری با موفقیت اضافه شد",
                    customerId = customer.CustomerId,
                    wasReactivated = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        // ===================== Category Management (V2) =====================
        [HttpPost("addcategory")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> AddCategory([FromBody] AddCategoryRequest request)
        {
            try
            {
                // 1. Validate owner from token
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int ownerIdFromToken))
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

                // 2. Verify restaurant belongs to this owner
                var restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(r => r.restaurant_id == request.RestaurantId && r.owner_id == ownerIdFromToken);
                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

                // 3. Check duplicate category name for this restaurant
                bool exists = await _context.Categories.AnyAsync(c =>
                    c.RestaurantId == request.RestaurantId &&
                    c.CategoryName.ToLower().Trim() == request.CategoryName.ToLower().Trim());
                if (exists)
                    return Ok(new { success = false, message = "این دسته‌بندی قبلاً برای این رستوران ثبت شده است." });

                // 4. Create new category
                var category = new Category
                {
                    RestaurantId = request.RestaurantId,
                    CategoryName = request.CategoryName.Trim(),
                   
                    DisplayOrder = 0,               // default order (or compute max+1 if needed)
                    CreatedAt = DateTime.Now
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "دسته‌بندی با موفقیت اضافه شد",
                    category_id = category.CategoryId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpPut("editcategory")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> EditCategory([FromBody] EditCategoryRequest request)
        {
            try
            {
                // 1. Validate owner from token
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int ownerIdFromToken))
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

                // 2. Verify restaurant belongs to this owner
                var restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(r => r.restaurant_id == request.RestaurantId && r.owner_id == ownerIdFromToken);
                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

                // 3. Find the category
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId && c.RestaurantId == request.RestaurantId);
                if (category == null)
                    return NotFound(new { success = false, message = "دسته‌بندی یافت نشد یا متعلق به این رستوران نیست." });

                // 4. Check duplicate name (excluding itself)
                bool duplicate = await _context.Categories.AnyAsync(c =>
                    c.RestaurantId == request.RestaurantId &&
                    c.CategoryId != request.CategoryId &&
                    c.CategoryName.ToLower().Trim() == request.CategoryName.ToLower().Trim());
                if (duplicate)
                    return Ok(new { success = false, message = "این نام دسته‌بندی قبلاً در این رستوران ثبت شده است." });

                // 5. Update and save
                category.CategoryName = request.CategoryName.Trim();
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "دسته‌بندی با موفقیت ویرایش شد." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpDelete("deletecategory")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeleteCategory([FromBody] DeleteCategoryRequest request)
        {
            try
            {
                // 1. Validate owner from token
                var nameIdentifierClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(nameIdentifierClaim) || !int.TryParse(nameIdentifierClaim, out int ownerIdFromToken))
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

                // 2. Verify restaurant ownership
                var restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(r => r.restaurant_id == request.RestaurantId && r.owner_id == ownerIdFromToken);
                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

                // 3. Find category
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId && c.RestaurantId == request.RestaurantId);
                if (category == null)
                    return NotFound(new { success = false, message = "دسته‌بندی یافت نشد یا متعلق به این رستوران نیست." });

                // 4. Check if any active food items exist in this category
                bool hasFood = await _context.FoodItems.AnyAsync(f => f.CategoryId == category.CategoryId );
                if (hasFood)
                    return Ok(new { success = false, message = "امکان حذف دسته‌بندی وجود ندارد، چون هنوز غذاهای فعال در این دسته ثبت شده‌اند." });
                _context.Categories.Remove(category);
                _context.SaveChanges();


                return Ok(new { success = true, message = "دسته‌بندی با موفقیت غیرفعال شد." });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
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
    }
}
