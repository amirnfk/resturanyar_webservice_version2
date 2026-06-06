using Microsoft.AspNetCore.Mvc;
using Resturanyar.Data;
using resturanyar.Models;
using System.Text;

 
 
using resturanyar.Models;
using Resturanyar.Data;
 
using Microsoft.EntityFrameworkCore;
using System;
using System.Text;
using resturanyar.Utility;
using System.Data.SqlClient;
using Azure.Core;
using Microsoft.AspNetCore.SignalR;
using Resturanyar.Hubs;
using Microsoft.EntityFrameworkCore;

using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
namespace Resturanyar.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]

    public class UserApiController : ControllerBase
    {
        private readonly IHubContext<OrderHub> _hubContext;

        private readonly AppDbContext _context;

        public UserApiController(AppDbContext context, IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;

        }
        private static string EncodePassword(string plainPassword)
        {
            if (string.IsNullOrEmpty(plainPassword)) return null;
            byte[] bytes = Encoding.UTF8.GetBytes(plainPassword);
            return Convert.ToBase64String(bytes);
        }

        private static string DecodePassword(string encodedPassword)
        {
            if (string.IsNullOrEmpty(encodedPassword)) return null;
            byte[] bytes = Convert.FromBase64String(encodedPassword);
            return Encoding.UTF8.GetString(bytes);
        }
        public class AddOwnerRequest
        {
            public string Name { get; set; }
            public string Phone { get; set; }
            public string Password { get; set; }
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



      

        [HttpPost("addrestaurant")]
        public IActionResult AddRestaurant(AddRestaurantRequest request)
        {
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                var owner = _context.Owners.Find(request.owner_id);
                if (owner == null)
                    return NotFound(new { success = false, message = "مالک با این شناسه یافت نشد" });

                // 🟡 بررسی وجود اشتراک طلایی فعال
                bool hasActiveGold = _context.Subscriptions
                    .Include(s => s.SubscriptionPlan)
                    .Any(s =>
                        s.OwnerId == request.owner_id &&
                        s.Status == "Active" &&
                        s.EndDate > DateTime.Now &&
                        s.SubscriptionPlan.Name == "طلایی"
                    );

                // 🟠 بررسی تعداد رستوران‌های فعلی کاربر
                int restaurantCount = _context.Restaurants.Count(r => r.owner_id == request.owner_id);

                // 🔴 اگر کاربر رستوران دارد ولی اشتراک طلایی فعال ندارد
                if (restaurantCount > 0 && !hasActiveGold)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "برای افزودن رستوران جدید، باید حداقل یک اشتراک طلایی فعال داشته باشید."
                    });
                }

                // ⚙️ بررسی تکراری نبودن نام رستوران برای همین مالک
                bool isDuplicate = _context.Restaurants.Any(r =>
                    r.owner_id == request.owner_id &&
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

                // ✅ ساخت رستوران جدید
                var restaurant = new Restaurant
                {
                    name = request.name.Trim(),
                    owner_id = request.owner_id,
                    restaurant_code = GenerateUniqueRestaurantCode(),
                    PublicMenuToken = Guid.NewGuid().ToString("N"),
                };

                _context.Restaurants.Add(restaurant);
                _context.SaveChanges();

                // 👥 افزودن کاربران پیش‌فرض
                var defaultUsers = new List<User>
        {
            new User { name = "waiter1", role_id = 2, password = EncodePassword("123456"), restaurant_id = restaurant.restaurant_id, order_management_permission = true },
            new User { name = "chief1", role_id = 3, password = EncodePassword("123456"), restaurant_id = restaurant.restaurant_id, kitchen_management_permission = true },
            new User { name = "cashier1", role_id = 4, password = EncodePassword("123456"), restaurant_id = restaurant.restaurant_id, payment_management_permission = true }
        };

                _context.Users.AddRange(defaultUsers);
                _context.SaveChanges();

                // 🍽️ افزودن میز پیش‌فرض
                _context.RestaurantTables.Add(new RestaurantTable
                {
                    TableName = "میز اصلی",
                    Seats = 1,
                    RestaurantId = restaurant.restaurant_id,
                    CreatedAt = DateTime.Now
                });
                _context.SaveChanges();

                transaction.Commit();

                return Ok(new
                {
                    success = true,
                    message = "رستوران جدید با موفقیت ثبت شد.",
                    restaurant_id = restaurant.restaurant_id,
                    restaurant_code = restaurant.restaurant_code
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


        [HttpGet("getrestaurantsbyowner/{ownerId}")]
        public IActionResult GetRestaurantsByOwner(int ownerId)
        {
            try
            {
                // Check if the owner exists
                var owner = _context.Owners.Find(ownerId);
                if (owner == null)
                {
                    return NotFound(new { success = false, message = "مالک با این شناسه یافت نشد" });
                }

                // Get restaurants related to the owner
                var restaurants = _context.Restaurants
                    .Where(r => r.owner_id == ownerId)
                    .Select(r => new
                    {
                        r.restaurant_id,
                        r.name
                    })
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
                string fullErrorMessage = ex.Message;
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    fullErrorMessage += " --> " + inner.Message;
                    inner = inner.InnerException;
                }

                return Ok(new
                {
                    success = false,
                    message = "خطا در سرور: " + fullErrorMessage
                });
            }
        }
        [HttpPost("owner_login")]
        public IActionResult Login([FromBody] OwnerLoginRequest request)
        {
            try
            {



                 
                var response = new LoginResponse
                {
                    success = false,

                    message = "",

                    owner_id = null
                };

               




               
                var owner = _context.Owners.FirstOrDefault(o => o.Phone == request.Phone);
                if (owner == null)
                {
                    response.message = "شماره تلفن یافت نشد";
                    return Ok(response);
                }

               
                if (DecodePassword(owner.Password) != request.Password)
                {
                    response.message = "رمز عبور برای این شماره تلفن نادرست است";
                    return Ok(response);
                }

                
                response.success = true;
                response.owner_id = owner.Id;

                 


                response.message = "ورود با موفقیت انجام شد";


                return Ok(response);
            }
            catch (Exception ex)
            {
                string fullErrorMessage = ex.Message;
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    fullErrorMessage += " --> " + inner.Message;
                    inner = inner.InnerException;
                }

                return Ok(new LoginResponse
                {
                    success = false,
                    message = "خطا در سرور: " + fullErrorMessage,



                });
            }
        }

        // مدل ریسپانس واحد
        public class LoginResponse
        {
            public bool success { get; set; }

            public string message { get; set; }

            public int? owner_id { get; set; }
        }

        // مدل کانفیگ آپدیت
        public class UpdateConfig
        {
            public string ForceVersion { get; set; }
            public string SoftVersion { get; set; }
            public string UpdateUrl { get; set; }
            public string Message { get; set; }
        }



        [HttpPost("checkVersion")]
        public IActionResult CheckVersion([FromBody] VersionCheckRequest request, [FromServices] IConfiguration config)
        {
            try
            {
                var updateConfig = config.GetSection("UpdateConfig").Get<UpdateConfig>();
                var clientVersion = request.Version;

                // مدل ریسپانس
                var response = new VersionCheckResponse
                {
                    forceUpdate = false,
                    softUpdate = false,
                    message = "",
                    updateUrl = updateConfig.UpdateUrl
                };

                // بررسی نسخه اجباری
                if (string.Compare(clientVersion, updateConfig.ForceVersion) < 0)
                {
                    response.forceUpdate = true;
                    response.message = "لطفاً اپلیکیشن را به آخرین نسخه بروزرسانی کنید.";
                    return Ok(response);
                }

                // بررسی نسخه نرم (Soft Update)
                if (string.Compare(clientVersion, updateConfig.SoftVersion) < 0)
                {
                    response.softUpdate = true;
                    response.message = updateConfig.Message;
                }

                if (!response.forceUpdate && !response.softUpdate)
                {
                    response.message = "نسخه اپلیکیشن شما به‌روز است.";
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                string fullErrorMessage = ex.Message;
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    fullErrorMessage += " --> " + inner.Message;
                    inner = inner.InnerException;
                }

                return Ok(new VersionCheckResponse
                {
                    forceUpdate = false,
                    softUpdate = false,
                    message = "خطا در سرور: " + fullErrorMessage,
                    updateUrl = config.GetSection("UpdateConfig:UpdateUrl").Value
                });
            }
        }

        [HttpPost("getOwnerInfo")]
        public IActionResult getOwnerInfo(OwnerLoginRequest request)
        {
            try
            {
                // Check if an owner with the provided phone number exists
                var owner = _context.Owners.FirstOrDefault(o => o.Phone == request.Phone);

                if (owner == null)
                {
                    // Phone number does not exist
                    return Ok(new
                    {
                        success = false,
                        message = "شماره تلفن یافت نشد"
                    });
                }

                // Check if the password matches
                if (DecodePassword(owner.Password) != request.Password)
                {
                    // Phone exists, but password is incorrect
                    return Ok(new
                    {
                        success = false,
                        message = "رمز عبور برای این شماره تلفن نادرست است"
                    });
                }

                // Successful login
                return Ok(new
                {
                    success = true,
                    message = "ورود با موفقیت انجام شد",
                    owner_name = owner.Name,
                    owner_phone = request.Phone
                });
            }
            catch (Exception ex)
            {
                string fullErrorMessage = ex.Message;
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    fullErrorMessage += " --> " + inner.Message;
                    inner = inner.InnerException;
                }

                return Ok(new
                {
                    success = false,
                    message = "خطا در سرور: " + fullErrorMessage
                });
            }
        }
        [HttpGet("getusersbyrestaurant/{restaurantId}")]
        public IActionResult GetUsersByRestaurant(int restaurantId)
        {
            try
            {
                var restaurant = _context.Restaurants.Find(restaurantId);
                if (restaurant == null)
                {
                    return NotFound(new { success = false, message = "رستوران یافت نشد" });
                }

                var users = _context.Users
                    .Where(u => u.restaurant_id == restaurantId)
                    .Include(u => u.Role)
                    .Select(u => new
                    {
                        u.user_id,
                        u.name,
                        password = DecodePassword(u.password), // ⚠️ هش پسورد در تولید
                        role_id = u.role_id,
                        role_name = u.Role.role_name,
                        // پرمیشن‌ها
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
                string fullErrorMessage = ex.Message;
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    fullErrorMessage += " --> " + inner.Message;
                    inner = inner.InnerException;
                }

                return StatusCode(500, new
                {
                    success = false,
                    message = "خطا در سرور: " + fullErrorMessage
                });
            }
        }




        [HttpDelete("deleteuser/{restaurantId}/{userId}")]
        public IActionResult DeleteUser(int restaurantId, int userId)
        {
            try
            {
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
                string fullErrorMessage = ex.Message;
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    fullErrorMessage += " --> " + inner.Message;
                    inner = inner.InnerException;
                }

                return Ok(new { success = false, message = "خطا در سرور: " + fullErrorMessage });
            }
        }
        [HttpGet("getrestaurantcode/{restaurantId}")]
        public IActionResult GetRestaurantCode(int restaurantId)
        {
            try
            {
                var restaurant = _context.Restaurants
                    .Where(r => r.restaurant_id == restaurantId)
                    .Select(r => new { r.restaurant_code })
                    .FirstOrDefault();

                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران پیدا نشد." });

                return Ok(new { success = true, code = restaurant.restaurant_code });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطای سرور: " + ex.Message });
            }
        }
        [HttpGet("getrestaurantpublicmenutoken/{restaurantId}")]
        public IActionResult GetRestaurantPublicMenuToken(int restaurantId)
        {
            try
            {
                var restaurant = _context.Restaurants
                    .Where(r => r.restaurant_id == restaurantId)
                    .Select(r => new { r.PublicMenuToken })
                    .FirstOrDefault();

                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران پیدا نشد." });

                return Ok(new { success = true, code = restaurant.PublicMenuToken });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطای سرور: " + ex.Message });
            }
        }

        [HttpPost("edituser")]
        public IActionResult EditUser(EditUserRequest request)
        {
            try
            {
                var user = _context.Users
                    .FirstOrDefault(u => u.user_id == request.user_id && u.restaurant_id == request.restaurant_id);

                if (user == null)
                {
                    return NotFound(new { success = false, message = "کاربر یافت نشد یا متعلق به این رستوران نیست" });
                }

                user.name = request.name;
                user.role_id = request.role_id;
                user.password = EncodePassword(request.password);
                // ویرایش پرمیشن‌ها اگر مقدار ارسال شده باشد
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
                string fullErrorMessage = ex.Message;
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    fullErrorMessage += " --> " + inner.Message;
                    inner = inner.InnerException;
                }

                return StatusCode(500, new
                {
                    success = false,
                    message = "خطا در سرور: " + fullErrorMessage
                });
            }
        }



        [HttpPost("login")]
        public IActionResult LoginUser(LoginUserRequest request)
        {
            try
            {
                // پیدا کردن رستوران بر اساس کد
                var restaurant = _context.Restaurants
                    .FirstOrDefault(r => r.restaurant_code == request.restaurant_code);

                if (restaurant == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "کد رستوران معتبر نیست"
                    });
                }

                // پیدا کردن کاربر
                var user = _context.Users
       .FirstOrDefault(u =>
           u.name == request.name &&
           u.restaurant_id == restaurant.restaurant_id
       );

                if (user == null || DecodePassword(user.password) != request.password)
                {
                    return Ok(new { success = false, message = "کاربری با این مشخصات یافت نشد" });
                }

                // بازگرداندن اطلاعات کاربر همراه با پرمیشن‌ها
                return Ok(new
                {
                    success = true,
                    message = "ورود موفقیت‌آمیز بود",
                    user = new
                    {
                        user_id = user.user_id,
                        name = user.name,
                        role = user.role_id,
                        restaurant_id = user.restaurant_id,
                        restaurant_code = restaurant.restaurant_code,
                        restaurant_name = restaurant.name,
                        // پرمیشن‌ها
                        order_management_permission = user.order_management_permission,
                        kitchen_management_permission = user.kitchen_management_permission,
                        payment_management_permission = user.payment_management_permission
                    }
                });
            }
            catch (Exception ex)
            {
                string fullErrorMessage = ex.Message;
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    fullErrorMessage += " --> " + inner.Message;
                    inner = inner.InnerException;
                }

                return Ok(new
                {
                    success = false,
                    message = "خطا در سرور: " + fullErrorMessage
                });
            }
        }




        [HttpPost("adduser")]
        public IActionResult AddUser(AddUserRequest request)
        {
            try
            {
                // بررسی وجود رستوران
                var restaurant = _context.Restaurants.Find(request.restaurant_id);
                if (restaurant == null)
                {
                    return NotFound(new { success = false, message = "رستوران یافت نشد" });
                }

                // جلوگیری از ایجاد کاربر تکراری برای همان رستوران
                var existingUser = _context.Users
                    .FirstOrDefault(u => u.name == request.name && u.restaurant_id == request.restaurant_id);

                if (existingUser != null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "کاربری با این نام برای این رستوران قبلاً ثبت شده است"
                    });
                }

                // ایجاد کاربر جدید
                var user = new User
                {
                    name = request.name,
                    role_id = request.role_id,
                    password = EncodePassword(request.password), // ⚠️ در تولید، پسورد را هش کنید
                    restaurant_id = request.restaurant_id
                };

                // تنظیم پیش‌فرض بر اساس نقش
                switch (request.role_id)
                {
                    case 1: // Admin
                        user.order_management_permission = true;
                        user.kitchen_management_permission = true;
                        user.payment_management_permission = true;
                        break;
                    case 2: // Waiter
                        user.order_management_permission = true;
                        user.kitchen_management_permission = false;
                        user.payment_management_permission = false;
                        break;
                    case 3: // Chef
                        user.order_management_permission = false;
                        user.kitchen_management_permission = true;
                        user.payment_management_permission = false;
                        break;
                    case 4: // Cashier
                        user.order_management_permission = false;
                        user.kitchen_management_permission = false;
                        user.payment_management_permission = true;
                        break;
                    default:
                        user.order_management_permission = false;
                        user.kitchen_management_permission = false;
                        user.payment_management_permission = false;
                        break;
                }

                // اگر کاربر در request پرمیشن ارسال کرده باشه، مقدار اون اعمال میشه
                if (request.order_management_permission.HasValue)
                    user.order_management_permission = request.order_management_permission.Value;
                if (request.kitchen_management_permission.HasValue)
                    user.kitchen_management_permission = request.kitchen_management_permission.Value;
                if (request.payment_management_permission.HasValue)
                    user.payment_management_permission = request.payment_management_permission.Value;


                // اگر می‌خوای از request هم مقداردهی دستی امکان‌پذیر باشه:
                // user.order_management_permission = request.order_management_permission ?? user.order_management_permission;

                _context.Users.Add(user);
                _context.SaveChanges();

                return Ok(new { success = true, message = "کاربر با موفقیت ثبت شد" });
            }
            catch (Exception ex)
            {
                string fullErrorMessage = ex.Message;
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    fullErrorMessage += " --> " + inner.Message;
                    inner = inner.InnerException;
                }

                return Ok(new
                {
                    success = false,
                    message = "خطا در سرور: " + fullErrorMessage
                });
            }
        }

        [HttpPost("checkphone")]
        public IActionResult CheckPhone([FromBody] string phone)
        {
            try
            {
                var isRegistered = _context.Owners
                    .Any(u => u.Phone == phone);

                return Ok(new
                {
                    success = true,
                    isRegistered = isRegistered
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "خطا در سرور: " + ex.Message
                });
            }
        }


        [HttpPost("addowner")]
        public IActionResult AddOwner(AddOwnerRequest request)
        {


            try
            {

                var owner = new Owner
                {
                    Name = request.Name,
                    Phone = request.Phone,
                    Password = EncodePassword(request.Password)
                };
                var existingUser = _context.Owners
                   .FirstOrDefault(u => u.Phone == request.Phone);

                if (existingUser != null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "این شماره تلفن قبلاً ثبت شده است"
                    });
                }
                _context.Owners.Add(owner);
                _context.SaveChanges(); // بعد از این خط owner.Id مقداردهی می‌شود

                return Ok(new
                {
                    success = true,
                    message = "کاربر با موفقیت ثبت شد",
                    owner_id = owner.Id
                });
            }
            catch (Exception ex)
            {
                string fullErrorMessage = ex.Message;
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    fullErrorMessage += " --> " + inner.Message;
                    inner = inner.InnerException;
                }

                return Ok(new
                {
                    success = false,
                    message = "خطا در سرور: " + fullErrorMessage
                });
            }
        }
        [HttpPost("changepassword")]
        public IActionResult ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                // Find owner by phone
                var owner = _context.Owners.FirstOrDefault(o => o.Phone == request.Phone);

                if (owner == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "کاربری با این شماره تلفن یافت نشد"
                    });
                }

                // Update password
                owner.Password = EncodePassword(request.NewPassword);
                _context.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "رمز عبور با موفقیت تغییر یافت"
                });
            }
            catch (Exception ex)
            {
                string fullErrorMessage = ex.Message;
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    fullErrorMessage += " --> " + inner.Message;
                    inner = inner.InnerException;
                }

                return Ok(new
                {
                    success = false,
                    message = "خطا در سرور: " + fullErrorMessage
                });
            }
        }


        [HttpPost("addfood")]
        public async Task<IActionResult> AddFood([FromForm] FoodItemCreateRequest request)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(new ApiResponse<string> { Success = false, Message = "نام غذا الزامی است." });

                if (request.Price <= 0)
                    return BadRequest(new ApiResponse<string> { Success = false, Message = "قیمت باید بیشتر از صفر باشد." });

                if (request.RestaurantId <= 0)
                    return BadRequest(new ApiResponse<string> { Success = false, Message = "شناسه رستوران معتبر نیست." });

                if (request.CategoryId <= 0)
                    return BadRequest(new ApiResponse<string> { Success = false, Message = "دسته‌بندی معتبر نیست." });

                // Check that category exists and is active
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId && c.RestaurantId == request.RestaurantId  );
                if (category == null)
                    return BadRequest(new ApiResponse<string> { Success = false, Message = "دسته‌بندی یافت نشد یا غیرفعال است." });

                // Handle image upload (optional)
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

                // Create FoodItem object
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
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "آیتم با موفقیت اضافه شد",
                    Data = new
                    {
                        food.FoodItemId,
                        food.Name,
                        food.Price,
                        food.ImageUrl,
                        food.CategoryId
                    }
                });

                //return Ok(new ApiResponse<FoodItem>
                //{
                //    Success = true,
                //    Message = "آیتم با موفقیت اضافه شد",
                //    Data = food
                //});
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>
                {
                    Success = false,
                    Message = "خطا در سرور: " + ex.Message
                });
            }
        }


        //[HttpPost("addfood")]
        //public async Task<IActionResult> AddFood([FromForm] FoodItemCreateRequest request)
        //{
        //    try
        //    {
        //        // 🔒 Validate required fields
        //        if (string.IsNullOrWhiteSpace(request.Name))
        //            return BadRequest(new ApiResponse<string> { Success = false, Message = "نام غذا الزامی است." });

        //        if (request.Price <= 0)
        //            return BadRequest(new ApiResponse<string> { Success = false, Message = "قیمت باید بیشتر از صفر باشد." });

        //        if (request.RestaurantId <= 0)
        //            return BadRequest(new ApiResponse<string> { Success = false, Message = "شناسه رستوران معتبر نیست." });

        //        // 🖼️ Handle image upload (optional)
        //        string imageUrl = "";

        //        if (request.Image != null && request.Image.Length > 0)
        //        {
        //            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        //            if (!Directory.Exists(uploadsFolder))
        //                Directory.CreateDirectory(uploadsFolder);

        //            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(request.Image.FileName);
        //            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        //            using (var stream = new FileStream(filePath, FileMode.Create))
        //            {
        //                await request.Image.CopyToAsync(stream);
        //            }

        //            imageUrl = $"/uploads/{uniqueFileName}";
        //        }


        //        // ✅ Create FoodItem object
        //        var food = new FoodItem
        //        {
        //            Name = request.Name.Trim(),
        //            Description = request.Description?.Trim(),
        //            ImageUrl = imageUrl,
        //            Category = request.Category?.Trim(),

        //            Price = request.Price,
        //            DiscountPrice = request.DiscountPrice,
        //            CostPrice = request.CostPrice,
        //            RestaurantId = request.RestaurantId,
        //            IsAvailable = request.isAvailable,
        //            CreatedAt = DateTime.Now // optional, but good practice
        //        };

        //        _context.FoodItems.Add(food);
        //        await _context.SaveChangesAsync();

        //        return Ok(new ApiResponse<FoodItem>
        //        {
        //            Success = true,
        //            Message = "آیتم با موفقیت اضافه شد",
        //            Data = food
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(new ApiResponse<string>
        //        {
        //            Success = false,
        //            Message = "خطا در سرور: " + ex.Message
        //        });
        //    }
        //}



        [HttpGet("getallFoods/{restaurantId}")]
        public async Task<IActionResult> GetAllByRestaurant(int restaurantId)
        {
            try
            {
                var items = await _context.FoodItems
                    .Where(f => f.RestaurantId == restaurantId && f.IsActive)
                    .Join(_context.Categories,
                          f => f.CategoryId,
                          c => c.CategoryId,
                          (f, c) => new
                          {
                              f.FoodItemId,
                              f.RestaurantId,
                              Name = f.Name ?? "",
                              Description = f.Description ?? "",
                              ImageUrl = f.ImageUrl ?? "",
                              CategoryName = c.CategoryName ?? "",
                              CategoryId = c.CategoryId ,
                              Price = f.Price,
                              DiscountPrice = f.DiscountPrice ?? 0,
                              CostPrice = f.CostPrice ?? 0,
                              IsAvailable = f.IsAvailable,
                              CreatedAt = f.CreatedAt.HasValue ? f.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm") : ""
                          })
                    .ToListAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "لیست آیتم‌ها با موفقیت دریافت شد.",
                    Data = items
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>
                {
                    Success = false,
                    Message = "خطا در سرور: " + ex.Message
                });
            }
        }




        [HttpPut("updatefood/{id}")]
        public async Task<IActionResult> UpdateFood(int id, [FromForm] FoodItemCreateRequest request)
        {
            try
            {
                var food = await _context.FoodItems.FindAsync(id);
                if (food == null)
                {
                    return NotFound(new FoodItemResponse
                    {
                        Success = false,
                        Message = "آیتم غذایی مورد نظر یافت نشد.",
                        StatusCode = 404
                    });
                }

                // بررسی CategoryId
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId && c.RestaurantId == food.RestaurantId);
                if (category == null)
                    return BadRequest(new FoodItemResponse
                    {
                        Success = false,
                        Message = "دسته‌بندی معتبر نیست یا غیرفعال است."
                    });

                // مدیریت تصویر
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                if (request.Image != null && request.Image.Length > 0)
                {
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

                // به‌روزرسانی اطلاعات دیگر
                food.Name = request.Name.Trim();
                food.Description = request.Description?.Trim();
                food.CategoryId = request.CategoryId;
                food.Price = request.Price;
                food.DiscountPrice = request.DiscountPrice;
                food.CostPrice = request.CostPrice;
                food.IsAvailable = request.isAvailable ?? true;
                // معمولا RestaurantId تغییر داده نمی‌شود، مگر سیاست اپلیکیشن اجازه دهد

                await _context.SaveChangesAsync();

                return Ok(new FoodItemResponse
                {
                    Success = true,
                    Message = "آیتم با موفقیت ویرایش شد.",
                    StatusCode = 200
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new FoodItemResponse
                {
                    Success = false,
                    Message = "خطا در سرور: " + ex.Message
                });
            }
        }



        [HttpDelete("deleteFood/{id}")]
        public async Task<IActionResult> DeleteFoodItem(int id)
        {
            try
            {
                var item = await _context.FoodItems.FindAsync(id);

                if (item == null)
                {
                    return NotFound(new DeleteApiResponse
                    {
                        Success = false,
                        Message = "آیتم مورد نظر پیدا نشد.",
                        StatusCode = 404
                    });
                }

                // ✅ حذف منطقی بدون بررسی وابستگی
                item.IsActive = false;
                _context.FoodItems.Update(item);
                await _context.SaveChangesAsync();

                return Ok(new DeleteApiResponse
                {
                    Success = true,
                    Message = "آیتم با موفقیت غیرفعال شد.",
                    StatusCode = 200
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "خطای غیرمنتظره در سرور",
                    detail = ex.InnerException?.Message ?? ex.Message
                });
            }
        }


        [HttpGet("getfoodbyid/{id}")]
        public async Task<IActionResult> GetFoodById(int id)
        {
            try
            {
                var food = await _context.FoodItems
                    .Where(f => f.FoodItemId == id)
                    .Join(_context.Categories,
                        f => f.CategoryId,
                        c => c.CategoryId,
                        (f, c) => new
                        {
                            f.FoodItemId,
                            f.RestaurantId,
                            Name = f.Name ?? "",
                            Description = f.Description ?? "",
                            ImageUrl = f.ImageUrl ?? "",
                            CategoryName = c.CategoryName ?? "",
                            CategoryId = c.CategoryId,
                            Price = f.Price,
                            DiscountPrice = f.DiscountPrice ?? 0,
                            CostPrice = f.CostPrice ?? 0,
                            IsAvailable = f.IsAvailable,
                            IsActive = f.IsActive,
                            CreatedAt = f.CreatedAt.HasValue ? f.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm") : ""
                        })
                    .FirstOrDefaultAsync();

                if (food == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "آیتم غذایی یافت نشد.",
                        Data = null
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "آیتم با موفقیت دریافت شد.",
                    Data = food
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "خطا در سرور: " + ex.Message
                });
            }
        }




        [HttpPost("createOrder")]
        public async Task<IActionResult> CreateOrder(CreateOrderRequest request) 

        {
            try
            {
                
                if (request.CustomerId.HasValue)
                {
                    var customerExists = _context.Customers.Any(c => c.CustomerId == request.CustomerId.Value && c.RestaurantId == request.RestaurantId);
                    if (!customerExists)
                    {
                        return BadRequest(new CreateOrderApiResponse
                        {
                            Success = false,
                            Message = "مشتری با این شناسه برای رستوران مورد نظر یافت نشد."
                        });
                    }
                }

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

                // Add order items
                foreach (var item in request.Items)
                {
                    var food = _context.FoodItems.Find(item.FoodItemId);
                    if (food == null)
                    {
                        return BadRequest(new CreateOrderApiResponse
                        {
                            Success = false,
                            Message = $"FoodItemId {item.FoodItemId} not found."
                        });
                    }

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

                // 1️⃣ Save order first to get OrderId
                _context.Orders.Add(order);
                _context.SaveChanges();

                // 2️⃣ Add initial OrderUpdate
                int? nextRoleId = 3; // Default first role
                if (nextRoleId.HasValue)
                {
                    var existingUpdate = _context.OrderUpdates
                        .FirstOrDefault(u => u.OrderId == order.OrderId
                                          && u.TargetRoleId == nextRoleId.Value);

                    if (existingUpdate != null)
                    {
                        // Update existing timestamp
                        existingUpdate.UpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                    }
                    else
                    {
                        _context.OrderUpdates.Add(new OrderUpdate
                        {
                            OrderId = order.OrderId,
                            RestaurantId = order.RestaurantId,
                            TargetRoleId = nextRoleId.Value,
                            UpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds()
                        });
                    }
                }

                _context.SaveChanges(); // Save the update

                // 🔥 اضافه کردن SignalR برای ارسال نوتیفیکیشن به تمام کلاینت‌های رستوران
                await _hubContext.Clients.Group(order.RestaurantId.ToString())
               .SendAsync("ReceiveOrderUpdate", new
               {
                   orderId = order.OrderId,
                   newStatusId = order.StatusId,
                   message = $"Order {order.OrderId} updated to status {order.StatusId}"
               });

                return Ok(new CreateOrderApiResponse
                {
                    Success = true,
                    Message = "Order created successfully.",
                    Data = new { orderId = order.OrderId }
                });

            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                    errorMessage += " | Inner: " + ex.InnerException.Message;
                if (ex.InnerException?.InnerException != null)
                    errorMessage += " | Deep: " + ex.InnerException.InnerException.Message;

                return StatusCode(500, new CreateOrderApiResponse
                {
                    Success = false,
                    Message = $"خطای داخلی سرور: {errorMessage}"
                });
            }
        }

        [HttpPost("UpdateOrderStatusWithSignalar/{orderId}")]
        public async Task<IActionResult> UpdateOrderStatusWithSignalar(int orderId, [FromBody] UpdateOrderStatusDto dto)
        {
            var order = _context.Orders.Find(orderId);
            if (order == null)
                return NotFound(new { success = false, message = "Order not found." });

           
            if (order.StatusId != dto.CurrentStatusId)
            {
                return Conflict(new
                {
                    success = false,
                    message = "وضعیت سفارش توسط کاربر دیگری تغییر کرده است."
                });
            }

            
            order.StatusId = dto.NewStatusId;
            order.UpdatedAt = DateTime.Now;

            
            int? nextRoleId = GetNextRoleId(dto.NewStatusId);
            if (nextRoleId.HasValue)
            {
                var existingUpdate = _context.OrderUpdates
                    .FirstOrDefault(u => u.OrderId == order.OrderId
                                      && u.TargetRoleId == nextRoleId.Value);

                if (existingUpdate != null)
                {
                    existingUpdate.UpdateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                }
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
            }

            _context.SaveChanges();

            // 🔥 اضافه کردن await برای ارسال SignalR
            await _hubContext.Clients.Group(order.RestaurantId.ToString())
                .SendAsync("ReceiveOrderUpdate", new
                {
                    orderId = order.OrderId,
                    newStatusId = order.StatusId,
                    message = $"Order {order.OrderId} updated to status {order.StatusId}"
                });

            return Ok(new { success = true, message = "Order status updated and signal sent successfully." });
        }

        [HttpPut("UpdateOrder/{orderId}")]
        public async Task<IActionResult> UpdateOrder(int orderId, [FromBody] UpdateOrderRequest request)
        {
            try
            {
                var order = _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefault(o => o.OrderId == orderId);

                if (order == null)
                    return NotFound(new { success = false, message = "Order not found." });

                // ذخیره وضعیت قبلی برای SignalR
                var oldStatusId = order.StatusId;

                // Update main fields
                order.TableNumber = request.TableNumber;
                order.RestaurantId = request.RestaurantId;
                order.StatusId = request.StatusId;
                order.UpdatedAt = DateTime.Now;
                order.Description = request.Description;

                // *** اضافه کردن این قسمت برای آپدیت تاریخ شمسی ***
                order.UpdatedAtShamsi = DateHelper.ToShamsi(DateTime.Now);

                // اگر CreatedAtShamsi خالی است، آن را هم پر کن
                if (string.IsNullOrEmpty(order.CreatedAtShamsi))
                {
                    order.CreatedAtShamsi = DateHelper.ToShamsi(order.CreatedAt);
                }

                // Remove existing items
                _context.OrderItems.RemoveRange(order.OrderItems);

                // Add new items
                order.OrderItems = new List<OrderItem>();
                foreach (var item in request.Items)
                {
                    var food = _context.FoodItems.Find(item.FoodItemId);
                    if (food == null)
                        return BadRequest(new { success = false, message = $"FoodItemId {item.FoodItemId} not found." });

                    order.OrderItems.Add(new OrderItem
                    {
                        FoodItemId = item.FoodItemId,
                        Quantity = item.Quantity,
                        UnitPrice = food.Price,
                        UnitPriceWithDiscount = (decimal)food.DiscountPrice,
                        FoodName = food.Name,
                        FoodImageUrl = food.ImageUrl
                    });
                }

                order.UpdatedAt = DateTime.Now;

                // تعیین نقش بعدی
                int? nextRoleId = GetNextRoleId(request.StatusId); // 🔥 اصلاح: استفاده از StatusId فعلی
                if (nextRoleId.HasValue)
                {
                    var existingUpdate = _context.OrderUpdates
                        .FirstOrDefault(u => u.OrderId == order.OrderId
                                          && u.TargetRoleId == nextRoleId.Value);

                    if (existingUpdate != null)
                    {
                        existingUpdate.UpdateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    }
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
                }

                _context.SaveChanges();

                // 🔥 اضافه کردن SignalR برای ارسال نوتیفیکیشن
                await _hubContext.Clients.Group(order.RestaurantId.ToString())
                    .SendAsync("ReceiveOrderUpdate", new
                    {
                        orderId = order.OrderId,
                        oldStatusId = oldStatusId, // وضعیت قبلی
                        newStatusId = order.StatusId, // وضعیت جدید
                        message = $"Order {order.OrderId} updated from status {oldStatusId} to {order.StatusId}",
                        updateType = "fullUpdate" // نوع آپدیت
                    });

                var orderDto = new OrderDto
                {
                    OrderId = order.OrderId,
                    TableNumber = order.TableNumber,
                    StatusId = order.StatusId,
                    CreatedAt = order.CreatedAt,
                    UpdatedAt = order.UpdatedAt,
                    CreatedAtShamsi = order.CreatedAtShamsi,
                    UpdatedAtShamsi = order.UpdatedAtShamsi,
                    Description = order.Description,
                    OrderItems = order.OrderItems.Select(oi => new OrderItemDto
                    {
                        OrderItemId = oi.OrderItemId,
                        FoodItemId = oi.FoodItemId,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        UnitPriceWithDiscount = oi.UnitPriceWithDiscount,
                        FoodName = oi.FoodName,
                        FoodImageUrl = oi.FoodImageUrl
                    }).ToList()
                };

                return Ok(new OrderResponse
                {
                    Success = true,
                    Message = "سفارش با موفقیت به‌روزرسانی شد",
                    OrderData = orderDto
                });
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                    errorMessage += " | Inner: " + ex.InnerException.Message;
                if (ex.InnerException?.InnerException != null)
                    errorMessage += " | Deep: " + ex.InnerException.InnerException.Message;

                return StatusCode(500, new OrderResponse
                {
                    Success = false,
                    Message = $"خطای داخلی سرور: {errorMessage}"
                });
            }
        }

      



        [HttpGet("GetOrdersByRestaurant/{restaurantId}")]
        public IActionResult GetOrdersByRestaurant(int restaurantId)
        {

            var statusIds = new List<int> { 9, 10, 11 };
            


            var orders = _context.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.RestaurantId == restaurantId)
                .Where(o => !statusIds.Contains(o.StatusId))
                .Select(o => new OrderDto
                {
                    OrderId = o.OrderId,
                    TableNumber = o.TableNumber,
                    StatusId = o.StatusId,
                    CreatedAt = o.CreatedAt,
                    UpdatedAt = o.UpdatedAt,
                    CreatedAtShamsi = o.CreatedAtShamsi ?? DateHelper.ToShamsi(o.CreatedAt),
                    UpdatedAtShamsi = o.UpdatedAtShamsi ?? DateHelper.ToShamsi(o.UpdatedAt),
                    Description = o.Description,
                    OrderItems = o.OrderItems.Select(oi => new OrderItemDto
                    {
                        OrderItemId = oi.OrderItemId,
                        FoodItemId = oi.FoodItemId,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        UnitPriceWithDiscount = oi.UnitPriceWithDiscount,
                        FoodName = oi.FoodName,
                        FoodImageUrl = oi.FoodImageUrl
                    }).ToList()
                }).ToList();
            var serverTime = DateTimeOffset.Now.ToUnixTimeSeconds();

            return Ok(new
            {
                success = true,
                data = orders,
                lastCheck = serverTime   // <--- add this
            });


        }

        [HttpPost("GetOrdersByRestaurantWithDateFilter")]
        public IActionResult GetOrdersByRestaurantWithDateFilter([FromBody] OrderDateFilterRequest request)
        {
            try
            {
                // Validate pagination parameters
                if (request.PageNumber < 1) request.PageNumber = 1;
                if (request.PageSize < 1 || request.PageSize > 100) request.PageSize = 20;

                var statusIds = new List<int> { 9, 10, 11 }; // CLOSED, CANCELED_BY_RESTAURANT, CANCELED_BY_CUSTOMER
                var query = _context.Orders
                    .Include(o => o.OrderItems)
                    .Where(o => o.RestaurantId == request.RestaurantId)
                    .Where(o => statusIds.Contains(o.StatusId));

                // فیلتر بر اساس تاریخ
                if (!string.IsNullOrEmpty(request.FromDate) && !string.IsNullOrEmpty(request.ToDate))
                {
                    try
                    {
                        // تبدیل تاریخ‌های شمسی به میلادی برای مقایسه
                        var fromDate = DateHelper.ShamsiToDateTime(request.FromDate);
                        var toDate = DateHelper.ShamsiToDateTime(request.ToDate).AddDays(1).AddSeconds(-1); // تا پایان روز

                        query = query.Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate);
                    }
                    catch (Exception dateEx)
                    {
                        return BadRequest(new PaginatedResponse<OrderDto>
                        {
                            Success = false,
                            Message = "فرمت تاریخ نامعتبر است",
                            Data = new List<OrderDto>()
                        });
                    }
                }

                // Get total count for pagination
                var totalCount = query.Count();

                // Apply pagination
                var orders = query
                    .OrderByDescending(o => o.CreatedAt) // Important: order for consistent pagination
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .Select(o => new OrderDto
                    {
                        OrderId = o.OrderId,
                        TableNumber = o.TableNumber,
                        StatusId = o.StatusId,
                        CreatedAt = o.CreatedAt,
                        UpdatedAt = o.UpdatedAt,
                        CreatedAtShamsi = o.CreatedAtShamsi ?? DateHelper.ToShamsi(o.CreatedAt),
                        UpdatedAtShamsi = o.UpdatedAtShamsi ?? DateHelper.ToShamsi(o.UpdatedAt),
                        Description = o.Description,
                        OrderItems = o.OrderItems.Select(oi => new OrderItemDto
                        {
                            OrderItemId = oi.OrderItemId,
                            FoodItemId = oi.FoodItemId,
                            Quantity = oi.Quantity,
                            UnitPrice = oi.UnitPrice,
                            UnitPriceWithDiscount = oi.UnitPriceWithDiscount,
                            FoodName = oi.FoodName,
                            FoodImageUrl = oi.FoodImageUrl
                        }).ToList()
                    })
                    .ToList();

                var serverTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

                return Ok(new PaginatedResponse<OrderDto>
                {
                    Success = true,
                    Data = orders,
                    TotalCount = totalCount,
                    CurrentPage = request.PageNumber,
                    TotalPages = totalPages,
                    HasNextPage = request.PageNumber < totalPages,
                    LastCheck = serverTime
                });
            }
            catch (Exception ex)
            {
                // Log the exception
              

                return BadRequest(new PaginatedResponse<OrderDto>
                {
                    Success = false,
                    Message = "خطا در دریافت سفارش‌ها",
                    Data = new List<OrderDto>()
                });
            }
        }

        [HttpPost("ExportOrdersToExcel")]
        public IActionResult ExportOrdersToExcel([FromBody] OrderDateFilterRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.FromDate) || string.IsNullOrEmpty(request.ToDate))
                    return BadRequest("بازه تاریخ معتبر نیست");

                var fromDate = DateHelper.ShamsiToDateTime(request.FromDate);
                var toDate = DateHelper.ShamsiToDateTime(request.ToDate).AddDays(1).AddSeconds(-1);

                var statusIds = new List<int> { 9, 10, 11 };

                var orders = _context.Orders
                    .Include(o => o.OrderItems)
                    .Where(o => o.RestaurantId == request.RestaurantId)
                    .Where(o => statusIds.Contains(o.StatusId))
                    .Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToList();

                if (!orders.Any())
                    return BadRequest("هیچ سفارشی در این بازه زمانی یافت نشد.");

                using (var workbook = new XLWorkbook())
                {
                    // ------------------------ Sheet 1: Orders Summary ------------------------
                    var wsOrders = workbook.Worksheets.Add("خلاصه سفارش‌ها");

                    // 🟩 Header
                    wsOrders.Cell(1, 1).Value = "شناسه سفارش";
                    wsOrders.Cell(1, 2).Value = "تاریخ ایجاد (شمسی)";
                    wsOrders.Cell(1, 3).Value = "شماره میز";
                    wsOrders.Cell(1, 4).Value = "وضعیت";
                    wsOrders.Cell(1, 5).Value = "توضیحات";
                    wsOrders.Cell(1, 6).Value = "تعداد آیتم‌ها";
                    wsOrders.Cell(1, 7).Value = "جمع مبلغ کل (تومان)";

                    int row = 2;
                    foreach (var o in orders)
                    {
                        var totalPrice = o.OrderItems.Sum(i => (decimal)((i.UnitPriceWithDiscount.HasValue && i.UnitPriceWithDiscount > 0 ? i.UnitPriceWithDiscount : i.UnitPrice) * i.Quantity));

                        wsOrders.Cell(row, 1).Value = o.OrderId;
                        wsOrders.Cell(row, 2).Value = o.CreatedAtShamsi ?? DateHelper.ToShamsi(o.CreatedAt);
                        wsOrders.Cell(row, 3).Value = o.TableNumber;
                        wsOrders.Cell(row, 4).Value = GetStatusName(o.StatusId);
                        wsOrders.Cell(row, 5).Value = o.Description ?? "-";
                        wsOrders.Cell(row, 6).Value = o.OrderItems.Count;
                        wsOrders.Cell(row, 7).Value = totalPrice;
                        row++;
                    }

                    // 🔹 Footer Summary
                    wsOrders.Cell(row + 1, 6).Value = "جمع کل سفارشات:";
                    wsOrders.Cell(row + 1, 7).FormulaA1 = $"=SUM(G2:G{row - 1})";

                    wsOrders.Cell(row + 2, 6).Value = "تعداد کل سفارش‌ها:";
                    wsOrders.Cell(row + 2, 7).Value = orders.Count;

                    wsOrders.Cell(row + 3, 6).Value = "میانگین مبلغ هر سفارش:";
                    wsOrders.Cell(row + 3, 7).FormulaA1 = $"=AVERAGE(G2:G{row - 1})";

                    // 🎨 Header style
                    var headerRange1 = wsOrders.Range("A1:G1");
                    headerRange1.Style.Font.Bold = true;
                    headerRange1.Style.Fill.BackgroundColor = XLColor.LightGray;
                    headerRange1.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    headerRange1.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    headerRange1.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    // 🔹 Right-to-Left
                    wsOrders.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    wsOrders.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    wsOrders.Columns().AdjustToContents();

                    // ------------------------ Sheet 2: Order Items ------------------------
                    var wsItems = workbook.Worksheets.Add("جزئیات سفارش‌ها");

                    wsItems.Cell(1, 1).Value = "شناسه سفارش";
                    wsItems.Cell(1, 2).Value = "شناسه آیتم";
                    wsItems.Cell(1, 3).Value = "نام غذا";
                    wsItems.Cell(1, 4).Value = "تعداد";
                    wsItems.Cell(1, 5).Value = "قیمت واحد (تومان)";
                    wsItems.Cell(1, 6).Value = "قیمت با تخفیف (تومان)";
                    wsItems.Cell(1, 7).Value = "مبلغ کل (تومان)";

                    int itemRow = 2;
                    foreach (var o in orders)
                    {
                        foreach (var i in o.OrderItems)
                        {
                            var finalUnitPrice = (i.UnitPriceWithDiscount.HasValue && i.UnitPriceWithDiscount > 0) ? i.UnitPriceWithDiscount : i.UnitPrice;
                            var total = (decimal)(finalUnitPrice * i.Quantity);
                            wsItems.Cell(itemRow, 1).Value = o.OrderId;
                            wsItems.Cell(itemRow, 2).Value = i.OrderItemId;
                            wsItems.Cell(itemRow, 3).Value = i.FoodName ?? "-";
                            wsItems.Cell(itemRow, 4).Value = i.Quantity;
                            wsItems.Cell(itemRow, 5).Value = i.UnitPrice;
                            wsItems.Cell(itemRow, 6).Value = (i.UnitPriceWithDiscount.HasValue && i.UnitPriceWithDiscount > 0) ? i.UnitPriceWithDiscount : i.UnitPrice;
                            wsItems.Cell(itemRow, 7).Value = total;
                            itemRow++;
                        }
                    }

                    var headerRange2 = wsItems.Range("A1:G1");
                    headerRange2.Style.Font.Bold = true;
                    headerRange2.Style.Fill.BackgroundColor = XLColor.LightGray;
                    headerRange2.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    headerRange2.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    headerRange2.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    wsItems.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    wsItems.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    wsItems.Columns().AdjustToContents();

                    // ------------------------ Save & Return ------------------------
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        string fileName = $"OrdersReport_{request.RestaurantId}_{request.FromDate}_{request.ToDate}.xlsx";

                        return File(content,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"خطا در تولید گزارش: {ex.Message}");
            }
        }
        private string GetStatusName(int statusId)
        {
            return statusId switch
            {
                9 => "لغو توسط مشتری",
                10 => "لغو توسط رستوران",
                11 => "بسته شده",
                _ => "نامشخص"
            };
        }

        //[HttpPost("GetOrdersByRestaurantWithDateFilter")]
        //public IActionResult GetOrdersByRestaurantWithDateFilter([FromBody] OrderDateFilterRequest request)
        //{
        //    try
        //    {
        //        var statusIds = new List<int> { 9, 10, 11 };
        //        var query = _context.Orders
        //            .Include(o => o.OrderItems)
        //            .Where(o => o.RestaurantId == request.RestaurantId)
        //            .Where(o => statusIds.Contains(o.StatusId));

        //        // فیلتر بر اساس تاریخ
        //        if (!string.IsNullOrEmpty(request.FromDate) && !string.IsNullOrEmpty(request.ToDate))
        //        {
        //            // تبدیل تاریخ‌های شمسی به میلادی برای مقایسه
        //            var fromDate = DateHelper.ShamsiToDateTime(request.FromDate);
        //            var toDate = DateHelper.ShamsiToDateTime(request.ToDate).AddDays(1).AddSeconds(-1); // تا پایان روز

        //            query = query.Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate);
        //        }

        //        var orders = query
        //            .Select(o => new OrderDto
        //            {
        //                OrderId = o.OrderId,
        //                TableNumber = o.TableNumber,
        //                StatusId = o.StatusId,
        //                CreatedAt = o.CreatedAt,
        //                UpdatedAt = o.UpdatedAt,
        //                CreatedAtShamsi = o.CreatedAtShamsi ?? DateHelper.ToShamsi(o.CreatedAt),
        //                UpdatedAtShamsi = o.UpdatedAtShamsi ?? DateHelper.ToShamsi(o.UpdatedAt),
        //                Description = o.Description,
        //                OrderItems = o.OrderItems.Select(oi => new OrderItemDto
        //                {
        //                    OrderItemId = oi.OrderItemId,
        //                    FoodItemId = oi.FoodItemId,
        //                    Quantity = oi.Quantity,
        //                    UnitPrice = oi.UnitPrice,
        //                    UnitPriceWithDiscount = oi.UnitPriceWithDiscount,
        //                    FoodName = oi.FoodName,
        //                    FoodImageUrl = oi.FoodImageUrl
        //                }).ToList()
        //            })
        //            .ToList();

        //        var serverTime = DateTimeOffset.Now.ToUnixTimeSeconds();

        //        return Ok(new
        //        {
        //            success = true,
        //            data = orders,
        //            lastCheck = serverTime
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(new
        //        {
        //            success = false,
        //            message = "خطا در دریافت سفارش‌ها",
        //            error = ex.Message
        //        });
        //    }
        //}



        public class UpdateOrderStatusDto
        {
            public int CurrentStatusId { get; set; }
            public int NewStatusId { get; set; }
        }


       


        [HttpGet("CheckOrderUpdates")]
        public IActionResult CheckOrderUpdates(int restaurantId, int role2, int role3, int role4, long lastCheck)
        {
            // لیست رول‌هایی که باید بررسی بشن
            var targetRoles = new List<int>();
            if (role2 == 1) targetRoles.Add(2);
            if (role3 == 1) targetRoles.Add(3);
            if (role4 == 1) targetRoles.Add(4);

            //bool hasUpdates = _context.OrderUpdates
            //    .Any(u => u.RestaurantId == restaurantId
            //           && targetRoles.Contains(u.TargetRoleId)
            //           && u.UpdateTime > lastCheck);


            bool hasUpdates = _context.OrderUpdates.Any(u =>
    u.RestaurantId == restaurantId &&
    (
        (role2 == 1 && u.TargetRoleId == 2) ||
        (role3 == 1 && u.TargetRoleId == 3) ||
        (role4 == 1 && u.TargetRoleId == 4)
    ) &&
    u.UpdateTime > lastCheck
);


            return Ok(new { success = true, hasUpdates });
        }


        private int? GetNextRoleId(int statusId)
        {
            // اینجا باید طبق بیزینس خودت مپ کنی
            switch (statusId)
            {
               
                case 3: return 3;  // Chef
                case 4: return 3;  // Cashier
                case 5: return 2;  // waiter
                case 6: return 4;  // Owner
                case 7: return 4;  // Owner
                case 8: return 4;  // Owner
                case 9: return 4;  // Owner
                case 10: return 4; // Owner
                case 11: return 4; // Owner
                case 12: return 3; // Chef
                case 99: return 3; // Chef
                default: return null;
            }
        }

      

        [HttpPost("otprequest")]
        public async Task<IActionResult> RequestOtp([FromBody] OtpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                return BadRequest(new { success = false, message = "Phone number is required" });

            // Generate 4-digit OTP
            var otpCode = new Random().Next(1000, 10000).ToString();

            // Save hashed OTP in DB
            var otpEntry = new OtpEntry
            {
                PhoneNumber = request.PhoneNumber,
                CodeHash = OtpHelper.HashOtp(otpCode),
                ExpireAt = DateTime.UtcNow.AddMinutes(2),
                Used = false
            };
            _context.OtpEntries.Add(otpEntry);
            await _context.SaveChangesAsync();

            // Send SMS (via Payamak API)
            var smsRequest = new
            {
                username = "09149141260",
                password = "PCMZA",
                text = $" دلاویتا ; {otpCode}", // Match Postman's exact format with spaces
                to = request.PhoneNumber,
                bodyId = "357811" // Ensure this is a string
            };

            using var client = new HttpClient();
            // Only add headers that belong to the request (not content headers)
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            try
            {
                // Serialize the request body manually to ensure correct encoding
                var json = System.Text.Json.JsonSerializer.Serialize(smsRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(
                    "https://rest.payamak-panel.com/api/SendSMS/BaseServiceNumber",
                    content
                );

                // Read response body
                var responseContent = await response.Content.ReadAsStringAsync();

                // Parse JSON response to check RetStatus and StrRetStatus
                var jsonResponse = System.Text.Json.JsonSerializer.Deserialize<PayamakResponse>(responseContent);

                if (!response.IsSuccessStatusCode || jsonResponse.RetStatus != 1 || jsonResponse.StrRetStatus != "Ok")
                {
                    // Log the error for debugging (use ILogger if available)
                    // _logger.LogError($"SMS API failed: Status {response.StatusCode}, Response: {responseContent}");
                    return StatusCode((int)response.StatusCode, new
                    {
                        success = false,
                        message = $"SMS failed: Status {response.StatusCode}, RetStatus: {jsonResponse.RetStatus}, StrRetStatus: {jsonResponse.StrRetStatus}"
                    });
                }

                return Ok(new { success = true, message = "OTP sent successfully" });
            }
            catch (Exception ex)
            {
                // Handle network or JSON parsing errors
                return StatusCode(500, new { success = false, message = $"SMS request failed: {ex.Message}" });
            }
        }

        // Define a class to deserialize Payamak API response


        [HttpPost("otpverify")]
        public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyRequest request)
        {
            var hashedInput = OtpHelper.HashOtp(request.Code);

            var otpEntry = await _context.OtpEntries
                .Where(x => x.PhoneNumber == request.PhoneNumber
                          && x.CodeHash == hashedInput 
                          && !x.Used)
                .OrderByDescending(x => x.ExpireAt)
                .FirstOrDefaultAsync();

            // The rest of your logic remains the same
            if (otpEntry == null || otpEntry.ExpireAt < DateTime.UtcNow)
                return BadRequest(new { success = false, message = "OTP expired or not found" });

            // Mark as used
            otpEntry.Used = true;
            await _context.SaveChangesAsync();

            // Check if owner exists, otherwise create one (depends on your logic)
            var owner = await _context.Owners.FirstOrDefaultAsync(o => o.Phone == request.PhoneNumber);

            if (owner == null)
            {
                owner = new Owner { Phone = request.PhoneNumber, Password = null }; // no password needed
                _context.Owners.Add(owner);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                success = true,
                message = "ورود با موفقیت انجام شد",
                owner_id = owner.Id,
                password = DecodePassword(owner.Password)

            });
        }




        [HttpPost("otpverifyregister")]
        public async Task<IActionResult> VerifyOtpForRegister([FromBody] OtpVerifyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Code))
                return BadRequest(new { success = false, message = "Phone and code are required" });

            var normalizedPhoneNumber = request.PhoneNumber.Replace("+", "").Replace(" ", "").Trim();
            var hashedInput = OtpHelper.HashOtp(request.Code);

            var otpEntry = await _context.OtpEntries
                .Where(x => x.PhoneNumber == normalizedPhoneNumber
                          && x.CodeHash == hashedInput
                          && !x.Used)
                .OrderByDescending(x => x.ExpireAt)
                .FirstOrDefaultAsync();

            if (otpEntry == null || otpEntry.ExpireAt < DateTime.UtcNow)
                return BadRequest(new { success = false, message = "OTP expired or not found" });

            // Mark OTP as used
            otpEntry.Used = true;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveChanges failed: {ex.ToString()}");
                return StatusCode(500, new { success = false, message = "خطای غیرمنتظره در سرور", detail = ex.ToString() });
            }

            // Do not create or check for an Owner here, as the user will be guided to register
            return Ok(new
            {
                success = true,
                message = "کد تأیید با موفقیت تأیید شد. لطفاً برای ثبت‌نام اقدام کنید."
            });
        }

        [HttpPost("verifyotpweb")]
        [AllowAnonymous] 
        public async Task<IActionResult> VerifyOtpWeb([FromBody] OtpVerifyRequest request)
        {
            try
            {
                // ۱. بررسی اعتبار ورودی‌ها
                if (request == null || string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Code))
                {
                    return BadRequest(new { success = false, message = "شماره موبایل و کد تایید الزامی است." });
                }

                // ۲. پیدا کردن کد OTP معتبر در دیتابیس
                var hashedInput = OtpHelper.HashOtp(request.Code);
                var otpEntry = await _context.OtpEntries
                    .Where(x => x.PhoneNumber == request.PhoneNumber
                              && x.CodeHash == hashedInput
                              && !x.Used)
                    .OrderByDescending(x => x.ExpireAt)
                    .FirstOrDefaultAsync();

                // ۳. بررسی صحت کد و انقضای آن
                if (otpEntry == null || otpEntry.ExpireAt < DateTime.UtcNow)
                {
                    return BadRequest(new { success = false, message = "کد تایید منقضی شده یا اشتباه است." });
                }

                // ۴. استفاده از کد (Mark as used)
                otpEntry.Used = true;
                await _context.SaveChangesAsync();

                // ۵. پیدا کردن یا ساخت کاربر (Owner)
                var owner = await _context.Owners.FirstOrDefaultAsync(o => o.Phone == request.PhoneNumber);
                if (owner == null)
                {
                    // اگر کاربر وجود نداشت، یک کاربر جدید بساز
                    owner = new Owner
                    {
                        Phone = request.PhoneNumber,
                        Password = null // چون با OTP وارد می‌شود پسورد ندارد
                    };
                    _context.Owners.Add(owner);
                    await _context.SaveChangesAsync();
                }

                // ۶. ایجاد کوکی لاگین (منحصر به فرد برای وب)
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, owner.Name ?? "مدیر رستوران"),
                    new Claim("OwnerId", owner.Id.ToString()),
                    new Claim(ClaimTypes.Role, "Owner")
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                // لاگین کردن کاربر در سیستم
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                // ۷. بازگشت پاسخ موفقیت
                return Ok(new
                {
                    success = true,
                    message = "ورود با موفقیت انجام شد",
                    redirectUrl = "/Home/ChooseRestaurant"
                });
            }
            catch (Exception ex)
            {
                // مدیریت خطا شبیه سایر متدهای کنترلر
                string fullErrorMessage = ex.Message;
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    fullErrorMessage += " --> " + inner.Message;
                    inner = inner.InnerException;
                }
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + fullErrorMessage });
            }
        }

        [HttpPost("addcategory")]
        public IActionResult AddCategory(AddCategoryRequest request)
        {
            try
            {
                var restaurant = _context.Restaurants.Find(request.RestaurantId);
                if (restaurant == null)
                {
                    return NotFound(new { success = false, message = "رستوران یافت نشد" });
                }

                bool exists = _context.Categories.Any(c =>
                    c.RestaurantId == request.RestaurantId &&
                    c.CategoryName.ToLower().Trim() == request.CategoryName.ToLower().Trim());

                if (exists)
                {
                    return Ok(new { success = false, message = "این دسته‌بندی قبلاً برای این رستوران ثبت شده است" });
                }

                var category = new Category
                {
                    RestaurantId = request.RestaurantId,
                    CategoryName = request.CategoryName.Trim(),
                    CreatedAt = DateTime.Now
                };

                _context.Categories.Add(category);
                _context.SaveChanges();

                return Ok(new { success = true, message = "دسته‌بندی با موفقیت اضافه شد", category_id = category.CategoryId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpGet("getcategoriesbyrestaurant/{restaurantId}")]
        public IActionResult GetCategoriesByRestaurant(int restaurantId)
        {
            try
            {
                var restaurant = _context.Restaurants.Find(restaurantId);
                if (restaurant == null)
                {
                    return NotFound(new { success = false, message = "رستوران یافت نشد" });
                }

                var categories = _context.Categories
                    .Where(c => c.RestaurantId == restaurantId)
                    .Select(c => new
                    {
                        c.CategoryId,
                        c.CategoryName,
                        c.CreatedAt
                    })
                    .ToList();

                return Ok(new { success = true, restaurant = new { restaurant.restaurant_id, restaurant.name }, categories });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpPost("deletecategory")]
        public IActionResult DeleteCategory(DeleteCategoryRequest request)
        {
            try
            {
                var restaurant = _context.Restaurants.Find(request.RestaurantId);
                if (restaurant == null)
                {
                    return NotFound(new { success = false, message = "رستوران یافت نشد" });
                }

                var category = _context.Categories
                    .FirstOrDefault(c => c.CategoryId == request.CategoryId && c.RestaurantId == request.RestaurantId);

                if (category == null)
                {
                    return NotFound(new { success = false, message = "دسته‌بندی یافت نشد یا متعلق به این رستوران نیست" });
                }

                // بررسی اینکه آیا غذایی به این دسته‌بندی وصل هست
                bool hasFood = _context.FoodItems.Any(f => f.CategoryId == category.CategoryId);
                if (hasFood)
                {
                    return Ok(new { success = false, message = "امکان حذف دسته‌بندی وجود ندارد، چون هنوز غذاهایی در این دسته ثبت شده‌اند" });
                }

                _context.Categories.Remove(category);
                _context.SaveChanges();

                return Ok(new { success = true, message = "دسته‌بندی با موفقیت حذف شد" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }




        [HttpPost("editcategory")]
        public IActionResult EditCategory([FromBody] EditCategoryRequest request)
        {
            try
            {
                var restaurant = _context.Restaurants.Find(request.RestaurantId);
                if (restaurant == null)
                {
                    return NotFound(new GenericResponse
                    {
                        Success = false,
                        Message = "رستوران یافت نشد"
                    });
                }

                var category = _context.Categories
                    .FirstOrDefault(c => c.CategoryId == request.CategoryId && c.RestaurantId == request.RestaurantId);

                if (category == null)
                {
                    return NotFound(new GenericResponse
                    {
                        Success = false,
                        Message = "دسته‌بندی یافت نشد یا متعلق به این رستوران نیست"
                    });
                }

                bool exists = _context.Categories.Any(c =>
                    c.RestaurantId == request.RestaurantId &&
                    c.CategoryId != request.CategoryId &&
                    c.CategoryName.ToLower().Trim() == request.CategoryName.ToLower().Trim());

                if (exists)
                {
                    return Ok(new GenericResponse
                    {
                        Success = false,
                        Message = "این نام دسته‌بندی قبلاً در این رستوران ثبت شده است"
                    });
                }

                category.CategoryName = request.CategoryName.Trim();
                _context.SaveChanges();

                return Ok(new GenericResponse
                {
                    Success = true,
                    Message = "دسته‌بندی با موفقیت ویرایش شد"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new GenericResponse
                {
                    Success = false,
                    Message = "خطا در سرور: " + ex.Message
                });
            }
        }

    

        [HttpPost("addtable")]
        public IActionResult AddTable(AddTableRequest request)
        {
            try
            {
                var restaurant = _context.Restaurants.Find(request.RestaurantId);
                if (restaurant == null)
                {
                    return NotFound(new { success = false, message = "رستوران یافت نشد" });
                }

                bool exists = _context.RestaurantTables.Any(t =>
                    t.RestaurantId == request.RestaurantId &&
                    t.TableName.ToLower().Trim() == request.TableName.ToLower().Trim());

                if (exists)
                {
                    return Ok(new { success = false, message = "این میز قبلاً برای این رستوران ثبت شده است" });
                }

                var table = new RestaurantTable
                {
                    RestaurantId = request.RestaurantId,
                    TableName = request.TableName.Trim(),
                    Seats = request.Seats,
                    CreatedAt = DateTime.Now
                };

                _context.RestaurantTables.Add(table);
                _context.SaveChanges();

                return Ok(new { success = true, message = "میز با موفقیت اضافه شد", table_id = table.TableId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpGet("gettablesbyrestaurant/{restaurantId}")]
        public IActionResult GetTablesByRestaurant(int restaurantId)
        {
            try
            {
                var restaurant = _context.Restaurants.Find(restaurantId);
                if (restaurant == null)
                {
                    return NotFound(new { success = false, message = "رستوران یافت نشد" });
                }

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

                return Ok(new { success = true, restaurant = new { restaurant.restaurant_id, restaurant.name }, tables });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpPost("deletetable")]
        public IActionResult DeleteTable(DeleteTableRequest request)
        {
            try
            {
                var restaurant = _context.Restaurants.Find(request.RestaurantId);
                if (restaurant == null)
                {
                    return NotFound(new { success = false, message = "رستوران یافت نشد" });
                }

                var table = _context.RestaurantTables
                    .FirstOrDefault(t => t.TableId == request.TableId && t.RestaurantId == request.RestaurantId);

                if (table == null)
                {
                    return NotFound(new { success = false, message = "میز یافت نشد یا متعلق به این رستوران نیست" });
                }

                _context.RestaurantTables.Remove(table);
                _context.SaveChanges();

                return Ok(new { success = true, message = "میز با موفقیت حذف شد" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }



        [HttpPost("edittable")]
        public IActionResult EditTable([FromBody] EditTableRequest request)
        {
            try
            {
                var restaurant = _context.Restaurants.Find(request.RestaurantId);
                if (restaurant == null)
                {
                    return NotFound(new GenericResponse
                    {
                        Success = false,
                        Message = "رستوران یافت نشد"
                    });
                }

                var table = _context.RestaurantTables
                    .FirstOrDefault(t => t.TableId == request.TableId && t.RestaurantId == request.RestaurantId);

                if (table == null)
                {
                    return NotFound(new GenericResponse
                    {
                        Success = false,
                        Message = "میز یافت نشد یا متعلق به این رستوران نیست"
                    });
                }

                bool exists = _context.RestaurantTables.Any(t =>
                    t.RestaurantId == request.RestaurantId &&
                    t.TableId != request.TableId &&
                    t.TableName.ToLower().Trim() == request.TableName.ToLower().Trim());

                if (exists)
                {
                    return Ok(new GenericResponse
                    {
                        Success = false,
                        Message = "این نام میز قبلاً در این رستوران ثبت شده است"
                    });
                }

                table.TableName = request.TableName.Trim();
                table.Seats = request.Seats;
                _context.SaveChanges();

                return Ok(new GenericResponse
                {
                    Success = true,
                    Message = "میز با موفقیت ویرایش شد"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new GenericResponse
                {
                    Success = false,
                    Message = "خطا در سرور: " + ex.Message
                });
            }
        }



        [HttpGet("getOrderById/{orderId}")]
        public IActionResult GetOrderById(int orderId)
        {
            try
            {
                var order = _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefault(o => o.OrderId == orderId);

                if (order == null)
                {
                    return NotFound(new OrderResponse
                    {
                        Success = false,
                        Message = "سفارش یافت نشد"
                    });
                }

                var orderDto = new OrderDto
                {
                    OrderId = order.OrderId,
                    TableNumber = order.TableNumber,
                    StatusId = order.StatusId,
                    CreatedAt = order.CreatedAt,
                    UpdatedAt = order.UpdatedAt,
                    Description = order.Description,
                    OrderItems = order.OrderItems.Select(oi => new OrderItemDto
                    {
                        OrderItemId = oi.OrderItemId,
                        FoodItemId = oi.FoodItemId,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        UnitPriceWithDiscount = oi.UnitPriceWithDiscount,
                        FoodName = oi.FoodName,
                        FoodImageUrl = oi.FoodImageUrl
                    }).ToList()
                };

                return Ok(new OrderResponse
                {
                    Success = true,
                    Message = "سفارش با موفقیت دریافت شد",
                    OrderData = orderDto
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new OrderResponse
                {
                    Success = false,
                    Message = "خطا در سرور: " + ex.Message
                });
            }
        }


       



        //[HttpPost("TestSignalR/{restaurantId}")]
        //public async Task<IActionResult> TestSignalR(int restaurantId, [FromBody] TestSignalRDto dto)
        //{
        //    try
        //    {
        //        // لاگ برای دیباگ


        //        // ارسال پیام تست به گروه رستوران
        //        await _hubContext.Clients.Group(restaurantId.ToString())
        //            .SendAsync("ReceiveOrderUpdate", new
        //            {
        //                orderId = dto.OrderId ?? 999,
        //                newStatusId = dto.StatusId ?? 99,
        //                message = dto.Message ?? $"🧪 Test message for restaurant {restaurantId} at {DateTime.UtcNow}"
        //            });



        //        return Ok(new
        //        {
        //            success = true,
        //            message = $"Test SignalR message sent to restaurant {restaurantId}",
        //            timestamp = DateTime.UtcNow
        //        });
        //    }
        //    catch (Exception ex)
        //    {

        //        return StatusCode(500, new { success = false, error = ex.Message });
        //    }
        //}

        //public class TestSignalRDto
        //{
        //    public int? OrderId { get; set; }
        //    public int? StatusId { get; set; }
        //    public string? Message { get; set; }
        //}



        //[HttpPut("UpdateOrderStatus/{orderId}")]
        //public IActionResult UpdateOrderStatus(int orderId, [FromBody] UpdateOrderStatusDto dto)
        //{
        //    var order = _context.Orders.Find(orderId);
        //    if (order == null)
        //        return NotFound(new { success = false, message = "Order not found." });

        //    // بررسی وضعیت فعلی
        //    if (order.StatusId != dto.CurrentStatusId)
        //    {
        //        return Conflict(new
        //        {
        //            success = false,
        //            message = $"وضعیت سفارش توسط کاربر دیگری تغییر کرده است."
        //        });
        //    }

        //    // تغییر وضعیت
        //    order.StatusId = dto.NewStatusId;
        //    order.UpdatedAt = DateTime.UtcNow;

        //    // تعیین نقش بعدی
        //    int? nextRoleId = GetNextRoleId(dto.NewStatusId);
        //    if (nextRoleId.HasValue)
        //    {
        //        var existingUpdate = _context.OrderUpdates
        //            .FirstOrDefault(u => u.OrderId == order.OrderId
        //                              && u.TargetRoleId == nextRoleId.Value);

        //        if (existingUpdate != null)
        //        {
        //            existingUpdate.UpdateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        //        }
        //        else
        //        {
        //            _context.OrderUpdates.Add(new OrderUpdate
        //            {
        //                OrderId = order.OrderId,
        //                RestaurantId = order.RestaurantId,
        //                TargetRoleId = nextRoleId.Value,
        //                UpdateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        //            });
        //        }
        //    }

        //    _context.SaveChanges();

        //    return Ok(new { success = true, message = "Order status updated successfully." });
        //}



        // Add this response class to your OrderApiController
        public class OrderResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public OrderDto OrderData { get; set; }
        }

      



         
        [HttpGet("getallsubscriptions")]

        public async Task<IActionResult> GetAllPlans()
            {
                var plans = await _context.SubscriptionPlans
                    
                    .OrderBy(p => p.Id)
                    .ToListAsync();

                return Ok(plans);
            }
        

    }



}
 