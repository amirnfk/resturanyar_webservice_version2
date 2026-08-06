using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using resturanyar.Models.Inventory;
using resturanyar.Services.Inventory;
using Resturanyar.Data;
using System.Security.Claims;

namespace resturanyar.Controllers.Api.V2
{
    [ApiController]
    [ApiVersion("2.0")]
    [Route("api/v{version:apiVersion}/inventory")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class InventoryApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IInventoryService _inventory;
        private readonly IInventoryRecipeService _recipes;
        private readonly IUnitConversionService _units;

        public InventoryApiController(
            AppDbContext context,
            IInventoryService inventory,
            IInventoryRecipeService recipes,
            IUnitConversionService units)
        {
            _context = context;
            _inventory = inventory;
            _recipes = recipes;
            _units = units;
        }

        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings([FromQuery] int restaurantId, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(restaurantId, ct))
                return ForbidResult();

            var data = await _inventory.GetSettingsAsync(restaurantId, ct);
            return Ok(new { success = true, message = "ok", data });
        }

        [HttpPut("settings")]
        public async Task<IActionResult> SetSettings([FromBody] SetInventoryEnabledRequest request, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(request.RestaurantId, ct))
                return ForbidResult();

            try
            {
                var data = await _inventory.UpdateSettingsAsync(
                    request.RestaurantId,
                    request.IsEnabled,
                    request.AutoDeductStatusId,
                    ct);
                var msg = data.IsEnabled ? "تنظیمات انبار ذخیره شد." : "ماژول انبار غیرفعال شد.";
                return Ok(new { success = true, message = msg, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("units")]
        public async Task<IActionResult> GetUnits(CancellationToken ct)
        {
            try
            {
                var data = await _units.GetAllActiveUnitsAsync(ct);
                return Ok(new { success = true, message = "ok", data });
            }
            catch (Exception)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "جدول واحدها هنوز در دیتابیس ایجاد نشده است. اسکریپت InventoryUnit را در SSMS اجرا کنید."
                });
            }
        }

        [HttpGet("units/compatible")]
        public async Task<IActionResult> GetCompatibleUnits([FromQuery] int baseUnitId, CancellationToken ct)
        {
            if (baseUnitId <= 0)
                return BadRequest(new { success = false, message = "baseUnitId الزامی است." });

            try
            {
                var data = await _units.GetCompatibleUnitsAsync(baseUnitId, ct);
                return Ok(new { success = true, message = "ok", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "جدول واحدها هنوز در دیتابیس ایجاد نشده است. اسکریپت InventoryUnit را در SSMS اجرا کنید."
                });
            }
        }

        [HttpGet("items")]
        public async Task<IActionResult> ListItems([FromQuery] int restaurantId, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(restaurantId, ct))
                return ForbidResult();

            try
            {
                var data = await _inventory.ListItemsAsync(restaurantId, activeOnly: true, ct);
                return Ok(new { success = true, message = "ok", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("items/{id:int}")]
        public async Task<IActionResult> GetItem([FromQuery] int restaurantId, int id, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(restaurantId, ct))
                return ForbidResult();

            try
            {
                var data = await _inventory.GetItemAsync(restaurantId, id, ct);
                if (data == null)
                    return NotFound(new { success = false, message = "ماده اولیه یافت نشد." });
                return Ok(new { success = true, message = "ok", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("items")]
        public async Task<IActionResult> CreateItem([FromBody] CreateInventoryItemRequest request, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(request.RestaurantId, ct))
                return ForbidResult();

            try
            {
                var ownerId = GetOwnerId();
                var data = await _inventory.CreateItemAsync(request, ownerId, ct);
                return Ok(new { success = true, message = "ماده اولیه ثبت شد.", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("items/{id:int}")]
        public async Task<IActionResult> UpdateItem(int id, [FromQuery] int restaurantId, [FromBody] UpdateInventoryItemRequest request, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(restaurantId, ct))
                return ForbidResult();

            try
            {
                var data = await _inventory.UpdateItemAsync(restaurantId, id, request, ct);
                if (data == null)
                    return NotFound(new { success = false, message = "ماده اولیه یافت نشد." });
                return Ok(new { success = true, message = "ماده اولیه به‌روزرسانی شد.", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("items/{id:int}")]
        public async Task<IActionResult> DeactivateItem(int id, [FromQuery] int restaurantId, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(restaurantId, ct))
                return ForbidResult();

            try
            {
                var ok = await _inventory.DeactivateItemAsync(restaurantId, id, ct);
                if (!ok)
                    return NotFound(new { success = false, message = "ماده اولیه یافت نشد." });
                return Ok(new { success = true, message = "ماده اولیه غیرفعال شد." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("items/{id:int}/add-stock")]
        public async Task<IActionResult> AddStock(int id, [FromQuery] int restaurantId, [FromBody] AddStockRequest request, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(restaurantId, ct))
                return ForbidResult();

            try
            {
                var data = await _inventory.AddStockAsync(restaurantId, id, request, GetOwnerId(), ct);
                if (data == null)
                    return NotFound(new { success = false, message = "ماده اولیه یافت نشد." });
                return Ok(new { success = true, message = "موجودی افزایش یافت.", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("items/{id:int}/adjust")]
        public async Task<IActionResult> AdjustStock(int id, [FromQuery] int restaurantId, [FromBody] AdjustStockRequest request, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(restaurantId, ct))
                return ForbidResult();

            try
            {
                var data = await _inventory.AdjustStockAsync(restaurantId, id, request, GetOwnerId(), ct);
                if (data == null)
                    return NotFound(new { success = false, message = "ماده اولیه یافت نشد." });
                return Ok(new { success = true, message = "موجودی تعدیل شد.", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("low-stock")]
        public async Task<IActionResult> LowStock([FromQuery] int restaurantId, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(restaurantId, ct))
                return ForbidResult();

            try
            {
                var data = await _inventory.GetLowStockItemsAsync(restaurantId, ct);
                return Ok(new { success = true, message = "ok", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("summary")]
        public async Task<IActionResult> Summary([FromQuery] int restaurantId, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(restaurantId, ct))
                return ForbidResult();

            var data = await _inventory.GetSummaryAsync(restaurantId, lowStockTake: 4, ct);
            return Ok(new { success = true, message = "ok", data });
        }

        [HttpGet("categories")]
        public async Task<IActionResult> ListCategories([FromQuery] int restaurantId, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(restaurantId, ct))
                return ForbidResult();

            try
            {
                var data = await _inventory.ListCategoriesAsync(restaurantId, activeOnly: true, ct);
                return Ok(new { success = true, message = "ok", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory([FromBody] CreateInventoryCategoryRequest request, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(request.RestaurantId, ct))
                return ForbidResult();

            try
            {
                var data = await _inventory.CreateCategoryAsync(request, ct);
                return Ok(new { success = true, message = "دسته‌بندی ثبت شد.", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("foods/{foodItemId:int}/recipe")]
        public async Task<IActionResult> GetRecipe(int foodItemId, [FromQuery] int restaurantId, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(restaurantId, ct))
                return ForbidResult();

            try
            {
                var data = await _recipes.GetRecipeAsync(restaurantId, foodItemId, ct);
                return Ok(new { success = true, message = "ok", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex) when (IsSchemaError(ex))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    success = false,
                    message = RecipeSchemaMissingMessage
                });
            }
        }

        [HttpPut("foods/{foodItemId:int}/recipe")]
        public async Task<IActionResult> SaveRecipe(int foodItemId, [FromBody] SaveInventoryRecipeRequest request, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(request.RestaurantId, ct))
                return ForbidResult();

            try
            {
                var data = await _recipes.SaveRecipeAsync(request.RestaurantId, foodItemId, request, ct);
                return Ok(new { success = true, message = "دستور تهیه ذخیره شد.", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex) when (IsSchemaError(ex))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    success = false,
                    message = RecipeSchemaMissingMessage
                });
            }
        }

        [HttpDelete("foods/{foodItemId:int}/recipe")]
        public async Task<IActionResult> DeleteRecipe(int foodItemId, [FromQuery] int restaurantId, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(restaurantId, ct))
                return ForbidResult();

            try
            {
                await _recipes.DeleteRecipeAsync(restaurantId, foodItemId, ct);
                return Ok(new { success = true, message = "دستور تهیه حذف شد." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex) when (IsSchemaError(ex))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    success = false,
                    message = RecipeSchemaMissingMessage
                });
            }
        }

        [HttpGet("foods-with-recipes")]
        public async Task<IActionResult> FoodsWithRecipes([FromQuery] int restaurantId, CancellationToken ct)
        {
            if (!await EnsureOwnedRestaurantAsync(restaurantId, ct))
                return ForbidResult();

            try
            {
                var data = await _recipes.GetFoodIdsWithRecipesAsync(restaurantId, ct);
                return Ok(new { success = true, message = "ok", data });
            }
            catch (InvalidOperationException)
            {
                // Inventory disabled — no badges
                return Ok(new { success = true, message = "ok", data = Array.Empty<int>() });
            }
            catch (Exception ex) when (IsSchemaError(ex))
            {
                // Recipe tables not created yet — fail soft for FoodList badges
                return Ok(new { success = true, message = "ok", data = Array.Empty<int>() });
            }
        }

        private const string RecipeSchemaMissingMessage =
            "جداول دستور تهیه (InventoryRecipe) هنوز روی دیتابیس ساخته نشده‌اند. لطفاً اسکریپت SQL مربوط به Recipes را در SSMS اجرا کنید.";

        private static bool IsSchemaError(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException!)
            {
                var msg = e.Message ?? string.Empty;
                if (msg.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("InventoryRecipe", StringComparison.OrdinalIgnoreCase)
                        && msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // SQL Server error numbers: 208 = invalid object, 207 = invalid column
                if (e.GetType().Name.Contains("SqlException", StringComparison.OrdinalIgnoreCase))
                {
                    var numberProp = e.GetType().GetProperty("Number");
                    if (numberProp?.GetValue(e) is int number && number is 207 or 208)
                        return true;
                }
            }

            return false;
        }

        private int? GetOwnerId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        private async Task<bool> EnsureOwnedRestaurantAsync(int restaurantId, CancellationToken ct)
        {
            var ownerId = GetOwnerId();
            if (ownerId == null || restaurantId <= 0)
                return false;

            return await _context.Restaurants.AsNoTracking()
                .AnyAsync(r => r.restaurant_id == restaurantId && r.owner_id == ownerId.Value, ct);
        }

        private IActionResult ForbidResult()
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                success = false,
                message = "دسترسی به این رستوران مجاز نیست."
            });
        }
    }
}
