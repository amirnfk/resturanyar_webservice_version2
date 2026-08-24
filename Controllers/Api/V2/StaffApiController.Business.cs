using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using resturanyar.Helpers;
using resturanyar.Models;
using resturanyar.Models.CustomerModels;
using resturanyar.Models.Receipt;
using resturanyar.Services.Receipt;
using resturanyar.Utility;
using System.Security.Claims;

namespace resturanyar.Controllers.Api.V2
{
    public partial class StaffApiController
    {
        // ---- Foods ----

        [HttpGet("getallFoods/{restaurantId}")]
        public async Task<IActionResult> GetAllFoods(int restaurantId)
        {
            var denied = EnsureRestaurantAccess(restaurantId);
            if (denied != null) return denied;

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
                        CostPrice = f.CostPrice,
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

        [HttpGet("getfoodbyid/{id}")]
        public async Task<IActionResult> GetFoodById(int id)
        {
            var food = await _context.FoodItems.FirstOrDefaultAsync(f => f.FoodItemId == id && f.IsActive);
            if (food == null)
                return NotFound(new { success = false, message = "غذا یافت نشد." });

            var denied = EnsureRestaurantAccess(food.RestaurantId);
            if (denied != null) return denied;

            var category = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == food.CategoryId);
            return Ok(new
            {
                success = true,
                data = new
                {
                    food.FoodItemId,
                    food.RestaurantId,
                    Name = food.Name ?? "",
                    Description = food.Description ?? "",
                    ImageUrl = food.ImageUrl ?? "",
                    CategoryName = category?.CategoryName ?? "",
                    CategoryId = food.CategoryId,
                    food.Price,
                    DiscountPrice = food.DiscountPrice ?? 0,
                    food.CostPrice,
                    food.IsAvailable
                }
            });
        }

        [HttpPost("addfood")]
        public async Task<IActionResult> AddFood([FromForm] FoodItemCreateRequest request)
        {
            var denied = EnsureRestaurantAccess(request.RestaurantId);
            if (denied != null) return denied;

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { success = false, message = "نام غذا الزامی است." });
            if (request.Price <= 0)
                return BadRequest(new { success = false, message = "قیمت باید بیشتر از صفر باشد." });

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId && c.RestaurantId == request.RestaurantId);
            if (category == null)
                return BadRequest(new { success = false, message = "دسته‌بندی یافت نشد." });

            string imageUrl = "";
            if (request.Image != null && request.Image.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);
                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(request.Image.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await request.Image.CopyToAsync(stream);
                imageUrl = $"/uploads/{uniqueFileName}";
            }

