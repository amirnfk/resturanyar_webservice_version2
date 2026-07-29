using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using resturanyar.Models.Receipt;
using resturanyar.Models.ViewModels;
using resturanyar.Services.Receipt;
using resturanyar.Utility;

namespace resturanyar.Controllers
{
    [Authorize]
    public class ReceiptController : Controller
    {
        private readonly IReceiptService _receiptService;
        private readonly Resturanyar.Data.AppDbContext _context;

        public ReceiptController(IReceiptService receiptService, Resturanyar.Data.AppDbContext context)
        {
            _receiptService = receiptService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Status(int orderId)
        {
            var restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return Json(new { success = false, message = "شناسه رستوران مشخص نیست." });

            var result = await _receiptService.GetStatusAsync(orderId, restaurantId.Value);
            return Json(new
            {
                success = result.Success,
                message = result.Message,
                data = result.Receipt == null ? null : new
                {
                    orderId = result.Receipt.OrderId,
                    isIssued = result.Receipt.IsIssued,
                    issuedAt = result.Receipt.IssuedAt,
                    usesCharges = result.Receipt.UsesCharges
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> Preview(int orderId, [FromBody] ReceiptPreviewRequest request)
        {
            var restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return Json(new { success = false, message = "شناسه رستوران مشخص نیست." });

            request ??= new ReceiptPreviewRequest();
            var result = await _receiptService.PreviewAsync(orderId, restaurantId.Value, request);
            return StatusCode(result.StatusCode, new
            {
                success = result.Success,
                message = result.Message,
                data = result.Receipt
            });
        }

        [HttpPost]
        public async Task<IActionResult> Issue(int orderId, [FromBody] ReceiptPreviewRequest request)
        {
            var restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return Json(new { success = false, message = "شناسه رستوران مشخص نیست." });

            request ??= new ReceiptPreviewRequest();
            var userId = User.GetOwnerId();
            var result = await _receiptService.IssueAsync(orderId, restaurantId.Value, request, userId, "Web");
            return StatusCode(result.StatusCode, new
            {
                success = result.Success,
                message = result.Message,
                data = result.Receipt
            });
        }

        [HttpPost]
        public async Task<IActionResult> Reissue(int orderId, [FromBody] ReceiptPreviewRequest request)
        {
            var restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return Json(new { success = false, message = "شناسه رستوران مشخص نیست." });

            request ??= new ReceiptPreviewRequest();
            var userId = User.GetOwnerId();
            var result = await _receiptService.ReissueAsync(orderId, restaurantId.Value, request, userId, "Web", recordPrintHistory: false);
            return StatusCode(result.StatusCode, new
            {
                success = result.Success,
                message = result.Message,
                data = result.Receipt
            });
        }

        [HttpGet]
        public async Task<IActionResult> Data(int orderId, bool recordPrintHistory = true)
        {
            var restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return Json(new { success = false, message = "شناسه رستوران مشخص نیست." });

            var userId = User.GetOwnerId();
            var result = await _receiptService.GetReceiptDataAsync(
                orderId,
                restaurantId.Value,
                "Web",
                userId,
                recordPrintHistory);
            return StatusCode(result.StatusCode, new
            {
                success = result.Success,
                message = result.Message,
                data = result.Receipt
            });
        }

        [HttpGet]
        public async Task<IActionResult> Html(int orderId)
        {
            var restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return BadRequest("شناسه رستوران مشخص نیست.");

            var userId = User.GetOwnerId();
            var status = await _receiptService.GetStatusAsync(orderId, restaurantId.Value);
            if (!status.Success)
                return StatusCode(status.StatusCode, status.Message);

            ReceiptDto? receipt = null;

            if (status.Receipt?.UsesCharges == true)
            {
                if (!status.Receipt.IsIssued)
                    return BadRequest("فاکتور این سفارش هنوز صادر نشده است.");

                var result = await _receiptService.GetReceiptDataAsync(orderId, restaurantId.Value, "Web", userId);
                if (!result.Success || result.Receipt == null)
                    return StatusCode(result.StatusCode, result.Message ?? "خطا در بارگذاری فاکتور");
                receipt = result.Receipt;
            }
            else
            {
                var preview = await _receiptService.PreviewAsync(orderId, restaurantId.Value, new ReceiptPreviewRequest());
                if (!preview.Success || preview.Receipt == null)
                    return StatusCode(preview.StatusCode, preview.Message ?? "خطا در تولید فاکتور");
                receipt = preview.Receipt;
            }

            return Content(_receiptService.RenderHtml(receipt), "text/html; charset=utf-8");
        }

        [HttpGet]
        public async Task<IActionResult> ChargeSettings()
        {
            var restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return RedirectToAction("ChooseRestaurant", "Home");

            var restaurant = await _context.Restaurants
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId.Value);

            if (restaurant == null || !restaurant.ReceiptChargesEnabled)
                return RedirectToAction("Dashboard", "Home");

            var definitions = await _receiptService.EnsureChargeDefinitionsAsync(restaurantId.Value);

            var model = new resturanyar.Models.ViewModels.ReceiptChargeSettingsViewModel
            {
                RestaurantId = restaurantId.Value,
                RestaurantName = restaurant.name,
                Definitions = definitions
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> SaveChargeSettings([FromBody] SaveChargeDefinitionsRequest request)
        {
            var restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return Json(new { success = false, message = "شناسه رستوران مشخص نیست." });

            var ok = await _receiptService.SaveChargeDefinitionsAsync(restaurantId.Value, request?.Definitions ?? new());
            if (!ok)
                return Json(new { success = false, message = "ذخیره تنظیمات انجام نشد. لطفاً دوباره تلاش کنید." });

            return Json(new { success = true, message = "تنظیمات با موفقیت ذخیره شد." });
        }

        [HttpGet]
        public async Task<IActionResult> GetChargeDefinitions()
        {
            var restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return Json(new { success = false, message = "شناسه رستوران مشخص نیست." });

            var defs = await _receiptService.EnsureChargeDefinitionsAsync(restaurantId.Value);
            return Json(new { success = true, data = defs });
        }

        [HttpGet]
        public async Task<IActionResult> PreviewDefaults(int orderId)
        {
            var restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return Json(new { success = false, message = "شناسه رستوران مشخص نیست." });

            var userId = User.GetOwnerId();
            var result = await _receiptService.GetPreviewDefaultsAsync(orderId, restaurantId.Value, userId);
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
    }
}
