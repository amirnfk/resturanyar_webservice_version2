using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Models.Receipt;
using resturanyar.Services.Receipt;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;

namespace resturanyar.Controllers.Api.V2
{
    public partial class UserApiController
    {
        [HttpGet("orders/{orderId}/receipt/status")]
        public async Task<IActionResult> GetReceiptStatus(int orderId)
        {
            var ownerId = GetOwnerIdFromToken();
            if (ownerId == null)
                return Unauthorized(new { success = false, message = "احراز هویت نامعتبر است." });

            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound(new { success = false, message = "سفارش یافت نشد." });

            if (!await OwnsRestaurant(ownerId.Value, order.RestaurantId))
                return Forbid();

            var result = await GetReceiptService().GetStatusAsync(orderId, order.RestaurantId);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Receipt });
        }

        [HttpPost("orders/{orderId}/receipt/preview")]
        public async Task<IActionResult> PreviewReceipt(int orderId, [FromBody] ReceiptPreviewRequest? request)
        {
            var ownerId = GetOwnerIdFromToken();
            if (ownerId == null)
                return Unauthorized(new { success = false, message = "احراز هویت نامعتبر است." });

            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound(new { success = false, message = "سفارش یافت نشد." });

            if (!await OwnsRestaurant(ownerId.Value, order.RestaurantId))
                return Forbid();

            request ??= new ReceiptPreviewRequest();
            var result = await GetReceiptService().PreviewAsync(orderId, order.RestaurantId, request);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Receipt });
        }

        [HttpGet("orders/{orderId}/receipt/preview-defaults")]
        public async Task<IActionResult> PreviewReceiptDefaults(int orderId)
        {
            var ownerId = GetOwnerIdFromToken();
            if (ownerId == null)
                return Unauthorized(new { success = false, message = "احراز هویت نامعتبر است." });

            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound(new { success = false, message = "سفارش یافت نشد." });

            if (!await OwnsRestaurant(ownerId.Value, order.RestaurantId))
                return Forbid();

            var result = await GetReceiptService().GetPreviewDefaultsAsync(orderId, order.RestaurantId, ownerId);
            return StatusCode(result.StatusCode, new
            {
                success = result.Success,
                message = result.Message,
                data = result.Receipt == null && !result.Success
                    ? null
                    : new
                    {
                        receipt = result.Receipt,
                        appliedCharges = result.AppliedCharges
                    }
            });
        }

        [HttpPost("orders/{orderId}/receipt/issue")]
        public async Task<IActionResult> IssueReceipt(int orderId, [FromBody] ReceiptPreviewRequest? request)
        {
            var ownerId = GetOwnerIdFromToken();
            if (ownerId == null)
                return Unauthorized(new { success = false, message = "احراز هویت نامعتبر است." });

            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound(new { success = false, message = "سفارش یافت نشد." });

            if (!await OwnsRestaurant(ownerId.Value, order.RestaurantId))
                return Forbid();

            request ??= new ReceiptPreviewRequest();
            var result = await GetReceiptService().IssueAsync(orderId, order.RestaurantId, request, ownerId, "Api");
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Receipt });
        }

        [HttpPost("orders/{orderId}/receipt/reissue")]
        public async Task<IActionResult> ReissueReceipt(int orderId, [FromBody] ReceiptPreviewRequest? request)
        {
            var ownerId = GetOwnerIdFromToken();
            if (ownerId == null)
                return Unauthorized(new { success = false, message = "احراز هویت نامعتبر است." });

            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound(new { success = false, message = "سفارش یافت نشد." });

            if (!await OwnsRestaurant(ownerId.Value, order.RestaurantId))
                return Forbid();

            request ??= new ReceiptPreviewRequest();
            var result = await GetReceiptService().ReissueAsync(orderId, order.RestaurantId, request, ownerId, "Api", recordPrintHistory: false);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Receipt });
        }

        [HttpGet("orders/{orderId}/receipt-data")]
        public async Task<IActionResult> GetReceiptData(int orderId)
        {
            var ownerId = GetOwnerIdFromToken();
            if (ownerId == null)
                return Unauthorized(new { success = false, message = "احراز هویت نامعتبر است." });

            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound(new { success = false, message = "سفارش یافت نشد." });

            if (!await OwnsRestaurant(ownerId.Value, order.RestaurantId))
                return Forbid();

            var result = await GetReceiptService().GetReceiptDataAsync(orderId, order.RestaurantId, "Android", ownerId);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Receipt });
        }

        [HttpGet("orders/{orderId}/receipt")]
        public async Task<IActionResult> GetReceiptHtml(int orderId)
        {
            var ownerId = GetOwnerIdFromToken();
            if (ownerId == null)
                return Unauthorized(new { success = false, message = "احراز هویت نامعتبر است." });

            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound(new { success = false, message = "سفارش یافت نشد." });

            if (!await OwnsRestaurant(ownerId.Value, order.RestaurantId))
                return Forbid();

            var service = GetReceiptService();
            var status = await service.GetStatusAsync(orderId, order.RestaurantId);
            if (!status.Success)
                return StatusCode(status.StatusCode, status.Message);

            if (status.Receipt?.UsesCharges == true && !status.Receipt.IsIssued)
                return BadRequest("فاکتور این سفارش هنوز صادر نشده است.");

            var result = status.Receipt?.UsesCharges == true
                ? await service.GetReceiptDataAsync(orderId, order.RestaurantId, "Android", ownerId)
                : await service.PreviewAsync(orderId, order.RestaurantId, new ReceiptPreviewRequest());

            if (!result.Success || result.Receipt == null)
                return StatusCode(result.StatusCode, result.Message ?? "خطا در تولید فاکتور");

            return Content(service.RenderHtml(result.Receipt), "text/html; charset=utf-8");
        }

        [HttpGet("restaurants/{restaurantId}/charge-definitions")]
        public async Task<IActionResult> GetChargeDefinitions(int restaurantId)
        {
            var ownerId = GetOwnerIdFromToken();
            if (ownerId == null)
                return Unauthorized(new { success = false, message = "احراز هویت نامعتبر است." });

            if (!await OwnsRestaurant(ownerId.Value, restaurantId))
                return Forbid();

            var defs = await GetReceiptService().GetChargeDefinitionsAsync(restaurantId);
            return Ok(new { success = true, data = defs });
        }

        [HttpPost("restaurants/{restaurantId}/charge-definitions")]
        public async Task<IActionResult> SaveChargeDefinitions(int restaurantId, [FromBody] SaveChargeDefinitionsRequest request)
        {
            var ownerId = GetOwnerIdFromToken();
            if (ownerId == null)
                return Unauthorized(new { success = false, message = "احراز هویت نامعتبر است." });

            if (!await OwnsRestaurant(ownerId.Value, restaurantId))
                return Forbid();

            var ok = await GetReceiptService().SaveChargeDefinitionsAsync(restaurantId, request?.Definitions ?? new());
            return Ok(new { success = ok, message = ok ? "تنظیمات ذخیره شد." : "ذخیره تنظیمات انجام نشد." });
        }

        private int? GetOwnerIdFromToken()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(id, out var ownerId) ? ownerId : null;
        }

        private async Task<bool> OwnsRestaurant(int ownerId, int restaurantId)
        {
            return await _context.Restaurants.AnyAsync(r => r.restaurant_id == restaurantId && r.owner_id == ownerId);
        }

        private IReceiptService GetReceiptService()
            => HttpContext.RequestServices.GetRequiredService<IReceiptService>();
    }
}
