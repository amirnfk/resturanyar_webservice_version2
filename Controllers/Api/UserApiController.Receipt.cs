using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using resturanyar.Models.Receipt;
using resturanyar.Services.Receipt;

namespace Resturanyar.Controllers.Api
{
    public partial class UserApiController
    {
        [HttpGet("orders/{orderId}/receipt/status")]
        public async Task<IActionResult> GetReceiptStatus(int orderId)
        {
            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound(new { success = false, message = "سفارش یافت نشد." });

            var result = await GetReceiptService().GetStatusAsync(orderId, order.RestaurantId);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Receipt });
        }

        [HttpPost("orders/{orderId}/receipt/preview")]
        public async Task<IActionResult> PreviewReceipt(int orderId, [FromBody] ReceiptPreviewRequest? request)
        {
            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound(new { success = false, message = "سفارش یافت نشد." });

            request ??= new ReceiptPreviewRequest();
            var result = await GetReceiptService().PreviewAsync(orderId, order.RestaurantId, request);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Receipt });
        }

        [HttpGet("orders/{orderId}/receipt/preview-defaults")]
        public async Task<IActionResult> PreviewReceiptDefaults(int orderId)
        {
            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound(new { success = false, message = "سفارش یافت نشد." });

            var result = await GetReceiptService().GetPreviewDefaultsAsync(orderId, order.RestaurantId, null);
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
            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound(new { success = false, message = "سفارش یافت نشد." });

            request ??= new ReceiptPreviewRequest();
            var result = await GetReceiptService().IssueAsync(orderId, order.RestaurantId, request, null, "Api");
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Receipt });
        }

        [HttpPost("orders/{orderId}/receipt/reissue")]
        public async Task<IActionResult> ReissueReceipt(int orderId, [FromBody] ReceiptPreviewRequest? request)
        {
            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound(new { success = false, message = "سفارش یافت نشد." });

            request ??= new ReceiptPreviewRequest();
            var result = await GetReceiptService().ReissueAsync(orderId, order.RestaurantId, request, null, "Api", recordPrintHistory: false);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Receipt });
        }

        [HttpGet("orders/{orderId}/receipt-data")]
        public async Task<IActionResult> GetReceiptData(int orderId)
        {
            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound(new { success = false, message = "سفارش یافت نشد." });

            var result = await GetReceiptService().GetReceiptDataAsync(orderId, order.RestaurantId, "Android", null);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Receipt });
        }

        [HttpGet("restaurants/{restaurantId}/charge-definitions")]
        public async Task<IActionResult> GetChargeDefinitions(int restaurantId)
        {
            var defs = await GetReceiptService().GetChargeDefinitionsAsync(restaurantId);
            return Ok(new { success = true, data = defs });
        }

        private IReceiptService GetReceiptService()
            => HttpContext.RequestServices.GetRequiredService<IReceiptService>();
    }
}