            var food = new FoodItem
            {
                Name = request.Name.Trim(),
                Description = request.Description ?? "",
                Price = request.Price,
                DiscountPrice = request.DiscountPrice,
                CostPrice = request.CostPrice ?? 0,
                CategoryId = request.CategoryId,
                RestaurantId = request.RestaurantId,
                IsAvailable = request.isAvailable ?? true,
                IsActive = true,
                ImageUrl = imageUrl,
                CreatedAt = DateTime.Now
            };
            _context.FoodItems.Add(food);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "غذا با موفقیت اضافه شد.", foodItemId = food.FoodItemId });
        }

        [HttpPut("updateFood/{id}")]
        public async Task<IActionResult> UpdateFood(int id, [FromForm] FoodItemCreateRequest request)
        {
            var food = await _context.FoodItems.FirstOrDefaultAsync(f => f.FoodItemId == id);
            if (food == null)
                return NotFound(new { success = false, message = "غذا یافت نشد." });

            var denied = EnsureRestaurantAccess(food.RestaurantId);
            if (denied != null) return denied;

            if (request.RestaurantId > 0 && request.RestaurantId != food.RestaurantId)
            {
                var denied2 = EnsureRestaurantAccess(request.RestaurantId);
                if (denied2 != null) return denied2;
            }

            if (!string.IsNullOrWhiteSpace(request.Name)) food.Name = request.Name.Trim();
            if (request.Description != null) food.Description = request.Description;
            if (request.Price > 0) food.Price = request.Price;
            food.DiscountPrice = request.DiscountPrice;
            food.CostPrice = request.CostPrice ?? food.CostPrice;
            if (request.CategoryId > 0) food.CategoryId = request.CategoryId;
            food.IsAvailable = request.isAvailable ?? food.IsAvailable;

            if (request.RemoveImage == 1 || request.RemoveImage == 2)
            {
                food.ImageUrl = "";
            }
            else if (request.Image != null && request.Image.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);
                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(request.Image.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await request.Image.CopyToAsync(stream);
                food.ImageUrl = $"/uploads/{uniqueFileName}";
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "غذا بروزرسانی شد.", data = food });
        }

        [HttpDelete("deleteFood/{id}")]
        public async Task<IActionResult> DeleteFood(int id)
        {
            var food = await _context.FoodItems.FirstOrDefaultAsync(f => f.FoodItemId == id);
            if (food == null)
                return NotFound(new { success = false, message = "غذا یافت نشد." });

            var denied = EnsureRestaurantAccess(food.RestaurantId);
            if (denied != null) return denied;

            food.IsActive = false;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "غذا حذف شد." });
        }

        // ---- Categories ----

        [HttpGet("getcategoriesbyrestaurant/{restaurantId}")]
        public async Task<IActionResult> GetCategories(int restaurantId)
        {
            var denied = EnsureRestaurantAccess(restaurantId);
            if (denied != null) return denied;

            var restaurant = await _context.Restaurants.FirstOrDefaultAsync(r => r.restaurant_id == restaurantId);
            var categories = await _context.Categories
                .Where(c => c.RestaurantId == restaurantId)
                .Select(c => new { c.CategoryId, c.CategoryName, c.CreatedAt })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                restaurant = restaurant == null ? null : new { restaurant.restaurant_id, restaurant.name },
                categories
            });
        }

        [HttpPost("addcategory")]
        public async Task<IActionResult> AddCategory([FromBody] AddCategoryRequest request)
        {
            var denied = EnsureRestaurantAccess(request.RestaurantId);
            if (denied != null) return denied;

            var exists = await _context.Categories.AnyAsync(c =>
                c.RestaurantId == request.RestaurantId &&
                c.CategoryName.ToLower().Trim() == request.CategoryName.ToLower().Trim());
            if (exists)
                return Ok(new { success = false, message = "این دسته‌بندی قبلاً ثبت شده است" });

            var category = new Category
            {
                RestaurantId = request.RestaurantId,
                CategoryName = request.CategoryName.Trim(),
                CreatedAt = DateTime.Now
            };
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "دسته‌بندی اضافه شد", categoryId = category.CategoryId });
        }

        // ---- Tables / customers ----

        [HttpGet("gettablesbyrestaurant/{restaurantId}")]
        public IActionResult GetTables(int restaurantId)
        {
            var denied = EnsureRestaurantAccess(restaurantId);
            if (denied != null) return denied;

            var restaurant = _context.Restaurants.FirstOrDefault(r => r.restaurant_id == restaurantId);
            var tables = _context.RestaurantTables
                .Where(t => t.RestaurantId == restaurantId)
                .Select(t => new { t.TableId, t.TableName, t.Seats, t.CreatedAt })
                .ToList();

            return Ok(new
            {
                success = true,
                restaurant = restaurant == null ? null : new { restaurant.restaurant_id, restaurant.name },
                tables
            });
        }

        [HttpGet("getcustomers/{restaurantId}")]
        public IActionResult GetCustomers(int restaurantId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string search = null)
        {
            var denied = EnsureRestaurantAccess(restaurantId);
            if (denied != null) return denied;

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _context.Customers.Where(c => c.RestaurantId == restaurantId && c.IsActive);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(c => c.FullName.Contains(s) || c.Mobile.Contains(s));
            }

            var total = query.Count();
            var customers = query
                .OrderByDescending(c => c.CustomerId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.CustomerId,
                    c.FullName,
                    c.Mobile,
                    c.Description
                })
                .ToList();

            return Ok(new
            {
                success = true,
                data = customers,
                page,
                pageSize,
                totalCount = total
            });
        }

        [HttpPost("addcustomer")]
        public IActionResult AddCustomer([FromBody] AddCustomerRequest request)
        {
            var denied = EnsureRestaurantAccess(request.RestaurantId);
            if (denied != null) return denied;

            var existingCustomer = _context.Customers
                .FirstOrDefault(c => c.RestaurantId == request.RestaurantId && c.Mobile == request.Mobile);

            if (existingCustomer != null)
            {
                if (!existingCustomer.IsActive)
                {
                    existingCustomer.IsActive = true;
                    existingCustomer.FullName = request.FullName;
                    existingCustomer.Description = request.Description;
                    existingCustomer.UpdatedAt = DateTime.Now;
                    _context.SaveChanges();
                    return Ok(new
                    {
                        success = true,
                        message = "مشتری غیرفعال قبلی با موفقیت فعال و ویرایش شد",
                        customerId = existingCustomer.CustomerId,
                        wasReactivated = true
                    });
                }
                return Ok(new { success = false, message = "این شماره موبایل قبلاً برای این رستوران ثبت شده است" });
            }

            var customer = new Customer
            {
                RestaurantId = request.RestaurantId,
                Mobile = request.Mobile,
                FullName = request.FullName,
                Description = request.Description,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _context.Customers.Add(customer);
            _context.SaveChanges();
            return Ok(new
            {
                success = true,
                message = "مشتری با موفقیت اضافه شد",
                customerId = customer.CustomerId,
                wasReactivated = false
            });
        }

        // ---- Orders ----

        [HttpPost("createorder")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            var denied = EnsureRestaurantAccess(request.RestaurantId);
            if (denied != null) return denied;

            if (IsDeliveryOnlyStaff())
                return StatusCode(403, new { success = false, message = "پیک مجاز به ثبت سفارش نیست." });

            if (request.CustomerId.HasValue)
            {
                var customerExists = await _context.Customers
                    .AnyAsync(c => c.CustomerId == request.CustomerId.Value && c.RestaurantId == request.RestaurantId && c.IsActive);
                if (!customerExists)
                    return BadRequest(new { success = false, message = "مشتری یافت نشد." });
            }

            TryGetStaffUserId(out int staffUserId);

            var restaurant = await _context.Restaurants
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == request.RestaurantId);
            if (restaurant == null)
                return NotFound(new { success = false, message = "رستوران یافت نشد." });

            var fulfillmentService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.Fulfillment.IOrderFulfillmentService>();
            var prepared = await fulfillmentService.ValidateAndPrepareAsync(
                request,
                restaurant.EnableTakeaway,
                restaurant.EnableDelivery);

            if (!prepared.Success)
            {
                if (prepared.StatusCode == 403)
                    return StatusCode(403, new { success = false, message = prepared.ErrorMessage });
                return BadRequest(new { success = false, message = prepared.ErrorMessage });
            }

            var order = new Order
            {
                RestaurantId = request.RestaurantId,
                TableNumber = prepared.ResolvedTableNumber,
                StatusId = request.StatusId,
                CustomerId = request.CustomerId,
                OrderType = prepared.ResolvedOrderType,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                CreatedAtShamsi = DateHelper.ToShamsi(DateTime.Now),
                UpdatedAtShamsi = DateHelper.ToShamsi(DateTime.Now),
                Description = request.Description,
                OrderItems = new List<OrderItem>()
            };

            foreach (var item in request.Items)
            {
                var food = await _context.FoodItems.FindAsync(item.FoodItemId);
                if (food == null)
                    return BadRequest(new { success = false, message = $"آیتم غذایی {item.FoodItemId} یافت نشد." });

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

            var hasDiscountCode = !string.IsNullOrWhiteSpace(request.DiscountCode);
            if (hasDiscountCode)
            {
                var discountService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.DiscountCodes.IDiscountCodeService>();
                var subtotal = resturanyar.Services.DiscountCodes.DiscountCodeService.ComputeItemsSubtotal(order);
                var validation = await discountService.ValidateAsync(new resturanyar.Models.DiscountCodes.ValidateDiscountCodeRequest
                {
                    RestaurantId = request.RestaurantId,
                    Code = request.DiscountCode!,
                    ItemsSubtotal = subtotal,
                    CustomerId = request.CustomerId
                });
                if (!validation.Success)
                    return BadRequest(new { success = false, message = validation.Message });
            }

            await using var orderTx = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                if (hasDiscountCode)
                {
                    var discountService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.DiscountCodes.IDiscountCodeService>();
                    var attach = await discountService.AttachToOrderAsync(order, request.DiscountCode!);
                    if (!attach.Success)
                    {
                        await orderTx.RollbackAsync();
                        return BadRequest(new { success = false, message = attach.Message });
                    }
                }

                await orderTx.CommitAsync();
            }
            catch
            {
                await orderTx.RollbackAsync();
                throw;
            }

            if (prepared.Fulfillment != null)
                await fulfillmentService.AttachFulfillmentAsync(order, prepared.Fulfillment);

            try
            {
                var receiptService = HttpContext.RequestServices.GetRequiredService<IReceiptService>();
                await receiptService.TryAutoIssueOnSettlementAsync(
                    order.OrderId, order.RestaurantId, null, 0, order.StatusId);
            }
            catch { /* best-effort */ }

            int? nextRoleId = GetNextRoleId(request.StatusId, prepared.ResolvedOrderType);
            if (nextRoleId.HasValue)
            {
                _context.OrderUpdates.Add(new OrderUpdate
                {
                    OrderId = order.OrderId,
                    RestaurantId = order.RestaurantId,
                    TargetRoleId = nextRoleId.Value,
                    UpdateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
                await _context.SaveChangesAsync();
            }

            await _hubContext.Clients.Group(order.RestaurantId.ToString())
                .SendAsync("ReceiveOrderUpdate", new
                {
                    orderId = order.OrderId,
                    newStatusId = order.StatusId,
                    message = $"سفارش {order.OrderId} ثبت شد."
                });

            return Ok(new
            {
                success = true,
                message = "سفارش با موفقیت ثبت شد.",
                orderId = order.OrderId,
                orderData = new { orderId = order.OrderId }
            });
        }

        [HttpGet("getordersbyrestaurant/{restaurantId}")]
        public async Task<IActionResult> GetOrdersByRestaurant(int restaurantId, [FromQuery] bool assignedToMe = false)
        {
            var denied = EnsureRestaurantAccess(restaurantId);
            if (denied != null) return denied;

            TryGetStaffUserId(out int staffUserId);

            var statusIds = new List<int> { 9, 10, 11 };
            var query = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Fulfillment!)
                    .ThenInclude(f => f.AssignedDriver)
                .Include(o => o.OrderItems)
                .Where(o => o.RestaurantId == restaurantId)
                .Where(o => !statusIds.Contains(o.StatusId));

            if (assignedToMe || IsDeliveryOnlyStaff())
            {
                query = query.Where(o =>
                    o.OrderType == OrderTypeKind.Delivery
                    && o.Fulfillment != null
                    && o.Fulfillment.AssignedDriverUserId == staffUserId);
            }

            var orders = await query
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
                    AssignedDriverUserId = o.Fulfillment != null ? o.Fulfillment.AssignedDriverUserId : null,
                    AssignedDriverName = o.Fulfillment != null && o.Fulfillment.AssignedDriver != null
                        ? o.Fulfillment.AssignedDriver.name
                        : null,
                    DeliveryFailureReason = o.Fulfillment != null ? o.Fulfillment.DeliveryFailureReason : null,
                    DeliveryFailedAt = o.Fulfillment != null ? o.Fulfillment.DeliveryFailedAt : null,
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

            if (orders.Count > 0)
            {
                var receiptService = HttpContext.RequestServices.GetRequiredService<IReceiptService>();
                await receiptService.AttachReceiptTotalsForOrderListAsync(orders, restaurantId, userId: null);
            }

            return Ok(new
            {
                success = true,
                data = orders,
                lastCheck = DateTimeOffset.Now.ToUnixTimeSeconds()
            });
        }

        [HttpGet("getOrderById/{orderId}")]
        public IActionResult GetOrderById(int orderId)
        {
            var order = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Fulfillment!)
                    .ThenInclude(f => f.AssignedDriver)
                .Include(o => o.OrderItems)
                .FirstOrDefault(o => o.OrderId == orderId);

            if (order == null)
                return NotFound(new { success = false, message = "سفارش یافت نشد" });

            var denied = EnsureRestaurantAccess(order.RestaurantId);
            if (denied != null) return denied;

            if (IsDeliveryOnlyStaff())
            {
                TryGetStaffUserId(out int staffUserId);
                if (order.OrderType != OrderTypeKind.Delivery
                    || order.Fulfillment?.AssignedDriverUserId != staffUserId)
                {
                    return StatusCode(403, new { success = false, message = "دسترسی به این سفارش مجاز نیست." });
                }
            }

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
                AssignedDriverUserId = order.Fulfillment?.AssignedDriverUserId,
                AssignedDriverName = order.Fulfillment?.AssignedDriver?.name,
                DeliveryFailureReason = order.Fulfillment?.DeliveryFailureReason,
                DeliveryFailedAt = order.Fulfillment?.DeliveryFailedAt,
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
        public async Task<IActionResult> UpdateOrder(int orderId, [FromBody] UpdateOrderRequest request)
        {
            if (IsDeliveryOnlyStaff())
                return StatusCode(403, new { success = false, message = "پیک مجاز به ویرایش سفارش نیست." });

            var order = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .FirstOrDefault(o => o.OrderId == orderId);

            if (order == null)
                return NotFound(new { success = false, message = "Order not found." });

            var denied = EnsureRestaurantAccess(order.RestaurantId);
            if (denied != null) return denied;

            if (request.RestaurantId != order.RestaurantId)
            {
                var denied2 = EnsureRestaurantAccess(request.RestaurantId);
                if (denied2 != null) return denied2;
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
            foreach (var item in request.Items)
            {
                var food = _context.FoodItems.Find(item.FoodItemId);
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
                var existingUpdate = _context.OrderUpdates
                    .FirstOrDefault(u => u.OrderId == order.OrderId && u.TargetRoleId == nextRoleId.Value);
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

            _context.SaveChanges();

            var courierService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.Fulfillment.IDeliveryCourierService>();
            if (await courierService.TryAutoAssignDefaultDriverAsync(
                    order.OrderId, order.RestaurantId, oldStatusId, order.StatusId))
            {
                await _hubContext.Clients.Group(order.RestaurantId.ToString())
                    .SendAsync("ReceiveOrderUpdate", new { orderId = order.OrderId, message = "driver assigned" });
            }

            try
            {
                var receiptService = HttpContext.RequestServices.GetRequiredService<IReceiptService>();
                await receiptService.TryAutoIssueOnSettlementAsync(
                    order.OrderId, order.RestaurantId, null, oldStatusId, order.StatusId);
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
                    OrderType = (byte)order.OrderType,
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
        public async Task<IActionResult> UpdateOrderStatusWithSignalR(int orderId, [FromBody] UpdateOrderStatusDto dto)
        {
            var order = _context.Orders
                .Include(o => o.Fulfillment)
                .FirstOrDefault(o => o.OrderId == orderId);
            if (order == null)
                return NotFound(new { success = false, message = "Order not found." });

            var denied = EnsureRestaurantAccess(order.RestaurantId);
            if (denied != null) return denied;

            if (order.StatusId != dto.CurrentStatusId)
            {
                return Conflict(new
                {
                    success = false,
                    message = "وضعیت سفارش توسط کاربر دیگری تغییر کرده است."
                });
            }

            TryGetStaffUserId(out int staffUserId);
            var courierService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.Fulfillment.IDeliveryCourierService>();
            var courierCheck = await courierService.ValidateCourierStatusChangeAsync(
                orderId, order.RestaurantId, staffUserId, IsDeliveryOnlyStaff(),
                dto.CurrentStatusId, dto.NewStatusId);
            if (!courierCheck.Allowed)
                return StatusCode(403, new { success = false, message = courierCheck.Message });

            var previousStatusId = order.StatusId;
            order.StatusId = dto.NewStatusId;
            order.UpdatedAt = DateTime.Now;

            if (previousStatusId == 5 && dto.NewStatusId == 6 && order.Fulfillment != null)
            {
                courierService.ClearFailureFields(order.Fulfillment);
                order.Fulfillment.UpdatedAt = DateTime.UtcNow;
            }

            int? nextRoleId = GetNextRoleId(dto.NewStatusId, order.OrderType);
            if (nextRoleId.HasValue)
            {
                var existingUpdate = _context.OrderUpdates
                    .FirstOrDefault(u => u.OrderId == order.OrderId && u.TargetRoleId == nextRoleId.Value);
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

            _context.SaveChanges();

            if (await courierService.TryAutoAssignDefaultDriverAsync(
                    order.OrderId, order.RestaurantId, previousStatusId, dto.NewStatusId))
            {
                await _hubContext.Clients.Group(order.RestaurantId.ToString())
                    .SendAsync("ReceiveOrderUpdate", new { orderId = order.OrderId, message = "driver assigned" });
            }

            object? receiptData = null;
            try
            {
                var receiptService = HttpContext.RequestServices.GetRequiredService<IReceiptService>();
                var issueResult = await receiptService.TryAutoIssueOnSettlementAsync(
                    order.OrderId, order.RestaurantId, null, previousStatusId, dto.NewStatusId);
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

        [HttpGet("CheckOrderUpdates")]
        public IActionResult CheckOrderUpdates(int restaurantId, int role2, int role3, int role4, long lastCheck, int role5 = 0)
        {
            var denied = EnsureRestaurantAccess(restaurantId);
            if (denied != null) return denied;

            bool hasUpdates = _context.OrderUpdates.Any(u =>
                u.RestaurantId == restaurantId &&
                (
                    (role2 == 1 && u.TargetRoleId == 2) ||
                    (role3 == 1 && u.TargetRoleId == 3) ||
                    (role4 == 1 && u.TargetRoleId == 4) ||
                    (role5 == 1 && u.TargetRoleId == 5)
                ) &&
                u.UpdateTime > lastCheck);

            return Ok(new { success = true, hasUpdates });
        }

        [HttpGet("restaurants/{restaurantId}/drivers")]
        public async Task<IActionResult> ListDrivers(int restaurantId)
        {
            var denied = EnsureRestaurantAccess(restaurantId);
            if (denied != null) return denied;

            if (User.FindFirst("order_permission")?.Value != "1")
                return StatusCode(403, new { success = false, message = "دسترسی تخصیص پیک ندارید." });

            var courierService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.Fulfillment.IDeliveryCourierService>();
            var result = await courierService.ListDriversAsync(restaurantId);
            return StatusCode(result.StatusCode, new { success = result.Ok, message = result.Message, data = result.Drivers });
        }

        [HttpPost("orders/{orderId}/assign-driver")]
        public async Task<IActionResult> AssignDriver(int orderId, [FromBody] resturanyar.Services.Fulfillment.AssignDriverRequest request)
        {
            if (User.FindFirst("order_permission")?.Value != "1")
                return StatusCode(403, new { success = false, message = "دسترسی تخصیص پیک ندارید." });

            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return NotFound(new { success = false, message = "سفارش یافت نشد." });
            var denied = EnsureRestaurantAccess(order.RestaurantId);
            if (denied != null) return denied;

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
        public async Task<IActionResult> UnassignDriver(int orderId)
        {
            if (User.FindFirst("order_permission")?.Value != "1")
                return StatusCode(403, new { success = false, message = "دسترسی تخصیص پیک ندارید." });

            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return NotFound(new { success = false, message = "سفارش یافت نشد." });
            var denied = EnsureRestaurantAccess(order.RestaurantId);
            if (denied != null) return denied;

            var courierService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.Fulfillment.IDeliveryCourierService>();
            var result = await courierService.UnassignDriverAsync(orderId, order.RestaurantId);
            if (result.Ok)
            {
                await _hubContext.Clients.Group(order.RestaurantId.ToString())
                    .SendAsync("ReceiveOrderUpdate", new { orderId, message = "driver unassigned" });
            }
            return StatusCode(result.StatusCode, new { success = result.Ok, message = result.Message });
        }

        [HttpPost("orders/{orderId}/delivery-failed")]
        public async Task<IActionResult> ReportDeliveryFailed(int orderId, [FromBody] resturanyar.Services.Fulfillment.DeliveryFailedRequest request)
        {
            if (!IsDeliveryOnlyStaff() && User.FindFirst("delivery_permission")?.Value != "1")
                return StatusCode(403, new { success = false, message = "فقط پیک می‌تواند تحویل ناموفق را گزارش کند." });

            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return NotFound(new { success = false, message = "سفارش یافت نشد." });
            var denied = EnsureRestaurantAccess(order.RestaurantId);
            if (denied != null) return denied;

            if (!TryGetStaffUserId(out int staffUserId))
                return Unauthorized(new { success = false, message = "احراز هویت نامعتبر است." });

            var courierService = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.Fulfillment.IDeliveryCourierService>();
            var result = await courierService.ReportDeliveryFailedAsync(
                orderId, order.RestaurantId, staffUserId, request?.Reason ?? "", request?.ReasonCode);

            if (result.Ok)
            {
                if (result.NewStatusId is 9 or 10)
                {
                    await _context.Orders
                        .Where(o => o.OrderId == orderId && o.StatusId != result.NewStatusId)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(o => o.StatusId, result.NewStatusId)
                            .SetProperty(o => o.UpdatedAt, DateTime.Now));
                }

                try
                {
                    var inventoryConsumption = HttpContext.RequestServices
                        .GetRequiredService<resturanyar.Services.Inventory.IOrderInventoryConsumptionService>();
                    await inventoryConsumption.HandleStatusChangeAsync(
                        orderId, order.RestaurantId, previousStatusId: 5, result.NewStatusId);
                }
                catch { /* best-effort */ }

                await _hubContext.Clients.Group(order.RestaurantId.ToString())
                    .SendAsync("ReceiveOrderUpdate", new
                    {
                        orderId,
                        message = "delivery failed",
                        statusId = result.NewStatusId,
                        newStatusId = result.NewStatusId
                    });
            }

            return StatusCode(result.StatusCode, new
            {
                success = result.Ok,
                message = result.Message,
                newStatusId = result.NewStatusId
            });
        }

        public class UpdateOrderStatusDto
        {
            public int CurrentStatusId { get; set; }
            public int NewStatusId { get; set; }
        }

        // ---- Receipts ----

        [HttpGet("orders/{orderId}/receipt/status")]
        public async Task<IActionResult> GetReceiptStatus(int orderId)
        {
            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return NotFound(new { success = false, message = "سفارش یافت نشد." });
            var denied = EnsureRestaurantAccess(order.RestaurantId);
            if (denied != null) return denied;

            var result = await GetReceiptService().GetStatusAsync(orderId, order.RestaurantId);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Receipt });
        }

        [HttpPost("orders/{orderId}/receipt/preview")]
        public async Task<IActionResult> PreviewReceipt(int orderId, [FromBody] ReceiptPreviewRequest? request)
        {
            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return NotFound(new { success = false, message = "سفارش یافت نشد." });
            var denied = EnsureRestaurantAccess(order.RestaurantId);
            if (denied != null) return denied;

            request ??= new ReceiptPreviewRequest();
            var result = await GetReceiptService().PreviewAsync(orderId, order.RestaurantId, request);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Receipt });
        }

        [HttpGet("orders/{orderId}/receipt/preview-defaults")]
        public async Task<IActionResult> PreviewReceiptDefaults(int orderId)
        {
            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return NotFound(new { success = false, message = "سفارش یافت نشد." });
            var denied = EnsureRestaurantAccess(order.RestaurantId);
            if (denied != null) return denied;

            TryGetStaffUserId(out int staffUserId);
            var result = await GetReceiptService().GetPreviewDefaultsAsync(orderId, order.RestaurantId, staffUserId);
            return StatusCode(result.StatusCode, new
            {
                success = result.Success,
                message = result.Message,
                data = result.Receipt == null && !result.Success
                    ? null
                    : new { receipt = result.Receipt, appliedCharges = result.AppliedCharges }
            });
        }

        [HttpPost("orders/{orderId}/receipt/discount-code")]
        public async Task<IActionResult> SetReceiptDiscountCode(int orderId, [FromBody] SetReceiptDiscountCodeRequest? request)
        {
            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return NotFound(new { success = false, message = "سفارش یافت نشد." });
            var denied = EnsureRestaurantAccess(order.RestaurantId);
            if (denied != null) return denied;

            var result = await GetReceiptService().SetOrderDiscountCodeAsync(
                orderId,
                order.RestaurantId,
                request?.Code);
            return StatusCode(result.StatusCode, new
            {
                success = result.Success,
                message = result.Message,
                data = result.Receipt
            });
        }

        [HttpPost("orders/{orderId}/receipt/issue")]
        public async Task<IActionResult> IssueReceipt(int orderId, [FromBody] ReceiptPreviewRequest? request)
        {
            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return NotFound(new { success = false, message = "سفارش یافت نشد." });
            var denied = EnsureRestaurantAccess(order.RestaurantId);
            if (denied != null) return denied;

            TryGetStaffUserId(out int staffUserId);
            request ??= new ReceiptPreviewRequest();
            var result = await GetReceiptService().IssueAsync(orderId, order.RestaurantId, request, staffUserId, "Api");
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Receipt });
        }

        [HttpPost("orders/{orderId}/receipt/reissue")]
        public async Task<IActionResult> ReissueReceipt(int orderId, [FromBody] ReceiptPreviewRequest? request)
        {
            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return NotFound(new { success = false, message = "سفارش یافت نشد." });
            var denied = EnsureRestaurantAccess(order.RestaurantId);
            if (denied != null) return denied;

            TryGetStaffUserId(out int staffUserId);
            request ??= new ReceiptPreviewRequest();
            var result = await GetReceiptService().ReissueAsync(orderId, order.RestaurantId, request, staffUserId, "Api", recordPrintHistory: false);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Receipt });
        }

        [HttpGet("orders/{orderId}/receipt-data")]
        public async Task<IActionResult> GetReceiptData(int orderId)
        {
            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return NotFound(new { success = false, message = "سفارش یافت نشد." });
            var denied = EnsureRestaurantAccess(order.RestaurantId);
            if (denied != null) return denied;

            TryGetStaffUserId(out int staffUserId);
            var result = await GetReceiptService().GetReceiptDataAsync(orderId, order.RestaurantId, "Android", staffUserId);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Receipt });
        }

        [HttpGet("restaurants/{restaurantId}/charge-definitions")]
        public async Task<IActionResult> GetChargeDefinitions(int restaurantId)
        {
            var denied = EnsureRestaurantAccess(restaurantId);
            if (denied != null) return denied;

            var defs = await GetReceiptService().GetChargeDefinitionsAsync(restaurantId);
            return Ok(new { success = true, data = defs });
        }

        [HttpGet("getfulfillmentsettings/{restaurantId:int}")]
        public async Task<IActionResult> GetFulfillmentSettings(int restaurantId)
        {
            var denied = EnsureRestaurantAccess(restaurantId);
            if (denied != null) return denied;

            var restaurant = await _context.Restaurants.AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId);
            if (restaurant == null)
                return NotFound(new { success = false, message = "رستوران یافت نشد." });

            var fulfillment = HttpContext.RequestServices.GetRequiredService<resturanyar.Services.Fulfillment.IOrderFulfillmentService>();
            return Ok(new
            {
                success = true,
                data = new resturanyar.Models.Fulfillment.FulfillmentSettingsDto
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
        public async Task<IActionResult> GetAddresses(int customerId)
        {
            var customer = await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.IsActive);
            if (customer == null)
                return NotFound(new { success = false, message = "مشتری یافت نشد." });

            var denied = EnsureRestaurantAccess(customer.RestaurantId);
            if (denied != null) return denied;

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
        public async Task<IActionResult> AddAddress([FromBody] AddAddressRequest request)
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.CustomerId == request.CustomerId && c.IsActive);
            if (customer == null)
                return NotFound(new { success = false, message = "مشتری یافت نشد." });

            var denied = EnsureRestaurantAccess(customer.RestaurantId);
            if (denied != null) return denied;

            if (request.IsDefault)
            {
                var existingDefaults = _context.CustomerAddresses
                    .Where(a => a.CustomerId == request.CustomerId && a.IsDefault);
                foreach (var addr in existingDefaults)
                    addr.IsDefault = false;
            }

            var address = new CustomerAddress
            {
                CustomerId = request.CustomerId,
                Title = request.Title,
                AddressText = request.AddressText,
                Unit = request.Unit,
                Floor = request.Floor,
                PlateNumber = request.PlateNumber,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                IsDefault = request.IsDefault,
                Description = request.Description,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.CustomerAddresses.Add(address);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "آدرس با موفقیت اضافه شد", addressId = address.AddressId });
        }

        private IReceiptService GetReceiptService()
            => HttpContext.RequestServices.GetRequiredService<IReceiptService>();
    }
}
