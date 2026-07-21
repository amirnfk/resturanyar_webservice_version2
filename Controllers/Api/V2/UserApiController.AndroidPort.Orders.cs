using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Utility;

namespace resturanyar.Controllers.Api.V2
{
    public partial class UserApiController
    {
        [HttpPost("GetOrdersByRestaurantWithDateFilter")]
        public async Task<IActionResult> GetOrdersByRestaurantWithDateFilter([FromBody] OrderDateFilterRequest request)
        {
            try
            {
                if (!TryGetOwnerId(out int ownerId))
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

                if (await GetOwnedRestaurantAsync(request.RestaurantId, ownerId) == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

                if (request.PageNumber < 1) request.PageNumber = 1;
                if (request.PageSize < 1 || request.PageSize > 100) request.PageSize = 20;

                var statusIds = new List<int> { 9, 10, 11 };
                var query = _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.OrderItems)
                    .Where(o => o.RestaurantId == request.RestaurantId)
                    .Where(o => statusIds.Contains(o.StatusId));

                if (!string.IsNullOrEmpty(request.FromDate) && !string.IsNullOrEmpty(request.ToDate))
                {
                    try
                    {
                        var fromDate = DateHelper.ShamsiToDateTime(request.FromDate);
                        var toDate = DateHelper.ShamsiToDateTime(request.ToDate).AddDays(1).AddSeconds(-1);
                        query = query.Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate);
                    }
                    catch
                    {
                        return BadRequest(new PaginatedResponse<OrderDto>
                        {
                            Success = false,
                            Message = "فرمت تاریخ نامعتبر است",
                            Data = new List<OrderDto>()
                        });
                    }
                }

                var totalCount = await query.CountAsync();
                var orders = await query
                    .OrderByDescending(o => o.CreatedAt)
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
                    })
                    .ToListAsync();

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
            catch
            {
                return BadRequest(new PaginatedResponse<OrderDto>
                {
                    Success = false,
                    Message = "خطا در دریافت سفارش‌ها",
                    Data = new List<OrderDto>()
                });
            }
        }

        [HttpGet("getOrderById/{orderId}")]
        public async Task<IActionResult> GetOrderById(int orderId)
        {
            try
            {
                if (!TryGetOwnerId(out int ownerId))
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                    return NotFound(new OrderResponse { Success = false, Message = "سفارش یافت نشد" });

                if (!await _context.Restaurants.AnyAsync(r => r.restaurant_id == order.RestaurantId && r.owner_id == ownerId))
                    return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

                return Ok(new OrderResponse
                {
                    Success = true,
                    Message = "سفارش با موفقیت دریافت شد",
                    OrderData = MapOrderToDto(order)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new OrderResponse { Success = false, Message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpPost("UpdateOrderStatusWithSignalar/{orderId}")]
        public async Task<IActionResult> UpdateOrderStatusWithSignalar(int orderId, [FromBody] UpdateOrderStatusDto dto)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
                return NotFound(new { success = false, message = "Order not found." });

            if (!await _context.Restaurants.AnyAsync(r => r.restaurant_id == order.RestaurantId && r.owner_id == ownerId))
                return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

            if (order.StatusId != dto.CurrentStatusId)
                return Conflict(new { success = false, message = "وضعیت سفارش توسط کاربر دیگری تغییر کرده است." });

            order.StatusId = dto.NewStatusId;
            order.UpdatedAt = DateTime.Now;

            int? nextRoleId = GetNextRoleId(dto.NewStatusId);
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
            }

            await _context.SaveChangesAsync();

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
                if (!TryGetOwnerId(out int ownerId))
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                    return NotFound(new { success = false, message = "Order not found." });

                if (!await _context.Restaurants.AnyAsync(r => r.restaurant_id == order.RestaurantId && r.owner_id == ownerId))
                    return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

                var oldStatusId = order.StatusId;
                order.TableNumber = request.TableNumber;
                order.RestaurantId = request.RestaurantId;
                order.StatusId = request.StatusId;
                order.UpdatedAt = DateTime.Now;
                order.Description = request.Description;
                order.CustomerId = request.CustomerId;
                order.UpdatedAtShamsi = DateHelper.ToShamsi(DateTime.Now);

                if (string.IsNullOrEmpty(order.CreatedAtShamsi))
                    order.CreatedAtShamsi = DateHelper.ToShamsi(order.CreatedAt);

                _context.OrderItems.RemoveRange(order.OrderItems);
                order.OrderItems = new List<OrderItem>();

                foreach (var item in request.Items)
                {
                    var food = await _context.FoodItems.FindAsync(item.FoodItemId);
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
                }

                await _context.SaveChangesAsync();

                await _hubContext.Clients.Group(order.RestaurantId.ToString())
                    .SendAsync("ReceiveOrderUpdate", new
                    {
                        orderId = order.OrderId,
                        oldStatusId,
                        newStatusId = order.StatusId,
                        message = $"Order {order.OrderId} updated from status {oldStatusId} to {order.StatusId}",
                        updateType = "fullUpdate"
                    });

                return Ok(new OrderResponse
                {
                    Success = true,
                    Message = "سفارش با موفقیت به‌روزرسانی شد",
                    OrderData = MapOrderToDto(order)
                });
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                    errorMessage += " | Inner: " + ex.InnerException.Message;

                return StatusCode(500, new OrderResponse
                {
                    Success = false,
                    Message = $"خطای داخلی سرور: {errorMessage}"
                });
            }
        }

        [HttpGet("CheckOrderUpdates")]
        public async Task<IActionResult> CheckOrderUpdates(int restaurantId, int role2, int role3, int role4, long lastCheck)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

            if (await GetOwnedRestaurantAsync(restaurantId, ownerId) == null)
                return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

            bool hasUpdates = await _context.OrderUpdates.AnyAsync(u =>
                u.RestaurantId == restaurantId &&
                (
                    (role2 == 1 && u.TargetRoleId == 2) ||
                    (role3 == 1 && u.TargetRoleId == 3) ||
                    (role4 == 1 && u.TargetRoleId == 4)
                ) &&
                u.UpdateTime > lastCheck);

            return Ok(new { success = true, hasUpdates });
        }

        [HttpPost("ExportOrdersToExcel")]
        public async Task<IActionResult> ExportOrdersToExcel([FromBody] OrderDateFilterRequest request)
        {
            try
            {
                if (!TryGetOwnerId(out int ownerId))
                    return Unauthorized(new { success = false, message = "توکن نامعتبر یا منقضی شده است." });

                if (await GetOwnedRestaurantAsync(request.RestaurantId, ownerId) == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد یا شما دسترسی ندارید." });

                if (string.IsNullOrEmpty(request.FromDate) || string.IsNullOrEmpty(request.ToDate))
                    return BadRequest("بازه تاریخ معتبر نیست");

                var fromDate = DateHelper.ShamsiToDateTime(request.FromDate);
                var toDate = DateHelper.ShamsiToDateTime(request.ToDate).AddDays(1).AddSeconds(-1);
                var statusIds = new List<int> { 9, 10, 11 };

                var orders = await _context.Orders
                    .Include(o => o.OrderItems)
                    .Include(o => o.Customer)
                    .Where(o => o.RestaurantId == request.RestaurantId)
                    .Where(o => statusIds.Contains(o.StatusId))
                    .Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                if (!orders.Any())
                    return BadRequest("هیچ سفارشی در این بازه زمانی یافت نشد.");

                using var workbook = new XLWorkbook();
                var wsOrders = workbook.Worksheets.Add("خلاصه سفارش‌ها");
                wsOrders.Cell(1, 1).Value = "شناسه سفارش";
                wsOrders.Cell(1, 2).Value = "تاریخ ایجاد (شمسی)";
                wsOrders.Cell(1, 3).Value = "شماره میز";
                wsOrders.Cell(1, 4).Value = "وضعیت";
                wsOrders.Cell(1, 5).Value = "نام مشتری";
                wsOrders.Cell(1, 6).Value = "شماره موبایل";
                wsOrders.Cell(1, 7).Value = "توضیحات";
                wsOrders.Cell(1, 8).Value = "تعداد آیتم‌ها";
                wsOrders.Cell(1, 9).Value = "جمع مبلغ کل (تومان)";

                int row = 2;
                foreach (var o in orders)
                {
                    var totalPrice = o.OrderItems.Sum(i => (decimal)((i.UnitPriceWithDiscount.HasValue && i.UnitPriceWithDiscount > 0 ? i.UnitPriceWithDiscount : i.UnitPrice) * i.Quantity));
                    wsOrders.Cell(row, 1).Value = o.OrderId;
                    wsOrders.Cell(row, 2).Value = o.CreatedAtShamsi ?? DateHelper.ToShamsi(o.CreatedAt);
                    wsOrders.Cell(row, 3).Value = o.TableNumber;
                    wsOrders.Cell(row, 4).Value = GetStatusName(o.StatusId);

                    if (o.Customer != null)
                    {
                        wsOrders.Cell(row, 5).Value = o.Customer.FullName ?? "-";
                        string mobile = o.Customer.Mobile ?? "-";
                        if (!string.IsNullOrEmpty(mobile) && mobile.StartsWith("991"))
                            mobile = "-";
                        wsOrders.Cell(row, 6).Value = mobile;
                    }
                    else
                    {
                        wsOrders.Cell(row, 5).Value = "مشتری مهمان";
                        wsOrders.Cell(row, 6).Value = "-";
                    }

                    wsOrders.Cell(row, 7).Value = o.Description ?? "-";
                    wsOrders.Cell(row, 8).Value = o.OrderItems.Count;
                    wsOrders.Cell(row, 9).Value = totalPrice;
                    row++;
                }

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();
                string fileName = $"OrdersReport_{request.RestaurantId}_{request.FromDate}_{request.ToDate}.xlsx";
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest($"خطا در تولید گزارش: {ex.Message}");
            }
        }

        [AllowAnonymous]
        [HttpPost("checkVersion")]
        public IActionResult CheckVersion([FromBody] VersionCheckRequest request)
        {
            try
            {
                var updateConfig = _configuration.GetSection("UpdateConfig").Get<UpdateConfig>();
                if (updateConfig == null)
                    return Ok(new VersionCheckResponse { message = "نسخه اپلیکیشن شما به‌روز است." });

                var clientVersion = request.Version;
                var response = new VersionCheckResponse
                {
                    forceUpdate = false,
                    softUpdate = false,
                    message = "",
                    updateUrl = updateConfig.UpdateUrl
                };

                if (string.Compare(clientVersion, updateConfig.ForceVersion, StringComparison.Ordinal) < 0)
                {
                    response.forceUpdate = true;
                    response.message = "لطفاً اپلیکیشن را به آخرین نسخه بروزرسانی کنید.";
                    return Ok(response);
                }

                if (string.Compare(clientVersion, updateConfig.SoftVersion, StringComparison.Ordinal) < 0)
                {
                    response.softUpdate = true;
                    response.message = updateConfig.Message;
                }

                if (!response.forceUpdate && !response.softUpdate)
                    response.message = "نسخه اپلیکیشن شما به‌روز است.";

                return Ok(response);
            }
            catch (Exception ex)
            {
                return Ok(new VersionCheckResponse
                {
                    forceUpdate = false,
                    softUpdate = false,
                    message = "خطا در سرور: " + ex.Message,
                    updateUrl = _configuration.GetSection("UpdateConfig:UpdateUrl").Value
                });
            }
        }

        private static OrderDto MapOrderToDto(Order order)
        {
            return new OrderDto
            {
                OrderId = order.OrderId,
                TableNumber = order.TableNumber,
                StatusId = order.StatusId,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                CreatedAtShamsi = order.CreatedAtShamsi,
                UpdatedAtShamsi = order.UpdatedAtShamsi,
                CustomerId = order.CustomerId,
                CustomerFullName = order.Customer?.FullName,
                CustomerMobile = order.Customer?.Mobile,
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
        }
    }
}
