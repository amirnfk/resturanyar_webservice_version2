using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using resturanyar.Helpers;
using resturanyar.Models;
using resturanyar.Utility;
using System.Security.Claims;

namespace resturanyar.Controllers.Api.V2
{
    public partial class UserApiController
    {
        public class RestaurantIdRequest
        {
            public int RestaurantId { get; set; }
        }

        [AllowAnonymous]
        [HttpPost("checkversion")]
        public IActionResult CheckVersionV2([FromBody] VersionCheckRequest request, [FromServices] IConfiguration config)
        {
            try
            {
                var updateConfig = config.GetSection("UpdateConfig").Get<UpdateConfig>();
                var clientVersion = request?.Version ?? "";

                var response = new VersionCheckResponse
                {
                    forceUpdate = false,
                    softUpdate = false,
                    message = "",
                    updateUrl = updateConfig?.UpdateUrl
                };

                if (updateConfig != null && string.Compare(clientVersion, updateConfig.ForceVersion) < 0)
                {
                    response.forceUpdate = true;
                    response.message = "لطفاً اپلیکیشن را به آخرین نسخه بروزرسانی کنید.";
                    return Ok(response);
                }

                if (updateConfig != null && string.Compare(clientVersion, updateConfig.SoftVersion) < 0)
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
                    updateUrl = config.GetSection("UpdateConfig:UpdateUrl").Value
                });
            }
        }

        [HttpGet("getOrderById/{orderId}")]
        public async Task<IActionResult> GetOrderByIdV2(int orderId)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر است." });

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Fulfillment)
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return NotFound(new { success = false, message = "سفارش یافت نشد" });

            if (await GetOwnedRestaurantAsync(order.RestaurantId, ownerId) == null)
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "دسترسی مجاز نیست." });

            var orderDto = new OrderDto
            {
                OrderId = order.OrderId,
                TableNumber = order.TableNumber,
                StatusId = order.StatusId,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                CustomerId = order.CustomerId,
                CustomerFullName = order.Customer != null ? order.Customer.FullName : null,
                CustomerMobile = order.Customer != null ? order.Customer.Mobile : null,
                Description = order.Description,
                OrderType = (byte)order.OrderType,
                AddressSnapshot = order.Fulfillment?.AddressSnapshot,
                PhoneSnapshot = order.Fulfillment?.PhoneSnapshot,
                CustomerNameSnapshot = order.Fulfillment?.CustomerNameSnapshot,
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

            return Ok(new { success = true, message = "سفارش با موفقیت دریافت شد", orderData = orderDto });
        }

        [HttpPut("UpdateOrder/{orderId}")]
        public async Task<IActionResult> UpdateOrderV2(int orderId, [FromBody] UpdateOrderRequest request)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر است." });

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return NotFound(new { success = false, message = "Order not found." });

            if (await GetOwnedRestaurantAsync(order.RestaurantId, ownerId) == null)
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "دسترسی مجاز نیست." });

            if (request.RestaurantId != order.RestaurantId &&
                await GetOwnedRestaurantAsync(request.RestaurantId, ownerId) == null)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "دسترسی مجاز نیست." });
            }

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

            if (request.CustomerAddressId.HasValue || !string.IsNullOrWhiteSpace(request.AddressText) || request.CustomerId.HasValue)
            {
                var fulfillmentService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.Fulfillment.IOrderFulfillmentService>();
                await fulfillmentService.TryUpdateFulfillmentSnapshotsAsync(
                    order.OrderId,
                    order.RestaurantId,
                    request.CustomerId,
                    request.CustomerAddressId,
                    request.AddressText);
            }

            _context.OrderItems.RemoveRange(order.OrderItems);
            order.OrderItems = new List<OrderItem>();
            foreach (var item in request.Items ?? new List<OrderItemDto>())
            {
                var food = await _context.FoodItems.FindAsync(item.FoodItemId);
                if (food == null)
                    return BadRequest(new { success = false, message = $"FoodItemId {item.FoodItemId} not found." });

                order.OrderItems.Add(new OrderItem
                {
                    FoodItemId = item.FoodItemId,
                    Quantity = item.Quantity,
                    UnitPrice = food.Price,
                    UnitPriceWithDiscount = FoodItemPricing.GetEffectiveSellingPrice(food.Price, food.DiscountPrice),
                    FoodName = food.Name,
                    FoodImageUrl = food.ImageUrl
                });
            }

            int? nextRoleId = GetNextRoleId(request.StatusId, order.OrderType);
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

            var discountService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.DiscountCodes.IDiscountCodeService>();
            if (request.UpdateDiscountCode == true)
            {
                if (string.IsNullOrWhiteSpace(request.DiscountCode))
                {
                    var detach = await discountService.DetachFromOrderAsync(order);
                    if (!detach.Success)
                        return BadRequest(new { success = false, message = detach.Message });
                }
                else
                {
                    var attach = await discountService.AttachToOrderAsync(order, request.DiscountCode);
                    if (!attach.Success)
                        return BadRequest(new { success = false, message = attach.Message });
                }
            }
            else if (order.DiscountCodeId.HasValue)
            {
                var refresh = await discountService.RefreshAttachedUsageAsync(order);
                if (!refresh.Success)
                {
                    // Items changed under the code (e.g. below min order): release usage rather than leave an invalid attach.
                    await discountService.DetachFromOrderAsync(order);
                }
            }

            var courierService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.Fulfillment.IDeliveryCourierService>();
            if (await courierService.TryAutoAssignDefaultDriverAsync(
                    order.OrderId, order.RestaurantId, oldStatusId, order.StatusId))
            {
                await _hubContext.Clients.Group(order.RestaurantId.ToString())
                    .SendAsync("ReceiveOrderUpdate", new { orderId = order.OrderId, message = "driver assigned" });
            }

            try
            {
                var receiptService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.Receipt.IReceiptService>();
                await receiptService.TryAutoIssueOnSettlementAsync(
                    order.OrderId, order.RestaurantId, ownerId, oldStatusId, order.StatusId);
            }
            catch { /* best-effort */ }

            await _hubContext.Clients.Group(order.RestaurantId.ToString())
                .SendAsync("ReceiveOrderUpdate", new
                {
                    orderId = order.OrderId,
                    oldStatusId,
                    newStatusId = order.StatusId,
                    message = $"Order {order.OrderId} updated",
                    updateType = "fullUpdate"
                });

            return Ok(new
            {
                success = true,
                message = "Order updated.",
                orderData = new OrderDto
                {
                    OrderId = order.OrderId,
                    TableNumber = order.TableNumber,
                    StatusId = order.StatusId,
                    CreatedAt = order.CreatedAt,
                    UpdatedAt = order.UpdatedAt,
                    CustomerId = order.CustomerId,
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
                }
            });
        }

        [HttpPost("UpdateOrderStatusWithSignalar/{orderId}")]
        public async Task<IActionResult> UpdateOrderStatusWithSignalRV2(int orderId, [FromBody] UpdateOrderStatusDto dto)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر است." });

            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
                return NotFound(new { success = false, message = "Order not found." });

            if (await GetOwnedRestaurantAsync(order.RestaurantId, ownerId) == null)
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "دسترسی مجاز نیست." });

            if (order.StatusId != dto.CurrentStatusId)
            {
                return Conflict(new
                {
                    success = false,
                    message = "وضعیت سفارش توسط کاربر دیگری تغییر کرده است."
                });
            }

            var previousStatusId = order.StatusId;
            order.StatusId = dto.NewStatusId;
            order.UpdatedAt = DateTime.Now;

            var courierService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.Fulfillment.IDeliveryCourierService>();

            if (previousStatusId == 5 && dto.NewStatusId == 6)
            {
                var fulfillment = await _context.OrderFulfillments.FirstOrDefaultAsync(f => f.OrderId == order.OrderId);
                if (fulfillment != null)
                {
                    courierService.ClearFailureFields(fulfillment);
                    fulfillment.UpdatedAt = DateTime.UtcNow;
                }
            }

            int? nextRoleId = GetNextRoleId(dto.NewStatusId, order.OrderType);
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

            if (await courierService.TryAutoAssignDefaultDriverAsync(
                    order.OrderId, order.RestaurantId, previousStatusId, dto.NewStatusId))
            {
                await _hubContext.Clients.Group(order.RestaurantId.ToString())
                    .SendAsync("ReceiveOrderUpdate", new { orderId = order.OrderId, message = "driver assigned" });
            }

            object? receiptData = null;
            try
            {
                var receiptService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.Receipt.IReceiptService>();
                var issueResult = await receiptService.TryAutoIssueOnSettlementAsync(
                    order.OrderId, order.RestaurantId, ownerId, previousStatusId, dto.NewStatusId);
                if (issueResult.Success && issueResult.Receipt?.IsIssued == true)
                {
                    receiptData = new
                    {
                        isIssued = true,
                        grandTotal = issueResult.Receipt.GrandTotal,
                        issuedAt = issueResult.Receipt.IssuedAt
                    };
                }
            }
            catch { /* best-effort */ }

            try
            {
                var inventoryConsumption = HttpContext.RequestServices
                    .GetRequiredService<resturanyar.Services.Inventory.IOrderInventoryConsumptionService>();
                await inventoryConsumption.HandleStatusChangeAsync(
                    order.OrderId, order.RestaurantId, previousStatusId, dto.NewStatusId);
            }
            catch { /* best-effort */ }

            await _hubContext.Clients.Group(order.RestaurantId.ToString())
                .SendAsync("ReceiveOrderUpdate", new
                {
                    orderId = order.OrderId,
                    newStatusId = order.StatusId,
                    message = $"Order {order.OrderId} updated to status {order.StatusId}"
                });

            return Ok(new { success = true, message = "Order status updated and signal sent successfully.", receipt = receiptData });
        }

        [HttpPost("GetOrdersByRestaurantWithDateFilter")]
        public async Task<IActionResult> GetOrdersByRestaurantWithDateFilterV2([FromBody] OrderDateFilterRequest request)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر است." });

            if (await GetOwnedRestaurantAsync(request.RestaurantId, ownerId) == null)
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "دسترسی مجاز نیست." });

            try
            {
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
                    var fromDate = DateHelper.ShamsiToDateTime(request.FromDate);
                    var toDate = DateHelper.ShamsiToDateTime(request.ToDate).AddDays(1).AddSeconds(-1);
                    query = query.Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate);
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
                        OrderType = (byte)o.OrderType,
                        AddressSnapshot = o.Fulfillment != null ? o.Fulfillment.AddressSnapshot : null,
                        PhoneSnapshot = o.Fulfillment != null ? o.Fulfillment.PhoneSnapshot : null,
                        CustomerNameSnapshot = o.Fulfillment != null ? o.Fulfillment.CustomerNameSnapshot : null,
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

                if (orders.Count > 0)
                {
                    var receiptService = HttpContext.RequestServices
                        .GetRequiredService<resturanyar.Services.Receipt.IReceiptService>();
                    await receiptService.AttachReceiptTotalsForOrderListAsync(orders, request.RestaurantId, userId: ownerId);
                }

                var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);
                return Ok(new PaginatedResponse<OrderDto>
                {
                    Success = true,
                    Data = orders,
                    TotalCount = totalCount,
                    CurrentPage = request.PageNumber,
                    TotalPages = totalPages,
                    HasNextPage = request.PageNumber < totalPages,
                    LastCheck = DateTimeOffset.Now.ToUnixTimeSeconds()
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

        [HttpGet("getallsubscriptions")]
        public async Task<IActionResult> GetAllSubscriptionsV2()
        {
            if (!TryGetOwnerId(out _))
                return Unauthorized(new { success = false, message = "توکن نامعتبر است." });

            var plans = await _context.SubscriptionPlans.OrderBy(p => p.Id).ToListAsync();
            return Ok(plans);
        }

        [HttpPost("getOwnerInfoAndSubscriptions")]
        public async Task<IActionResult> GetOwnerInfoAndSubscriptionsV2([FromBody] RestaurantIdRequest request)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر است." });

            var owner = await _context.Owners.FindAsync(ownerId);
            if (owner == null)
                return Unauthorized(new { success = false, message = "مالک یافت نشد." });

            var restaurant = await GetOwnedRestaurantAsync(request.RestaurantId, ownerId);
            if (restaurant == null)
                return Ok(new { success = false, message = "رستوران متعلق به این کاربر نمی‌باشد" });

            var activeSubscription = await _context.Subscriptions
                .Include(s => s.SubscriptionPlan)
                .Where(s => s.RestaurantId == request.RestaurantId &&
                            s.Status == "Active" &&
                            s.EndDate > DateTime.Now)
                .Select(s => new
                {
                    plan_name = s.SubscriptionPlan.Name,
                    end_date = s.EndDate,
                    days_remaining = (s.EndDate - DateTime.Now).Days,
                    features = new
                    {
                        employee_limit = s.SubscriptionPlan.EmployeeLimit,
                        food_limit = s.SubscriptionPlan.FoodLimit,
                        can_use_web = s.SubscriptionPlan.CanUseWeb,
                        can_use_printer = s.SubscriptionPlan.CanUsePrinter
                    }
                })
                .FirstOrDefaultAsync();

            return Ok(new
            {
                success = true,
                message = "ورود با موفقیت انجام شد",
                owner_name = owner.Name,
                owner_phone = owner.Phone,
                restaurant = new
                {
                    restaurant_id = restaurant.restaurant_id,
                    name = restaurant.name,
                    restaurant_code = restaurant.restaurant_code
                },
                subscription = activeSubscription,
                has_active_subscription = activeSubscription != null
            });
        }

        [HttpPost("getUserPermissions")]
        public async Task<IActionResult> GetUserPermissionsV2([FromBody] RestaurantIdRequest request)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر است." });

            var owner = await _context.Owners.FindAsync(ownerId);
            if (owner == null)
                return Unauthorized(new { success = false, message = "مالک یافت نشد." });

            var restaurant = await GetOwnedRestaurantAsync(request.RestaurantId, ownerId);
            if (restaurant == null)
                return Ok(new { success = false, message = "رستوران متعلق به این کاربر نمی‌باشد" });

            var activeSubscription = await _context.Subscriptions
                .Include(s => s.SubscriptionPlan)
                .Where(s => s.RestaurantId == request.RestaurantId &&
                            s.Status == "Active" &&
                            s.EndDate > DateTime.Now)
                .Select(s => new
                {
                    plan_id = s.SubscriptionPlan.Id,
                    plan_name = s.SubscriptionPlan.Name,
                    plan_code = s.SubscriptionPlan.Code,
                    end_date = s.EndDate,
                    days_remaining = (s.EndDate - DateTime.Now).Days
                })
                .FirstOrDefaultAsync();

            var planCodeToUse = activeSubscription?.plan_code ?? "FREE";

            var subscriptionPlan = await _context.SubscriptionPlans
                .Where(sp => sp.Code == planCodeToUse)
                .Select(sp => new
                {
                    limits = new
                    {
                        max_employees = sp.EmployeeLimit,
                        max_foods = sp.FoodLimit,
                        max_categories = sp.CategoryLimit,
                        max_tables = sp.TableLimit
                    },
                    modules = new
                    {
                        web_access = sp.CanUseWeb,
                        printer_access = sp.CanUsePrinter,
                        menu_sharing = sp.CanShareMenu,
                        goftino_integration = sp.CanUseGoftino,
                        social_chat = sp.CanUseSocialChat,
                        realtime_updates = sp.CanUseRealtime,
                        user_management = sp.CanManageUsers,
                        table_management = sp.CanManageTables,
                        category_management = sp.CanManageCategories,
                        image_upload = sp.CanAddImages,
                        multi_restaurant = sp.CanManageMultipleRestaurants,
                        reports_access = sp.CanAccessReports
                    },
                    plan_info = new
                    {
                        name = sp.Name,
                        code = sp.Code,
                        description = sp.Description,
                        is_active = sp.IsActive
                    }
                })
                .FirstOrDefaultAsync();

            if (subscriptionPlan == null)
                return Ok(new { success = false, message = "پلن اشتراک یافت نشد" });

            return Ok(new
            {
                success = true,
                has_active_subscription = activeSubscription != null,
                message = "دسترسی‌ها با موفقیت دریافت شد",
                user_info = new
                {
                    user_id = owner.Id,
                    user_name = owner.Name,
                    user_phone = owner.Phone,
                    user_role = "owner"
                },
                restaurant_info = new
                {
                    restaurant_id = restaurant.restaurant_id,
                    restaurant_name = restaurant.name,
                    restaurant_code = restaurant.restaurant_code
                },
                subscription_info = new
                {
                    plan_name = subscriptionPlan.plan_info.name,
                    plan_code = subscriptionPlan.plan_info.code,
                    end_date = activeSubscription?.end_date,
                    days_remaining = activeSubscription?.days_remaining ?? 0,
                    is_active = activeSubscription != null
                },
                permissions = new
                {
                    can_access_web = subscriptionPlan.modules.web_access,
                    can_use_printer = subscriptionPlan.modules.printer_access,
                    can_share_menu = subscriptionPlan.modules.menu_sharing,
                    can_use_goftino = subscriptionPlan.modules.goftino_integration,
                    can_use_social_chat = subscriptionPlan.modules.social_chat,
                    can_use_realtime = subscriptionPlan.modules.realtime_updates,
                    can_manage_users = subscriptionPlan.modules.user_management,
                    can_manage_tables = subscriptionPlan.modules.table_management,
                    can_manage_category = subscriptionPlan.modules.category_management,
                    can_upload_images = subscriptionPlan.modules.image_upload,
                    can_manage_multiple_restaurants = subscriptionPlan.modules.multi_restaurant,
                    can_access_reports = subscriptionPlan.modules.reports_access,
                    max_employees_allowed = subscriptionPlan.limits.max_employees,
                    max_foods_allowed = subscriptionPlan.limits.max_foods,
                    max_categories_allowed = subscriptionPlan.limits.max_categories,
                    max_tables_allowed = subscriptionPlan.limits.max_tables,
                    has_premium_access = planCodeToUse != "FREE"
                },
                ui_settings = new
                {
                    show_premium_features = subscriptionPlan.modules.reports_access ||
                                            subscriptionPlan.modules.multi_restaurant,
                    show_advanced_settings = subscriptionPlan.modules.user_management ||
                                             subscriptionPlan.modules.realtime_updates,
                    allow_menu_customization = subscriptionPlan.modules.menu_sharing &&
                                               subscriptionPlan.modules.image_upload
                }
            });
        }

        [HttpPost("createsubscription")]
        public async Task<IActionResult> CreateSubscriptionV2([FromBody] CreateSubscriptionRequest request)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر است." });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var restaurant = await GetOwnedRestaurantAsync(request.RestaurantId, ownerId);
                if (restaurant == null)
                {
                    return Ok(new SubscriptionResponse
                    {
                        Success = false,
                        Message = "رستوران یا مالک یافت نشد"
                    });
                }

                var owner = await _context.Owners.FindAsync(ownerId);
                if (owner == null)
                {
                    return Ok(new SubscriptionResponse
                    {
                        Success = false,
                        Message = "مالک یافت نشد"
                    });
                }

                var subscriptionPlan = await _context.SubscriptionPlans
                    .FirstOrDefaultAsync(sp => sp.Id == request.SubscriptionPlanId && sp.IsActive);
                if (subscriptionPlan == null)
                {
                    return Ok(new SubscriptionResponse
                    {
                        Success = false,
                        Message = "پلن اشتراک یافت نشد یا غیرفعال است"
                    });
                }

                var activeSubscriptions = await _context.Subscriptions
                    .Where(s => s.RestaurantId == request.RestaurantId && s.Status == "Active")
                    .ToListAsync();

                foreach (var sub in activeSubscriptions)
                {
                    sub.Status = "Expired";
                    sub.UpdatedAt = DateTime.Now;
                }

                var subscription = new Subscription
                {
                    RestaurantId = request.RestaurantId,
                    OwnerId = ownerId,
                    SubscriptionPlanId = request.SubscriptionPlanId,
                    SubscriptionPeriod = request.SubscriptionPeriod,
                    Status = "Active",
                    StartDate = DateTime.Now,
                    EndDate = CalculateSubscriptionEndDate(DateTime.Now, request.SubscriptionPeriod),
                    PurchaseDate = DateTime.Now,
                    PricePaid = request.PricePaid,
                    DiscountApplied = request.DiscountApplied,
                    PaymentMethod = request.PaymentMethod,
                    TransactionId = request.TransactionId,
                    IsPaid = true,
                    CafeBazarPurchaseToken = request.CafeBazarPurchaseToken,
                    CafeBazarOrderId = request.CafeBazarOrderId,
                    AutoRenew = request.AutoRenew,
                    NextRenewalDate = request.AutoRenew
                        ? CalculateSubscriptionEndDate(DateTime.Now, request.SubscriptionPeriod)
                        : null,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Subscriptions.Add(subscription);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new SubscriptionResponse
                {
                    Success = true,
                    Message = "اشتراک با موفقیت ایجاد شد",
                    Data = new SubscriptionData
                    {
                        Id = subscription.Id,
                        RestaurantName = restaurant.name,
                        PlanName = subscriptionPlan.Name,
                        Status = subscription.Status,
                        StartDate = subscription.StartDate,
                        EndDate = subscription.EndDate,
                        PricePaid = subscription.PricePaid,
                        AutoRenew = subscription.AutoRenew
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Ok(new SubscriptionResponse
                {
                    Success = false,
                    Message = "خطا در ایجاد اشتراک: " + ex.Message
                });
            }
        }

        [HttpGet("restaurants/{restaurantId}/drivers")]
        [Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> ListDriversOwner(int restaurantId)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر است." });
            if (await GetOwnedRestaurantAsync(restaurantId, ownerId) == null)
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "دسترسی مجاز نیست." });

            var courierService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.Fulfillment.IDeliveryCourierService>();
            var result = await courierService.ListDriversAsync(restaurantId);
            return StatusCode(result.StatusCode, new { success = result.Ok, message = result.Message, data = result.Drivers });
        }

        [HttpPost("orders/{orderId}/assign-driver")]
        [Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> AssignDriverOwner(int orderId, [FromBody] resturanyar.Services.Fulfillment.AssignDriverRequest request)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر است." });

            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return NotFound(new { success = false, message = "سفارش یافت نشد." });
            if (await GetOwnedRestaurantAsync(order.RestaurantId, ownerId) == null)
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "دسترسی مجاز نیست." });

            var courierService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.Fulfillment.IDeliveryCourierService>();
            var result = await courierService.AssignDriverAsync(orderId, order.RestaurantId, request.DriverUserId);
            if (result.Ok)
            {
                await _hubContext.Clients.Group(order.RestaurantId.ToString())
                    .SendAsync("ReceiveOrderUpdate", new { orderId, message = "driver assigned" });
            }
            return StatusCode(result.StatusCode, new { success = result.Ok, message = result.Message });
        }

        [HttpPost("orders/{orderId}/unassign-driver")]
        [Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UnassignDriverOwner(int orderId)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized(new { success = false, message = "توکن نامعتبر است." });

            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return NotFound(new { success = false, message = "سفارش یافت نشد." });
            if (await GetOwnedRestaurantAsync(order.RestaurantId, ownerId) == null)
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "دسترسی مجاز نیست." });

            var courierService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.Fulfillment.IDeliveryCourierService>();
            var result = await courierService.UnassignDriverAsync(orderId, order.RestaurantId);
            if (result.Ok)
            {
                await _hubContext.Clients.Group(order.RestaurantId.ToString())
                    .SendAsync("ReceiveOrderUpdate", new { orderId, message = "driver unassigned" });
            }
            return StatusCode(result.StatusCode, new { success = result.Ok, message = result.Message });
        }
    }
}
