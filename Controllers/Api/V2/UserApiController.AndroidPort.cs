using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Utility;
using System.Net.Http;
using System.Text;

namespace resturanyar.Controllers.Api.V2
{
    public partial class UserApiController
    {
        public class UpdateOrderStatusDto
        {
            public int CurrentStatusId { get; set; }
            public int NewStatusId { get; set; }
        }

        private class OrderResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public OrderDto? OrderData { get; set; }
        }

        private class UpdateConfig
        {
            public string ForceVersion { get; set; } = string.Empty;
            public string SoftVersion { get; set; } = string.Empty;
            public string UpdateUrl { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }

        private async Task<Restaurant?> GetOwnedRestaurantAsync(int restaurantId, int ownerId)
        {
            return await _context.Restaurants
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId && r.owner_id == ownerId);
        }

        private static string GetStatusName(int statusId)
        {
            return statusId switch
            {
                9 => "لغو توسط مشتری",
                10 => "لغو توسط رستوران",
                11 => "بسته شده",
                _ => "نامشخص"
            };
        }

        private static DateTime CalculateSubscriptionEndDate(DateTime startDate, string period)
        {
            return period switch
            {
                "Monthly" => startDate.AddMonths(1),
                "3Monthly" => startDate.AddMonths(3),
                "6Monthly" => startDate.AddMonths(6),
                "12Monthly" => startDate.AddMonths(12),
                _ => startDate.AddMonths(1)
            };
        }

        // ==================== Android port endpoints (V2 only) ====================

        [AllowAnonymous]
        [HttpPost("otprequest")]
        [EnableRateLimiting("OtpPolicy")]
        public async Task<IActionResult> RequestOtp([FromBody] OtpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                return BadRequest(new { success = false, message = "Phone number is required" });

            var otpCode = new Random().Next(1000, 10000).ToString();
            var otpEntry = new OtpEntry
            {
                PhoneNumber = request.PhoneNumber,
                CodeHash = OtpHelper.HashOtp(otpCode),
                ExpireAt = DateTime.UtcNow.AddMinutes(2),
                Used = false
            };
            _context.OtpEntries.Add(otpEntry);
            await _context.SaveChangesAsync();

            var smsRequest = new
            {
                username = _payamakSettings.Username,
                password = _payamakSettings.Password,
                text = $" رستورانیار دلاویتا ; {otpCode}",
                to = request.PhoneNumber,
                bodyId = _payamakSettings.BodyId
            };

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(smsRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(_payamakSettings.BaseUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonResponse = System.Text.Json.JsonSerializer.Deserialize<PayamakResponse>(responseContent);

                if (!response.IsSuccessStatusCode || jsonResponse?.RetStatus != 1 || jsonResponse.StrRetStatus != "Ok")
                {
                    return StatusCode((int)response.StatusCode, new
                    {
                        success = false,
                        message = $"SMS failed: Status {response.StatusCode}, RetStatus: {jsonResponse?.RetStatus}, StrRetStatus: {jsonResponse?.StrRetStatus}"
                    });
                }

                return Ok(new { success = true, message = "OTP sent successfully" });
            }
            catch
            {
                return StatusCode(500, new { success = false, message = "SMS request failed: " });
            }
        }

        [AllowAnonymous]
        [HttpPost("checkphone")]
        public IActionResult CheckPhone([FromBody] string phone)
        {
            try
            {
                var isRegistered = _context.Owners.Any(u => u.Phone == phone);
                return Ok(new { success = true, isRegistered });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("changepassword")]
        public IActionResult ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                var owner = _context.Owners.FirstOrDefault(o => o.Phone == request.Phone);
                if (owner == null)
                    return Ok(new { success = false, message = "کاربری با این شماره تلفن یافت نشد" });

                owner.Password = EncodePassword(request.NewPassword);
                _context.SaveChanges();

                return Ok(new { success = true, message = "رمز عبور با موفقیت تغییر یافت" });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpGet("getrestaurantsbyowner")]
        public async Task<IActionResult> GetRestaurantsByOwner()
        {
            try
            {
                if (!TryGetOwnerId(out int ownerId))
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

                var owner = await _context.Owners.FindAsync(ownerId);
                if (owner == null)
                    return NotFound(new { success = false, message = "مالک با این شناسه یافت نشد" });

                var restaurants = await _context.Restaurants
                    .Where(r => r.owner_id == ownerId)
                    .Select(r => new { r.restaurant_id, r.name })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    owner = new { owner.Id, owner.Name },
                    restaurants
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpGet("getrestaurantpublicmenutoken/{restaurantId}")]
        public async Task<IActionResult> GetRestaurantPublicMenuToken(int restaurantId)
        {
            try
            {
                if (!TryGetOwnerId(out int ownerId))
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

                var restaurant = await GetOwnedRestaurantAsync(restaurantId, ownerId);
                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران پیدا نشد." });

                return Ok(new { success = true, code = restaurant.PublicMenuToken });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطای سرور: " + ex.Message });
            }
        }

        [HttpGet("getallFoods/{restaurantId}")]
        public async Task<IActionResult> GetAllFoodsByRestaurant(int restaurantId)
        {
            try
            {
                if (!TryGetOwnerId(out int ownerId))
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

                if (await GetOwnedRestaurantAsync(restaurantId, ownerId) == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

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
                            CategoryId = c.CategoryId,
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

        [HttpGet("getfoodbyid/{id}")]
        public async Task<IActionResult> GetFoodById(int id)
        {
            try
            {
                if (!TryGetOwnerId(out int ownerId))
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

                var food = await _context.FoodItems
                    .Where(f => f.FoodItemId == id)
                    .Join(_context.Categories,
                        f => f.CategoryId,
                        c => c.CategoryId,
                        (f, c) => new { f, c })
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

                if (!await _context.Restaurants.AnyAsync(r => r.restaurant_id == food.f.RestaurantId && r.owner_id == ownerId))
                    return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

                var result = new
                {
                    food.f.FoodItemId,
                    food.f.RestaurantId,
                    Name = food.f.Name ?? "",
                    Description = food.f.Description ?? "",
                    ImageUrl = food.f.ImageUrl ?? "",
                    CategoryName = food.c.CategoryName ?? "",
                    CategoryId = food.c.CategoryId,
                    Price = food.f.Price,
                    DiscountPrice = food.f.DiscountPrice ?? 0,
                    CostPrice = food.f.CostPrice ?? 0,
                    IsAvailable = food.f.IsAvailable,
                    IsActive = food.f.IsActive,
                    CreatedAt = food.f.CreatedAt.HasValue ? food.f.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm") : ""
                };

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "آیتم با موفقیت دریافت شد.",
                    Data = result
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

        [HttpGet("GetOrdersByRestaurant/{restaurantId}")]
        public async Task<IActionResult> GetOrdersByRestaurant(int restaurantId)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

            if (await GetOwnedRestaurantAsync(restaurantId, ownerId) == null)
                return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

            var statusIds = new List<int> { 9, 10, 11 };
            var orders = await _context.Orders
                .Include(o => o.Customer)
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
                    CustomerId = o.CustomerId,
                    CustomerFullName = o.Customer != null ? o.Customer.FullName : null,
                    CustomerMobile = o.Customer != null ? o.Customer.Mobile : null,
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
                }).ToListAsync();

            var serverTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            return Ok(new { success = true, data = orders, lastCheck = serverTime });
        }
    }
}