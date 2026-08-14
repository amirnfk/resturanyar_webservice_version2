using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using resturanyar.Models.CustomerModels;
using resturanyar.Models.Fulfillment;
using resturanyar.Services.Fulfillment;

namespace resturanyar.Controllers.Api.V2
{
    public partial class UserApiController
    {
        [HttpGet("getfulfillmentsettings/{restaurantId:int}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetFulfillmentSettings(int restaurantId)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

            var restaurant = await _context.Restaurants.AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId && r.owner_id == ownerId);
            if (restaurant == null)
                return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

            var fulfillment = HttpContext.RequestServices.GetRequiredService<IOrderFulfillmentService>();
            return Ok(new
            {
                success = true,
                data = new FulfillmentSettingsDto
                {
                    EnableTakeaway = restaurant.EnableTakeaway,
                    EnableDelivery = restaurant.EnableDelivery,
                    GlobalEnabled = fulfillment.IsGlobalEnabled(),
                    AutoAssignDeliveryDriver = restaurant.AutoAssignDeliveryDriver,
                    DefaultDeliveryDriverUserId = restaurant.DefaultDeliveryDriverUserId
                }
            });
        }

        [HttpPost("setfulfillmentsettings")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> SetFulfillmentSettings([FromBody] UpdateFulfillmentSettingsRequest request)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

            if (request == null || request.RestaurantId <= 0)
                return BadRequest(new { success = false, message = "درخواست نامعتبر است." });

            var restaurant = await _context.Restaurants
                .FirstOrDefaultAsync(r => r.restaurant_id == request.RestaurantId && r.owner_id == ownerId);
            if (restaurant == null)
                return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

            var courierService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.Fulfillment.IDeliveryCourierService>();
            var validation = await courierService.ValidateFulfillmentDriverSettingsAsync(
                request.RestaurantId, request.EnableDelivery, request.AutoAssignDeliveryDriver,
                request.DefaultDeliveryDriverUserId);
            if (!validation.Valid)
                return BadRequest(new { success = false, message = validation.Message });

            restaurant.EnableTakeaway = request.EnableTakeaway;
            restaurant.EnableDelivery = request.EnableDelivery;
            restaurant.AutoAssignDeliveryDriver = request.AutoAssignDeliveryDriver;
            restaurant.DefaultDeliveryDriverUserId = request.AutoAssignDeliveryDriver
                ? request.DefaultDeliveryDriverUserId
                : restaurant.DefaultDeliveryDriverUserId;
            restaurant.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            var fulfillment = HttpContext.RequestServices.GetRequiredService<IOrderFulfillmentService>();
            return Ok(new
            {
                success = true,
                message = "تنظیمات بیرون‌بر/ارسال ذخیره شد.",
                data = new FulfillmentSettingsDto
                {
                    EnableTakeaway = restaurant.EnableTakeaway,
                    EnableDelivery = restaurant.EnableDelivery,
                    GlobalEnabled = fulfillment.IsGlobalEnabled(),
                    AutoAssignDeliveryDriver = restaurant.AutoAssignDeliveryDriver,
                    DefaultDeliveryDriverUserId = restaurant.DefaultDeliveryDriverUserId
                }
            });
        }

        [HttpGet("getaddresses/{customerId:int}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetAddressesV2(int customerId)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

            var customer = await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.IsActive);
            if (customer == null)
                return NotFound(new { success = false, message = "مشتری یافت نشد." });

            var restaurant = await GetOwnedRestaurantAsync(customer.RestaurantId, ownerId);
            if (restaurant == null)
                return StatusCode(403, new { success = false, message = "دسترسی مجاز نیست." });

            var addresses = await _context.CustomerAddresses.AsNoTracking()
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.UpdatedAt)
                .Select(a => new
                {
                    a.AddressId,
                    a.Title,
                    a.AddressText,
                    a.Unit,
                    a.Floor,
                    a.PlateNumber,
                    a.Latitude,
                    a.Longitude,
                    a.IsDefault,
                    a.Description,
                    a.CreatedAt,
                    a.UpdatedAt
                })
                .ToListAsync();

            return Ok(new { success = true, data = addresses });
        }

