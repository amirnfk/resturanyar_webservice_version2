using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using resturanyar.Models.DiscountCodes;
using resturanyar.Services.DiscountCodes;
using System.Security.Claims;

namespace resturanyar.Controllers.Api.V2
{
    public partial class UserApiController
    {
        [HttpGet("discountCodes/{restaurantId}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetDiscountCodes(int restaurantId, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(restaurantId, ct))
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "دسترسی به این رستوران مجاز نیست." });

            var service = HttpContext.RequestServices.GetRequiredService<IDiscountCodeService>();
            var data = await service.ListAsync(restaurantId, ct);
            return Ok(new { success = true, message = "ok", data });
        }

        [HttpGet("discountCodes/{restaurantId}/{id:int}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetDiscountCode(int restaurantId, int id, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(restaurantId, ct))
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "دسترسی به این رستوران مجاز نیست." });

            var service = HttpContext.RequestServices.GetRequiredService<IDiscountCodeService>();
            var data = await service.GetByIdAsync(restaurantId, id, ct);
            if (data == null)
                return NotFound(new { success = false, message = "کد تخفیف یافت نشد." });

            return Ok(new { success = true, message = "ok", data });
        }

        [HttpPost("discountCodes")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> CreateDiscountCode([FromBody] UpsertDiscountCodeRequest request, CancellationToken ct)
        {
            if (request == null)
                return BadRequest(new { success = false, message = "اطلاعات نامعتبر است." });

            if (!ModelState.IsValid)
            {
                var first = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault();
                return BadRequest(new { success = false, message = string.IsNullOrWhiteSpace(first) ? "اطلاعات فرم نامعتبر است." : first });
            }

            if (!await EnsureOwnedRestaurantAsync(request.RestaurantId, ct))
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "دسترسی به این رستوران مجاز نیست." });

            try
            {
                var service = HttpContext.RequestServices.GetRequiredService<IDiscountCodeService>();
                var result = await service.CreateAsync(request, ct);
                if (!result.Success)
                    return BadRequest(new { success = false, message = result.Message });

                return Ok(new { success = true, message = result.Message, data = result.Data });
            }
            catch (Exception ex) when (IsMissingDiscountSchema(ex))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "جداول/ستون کد تخفیف روی دیتابیس آماده نیست. اسکریپت AddOrderDiscountCodes.sql و در صورت نیاز AddDiscountCodeSpecificCustomer.sql را اجرا کنید."
                });
            }
        }

        [HttpPut("discountCodes/{id:int}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateDiscountCode(int id, [FromBody] UpsertDiscountCodeRequest request, CancellationToken ct)
        {
            if (request == null)
                return BadRequest(new { success = false, message = "اطلاعات نامعتبر است." });

            if (!ModelState.IsValid)
            {
                var first = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault();
                return BadRequest(new { success = false, message = string.IsNullOrWhiteSpace(first) ? "اطلاعات فرم نامعتبر است." : first });
            }

            if (!await EnsureOwnedRestaurantAsync(request.RestaurantId, ct))
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "دسترسی به این رستوران مجاز نیست." });

            try
            {
                var service = HttpContext.RequestServices.GetRequiredService<IDiscountCodeService>();
                var result = await service.UpdateAsync(id, request, ct);
                if (!result.Success)
                    return BadRequest(new { success = false, message = result.Message });

                return Ok(new { success = true, message = result.Message, data = result.Data });
            }
            catch (Exception ex) when (IsMissingDiscountSchema(ex))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "جداول/ستون کد تخفیف روی دیتابیس آماده نیست. اسکریپت AddOrderDiscountCodes.sql و در صورت نیاز AddDiscountCodeSpecificCustomer.sql را اجرا کنید."
                });
            }
        }

        [HttpDelete("discountCodes/{restaurantId}/{id:int}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeleteDiscountCode(int restaurantId, int id, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(restaurantId, ct))
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "دسترسی به این رستوران مجاز نیست." });

            var service = HttpContext.RequestServices.GetRequiredService<IDiscountCodeService>();
            var result = await service.DeleteAsync(restaurantId, id, ct);
            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        [HttpPost("validateDiscountCode")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> ValidateDiscountCode([FromBody] ValidateDiscountCodeRequest request, CancellationToken ct)
        {
            if (request == null)
                return BadRequest(new { success = false, message = "اطلاعات نامعتبر است." });

            if (!await EnsureOwnedRestaurantAsync(request.RestaurantId, ct))
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "دسترسی به این رستوران مجاز نیست." });

            var service = HttpContext.RequestServices.GetRequiredService<IDiscountCodeService>();
            var result = await service.ValidateAsync(request, ct);
            if (!result.Success)
                return Ok(new { success = false, message = result.Message });

            return Ok(new
            {
                success = true,
                message = result.Message,
                data = new
                {
                    result.DiscountCodeId,
                    result.Code,
                    result.Title,
                    result.DiscountType,
                    result.DiscountValue,
                    result.DiscountAmount,
                    result.ItemsSubtotal,
                    result.FinalSubtotalAfterDiscount
                }
            });
        }

        private async Task<bool> EnsureOwnedRestaurantAsync(int restaurantId, CancellationToken ct)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(claim, out var ownerId) || restaurantId <= 0)
                return false;

            return await _context.Restaurants.AsNoTracking()
                .AnyAsync(r => r.restaurant_id == restaurantId && r.owner_id == ownerId, ct);
        }

        private static bool IsMissingDiscountSchema(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException!)
            {
                var msg = e.Message ?? string.Empty;
                if (msg.Contains("RestaurantDiscountCodes", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("SpecificCustomerId", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (e.GetType().Name.Contains("SqlException", StringComparison.OrdinalIgnoreCase))
                {
                    var numberProp = e.GetType().GetProperty("Number");
                    if (numberProp?.GetValue(e) is int number && number is 207 or 208)
                        return true;
                }
            }

            return false;
        }
    }
}
