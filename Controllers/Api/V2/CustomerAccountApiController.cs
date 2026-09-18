using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using resturanyar.Models.CustomerAccounts;
using resturanyar.Services.CustomerAccounts;
using Resturanyar.Data;
using System.Security.Claims;

namespace resturanyar.Controllers.Api.V2
{
    [ApiController]
    [ApiVersion("2.0")]
    [Route("api/v{version:apiVersion}/customeraccount")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class CustomerAccountApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ICustomerAccountService _accounts;

        public CustomerAccountApiController(AppDbContext context, ICustomerAccountService accounts)
        {
            _context = context;
            _accounts = accounts;
        }

        [HttpPost("summary")]
        public async Task<IActionResult> GetSummary(
            [FromBody] CustomerAccountLookupRequest request,
            CancellationToken ct)
        {
            if (request == null)
                return BadRequest(new { success = false, message = "درخواست نامعتبر است." });

            var access = await EnsureAccessAsync(request.RestaurantId, requirePaymentForStaffWrite: false, ct);
            if (access != null)
                return access;

            try
            {
                var data = await _accounts.GetAccountSummaryAsync(request.RestaurantId, request.CustomerId, ct);
                return Ok(new { success = true, message = "ok", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex) when (IsSchemaError(ex))
            {
                return BadRequest(new { success = false, message = SchemaMissingMessage });
            }
        }

        [HttpPost("transactions")]
        public async Task<IActionResult> ListTransactions(
            [FromBody] CustomerAccountPagedLookupRequest request,
            CancellationToken ct = default)
        {
            if (request == null)
                return BadRequest(new { success = false, message = "درخواست نامعتبر است." });

            var access = await EnsureAccessAsync(request.RestaurantId, requirePaymentForStaffWrite: false, ct);
            if (access != null)
                return access;

            try
            {
                var data = await _accounts.ListTransactionsAsync(
                    request.RestaurantId,
                    request.CustomerId,
                    request.Page,
                    request.PageSize,
                    ct);
                return Ok(new
                {
                    success = true,
                    message = "ok",
                    data = data.Items,
                    totalCount = data.TotalCount,
                    currentPage = data.Page,
                    pageSize = data.PageSize,
                    totalPages = data.TotalPages
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex) when (IsSchemaError(ex))
            {
                return BadRequest(new { success = false, message = SchemaMissingMessage });
            }
        }

        [HttpPost("purchases")]
        public async Task<IActionResult> ListPurchases(
            [FromBody] CustomerAccountPagedLookupRequest request,
            CancellationToken ct = default)
        {
            if (request == null)
                return BadRequest(new { success = false, message = "درخواست نامعتبر است." });

            var access = await EnsureAccessAsync(request.RestaurantId, requirePaymentForStaffWrite: false, ct);
            if (access != null)
                return access;

            try
            {
                var pageSize = request.PageSize > 0 ? request.PageSize : 20;
                var data = await _accounts.ListPurchaseHistoryAsync(
                    request.RestaurantId,
                    request.CustomerId,
                    request.Page,
                    pageSize,
                    ct);
                return Ok(new
                {
                    success = true,
                    message = "ok",
                    data = data.Items,
                    totalCount = data.TotalCount,
                    currentPage = data.Page,
                    pageSize = data.PageSize,
                    totalPages = data.TotalPages,
                    totalSpent = data.TotalSpent
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex) when (IsSchemaError(ex))
            {
                return BadRequest(new { success = false, message = SchemaMissingMessage });
            }
        }

        [HttpPost("addtransaction")]
        public async Task<IActionResult> AddTransaction(
            [FromBody] AddCustomerAccountTransactionRequest request,
            CancellationToken ct)
        {
            if (request == null)
                return BadRequest(new { success = false, message = "درخواست نامعتبر است." });

            var access = await EnsureAccessAsync(request.RestaurantId, requirePaymentForStaffWrite: true, ct);
            if (access != null)
                return access;

            try
            {
                var isStaff = User.IsInRole("Staff");
                int? ownerId = isStaff ? null : GetActorId();
                int? staffUserId = isStaff ? GetActorId() : null;

                var data = await _accounts.AddTransactionAsync(request, ownerId, staffUserId, ct);
                return Ok(new { success = true, message = "تراکنش ثبت شد.", data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex) when (IsSchemaError(ex))
            {
                return BadRequest(new { success = false, message = SchemaMissingMessage });
            }
        }

        private async Task<IActionResult?> EnsureAccessAsync(
            int restaurantId,
            bool requirePaymentForStaffWrite,
            CancellationToken ct)
        {
            if (restaurantId <= 0)
                return BadRequest(new { success = false, message = "شناسه رستوران الزامی است." });

            if (User.IsInRole("Staff"))
            {
                var claimRestaurant = User.FindFirst("restaurant_id")?.Value;
                if (!int.TryParse(claimRestaurant, out var staffRestaurantId) || staffRestaurantId != restaurantId)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        success = false,
                        message = "دسترسی به این رستوران مجاز نیست."
                    });
                }

                if (requirePaymentForStaffWrite && User.FindFirst("payment_permission")?.Value != "1")
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        success = false,
                        message = "برای ثبت تراکنش حساب مشتری، مجوز مدیریت پرداخت لازم است."
                    });
                }

                return null;
            }

            // Owner (or other non-staff JWT)
            var ownerId = GetActorId();
            if (ownerId == null)
                return Unauthorized(new { success = false, message = "احراز هویت نامعتبر است." });

            var owned = await _context.Restaurants.AsNoTracking()
                .AnyAsync(r => r.restaurant_id == restaurantId && r.owner_id == ownerId.Value, ct);

            if (!owned)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = "دسترسی به این رستوران مجاز نیست."
                });
            }

            return null;
        }

        private int? GetActorId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        private const string SchemaMissingMessage =
            "جداول حساب مشتری هنوز روی دیتابیس ساخته نشده‌اند. لطفاً اسکریپت Scripts/AddCustomerAccount.sql را در SSMS اجرا کنید.";

        private static bool IsSchemaError(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException!)
            {
                var msg = e.Message ?? string.Empty;
                if (msg.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("CustomerAccount", StringComparison.OrdinalIgnoreCase)
                        && msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

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