        [HttpPost("addaddress")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> AddAddressV2([FromBody] AddAddressRequest request)
        {
            try
            {
                if (!TryGetOwnerId(out int ownerId))
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

                if (request == null || request.CustomerId <= 0)
                    return BadRequest(new { success = false, message = "شناسه مشتری نامعتبر است." });

                if (string.IsNullOrWhiteSpace(request.AddressText))
                    return BadRequest(new { success = false, message = "متن آدرس الزامی است." });

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.CustomerId == request.CustomerId && c.IsActive);
                if (customer == null)
                    return NotFound(new { success = false, message = "مشتری یافت نشد." });

                if (await GetOwnedRestaurantAsync(customer.RestaurantId, ownerId) == null)
                    return StatusCode(403, new { success = false, message = "دسترسی مجاز نیست." });

                if (request.IsDefault)
                {
                    var existingDefaults = await _context.CustomerAddresses
                        .Where(a => a.CustomerId == request.CustomerId && a.IsDefault)
                        .ToListAsync();
                    foreach (var addr in existingDefaults)
                        addr.IsDefault = false;
                }

                string? Clip(string? value, int max)
                {
                    if (string.IsNullOrWhiteSpace(value)) return null;
                    value = value.Trim();
                    return value.Length <= max ? value : value.Substring(0, max);
                }

                var address = new CustomerAddress
                {
                    CustomerId = request.CustomerId,
                    Title = Clip(request.Title, 100) ?? "آدرس",
                    AddressText = Clip(request.AddressText, 1000) ?? string.Empty,
                    Unit = Clip(request.Unit, 10),
                    Floor = Clip(request.Floor, 10),
                    PlateNumber = Clip(request.PlateNumber, 10),
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    IsDefault = request.IsDefault,
                    Description = Clip(request.Description, 500),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.CustomerAddresses.Add(address);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "آدرس با موفقیت اضافه شد", addressId = address.AddressId });
            }
            catch (Exception ex)
            {
                var detail = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { success = false, message = "خطا در ذخیره آدرس: " + detail });
            }
        }

        [HttpPost("editaddress")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> EditAddressV2([FromBody] EditAddressRequest request)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

            if (request == null || request.CustomerId <= 0 || request.AddressId <= 0)
                return BadRequest(new { success = false, message = "درخواست نامعتبر است." });

            if (string.IsNullOrWhiteSpace(request.AddressText))
                return BadRequest(new { success = false, message = "متن آدرس الزامی است." });

            var address = await _context.CustomerAddresses
                .FirstOrDefaultAsync(a => a.AddressId == request.AddressId && a.CustomerId == request.CustomerId);
            if (address == null)
                return NotFound(new { success = false, message = "آدرس یافت نشد." });

            var customer = await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CustomerId == address.CustomerId);
            if (customer == null || await GetOwnedRestaurantAsync(customer.RestaurantId, ownerId) == null)
                return StatusCode(403, new { success = false, message = "دسترسی مجاز نیست." });

            if (request.IsDefault && !address.IsDefault)
            {
                var otherAddresses = _context.CustomerAddresses
                    .Where(a => a.CustomerId == request.CustomerId && a.AddressId != request.AddressId);
                foreach (var a in otherAddresses)
                    a.IsDefault = false;
            }

            static string? Clip(string? value, int max)
            {
                if (string.IsNullOrWhiteSpace(value)) return null;
                value = value.Trim();
                return value.Length <= max ? value : value.Substring(0, max);
            }

            address.Title = Clip(request.Title, 100) ?? "آدرس";
            address.AddressText = request.AddressText.Trim();
            address.Unit = Clip(request.Unit, 10);
            address.Floor = Clip(request.Floor, 10);
            address.PlateNumber = Clip(request.PlateNumber, 10);
            address.Latitude = request.Latitude;
            address.Longitude = request.Longitude;
            address.IsDefault = request.IsDefault;
            address.Description = Clip(request.Description, 500);
            address.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "آدرس به‌روزرسانی شد." });
        }

        [HttpPost("deleteaddress")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeleteAddressV2([FromBody] DeleteAddressRequest request)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

            var address = await _context.CustomerAddresses
                .FirstOrDefaultAsync(a => a.AddressId == request.AddressId && a.CustomerId == request.CustomerId);
            if (address == null)
                return NotFound(new { success = false, message = "آدرس یافت نشد." });

            var customer = await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CustomerId == address.CustomerId);
            if (customer == null || await GetOwnedRestaurantAsync(customer.RestaurantId, ownerId) == null)
                return StatusCode(403, new { success = false, message = "دسترسی مجاز نیست." });

            _context.CustomerAddresses.Remove(address);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "آدرس حذف شد." });
        }
    }
}
