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

            if (request.CustomerId.HasValue)
            {
                var customerExists = await _context.Customers
                    .AnyAsync(c => c.CustomerId == request.CustomerId.Value && c.RestaurantId == request.RestaurantId && c.IsActive);
                if (!customerExists)
                    return BadRequest(new { success = false, message = "مشتری یافت نشد." });
            }

            TryGetStaffUserId(out int staffUserId);

            var order = new Order
            {
                RestaurantId = request.RestaurantId,
                TableNumber = request.TableNumber,
                StatusId = request.StatusId,
                CustomerId = request.CustomerId,
                OrderType = request.OrderType.HasValue
                    ? (OrderTypeKind)request.OrderType.Value
                    : OrderTypeKind.DineIn,
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

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            try
            {
                var receiptService = HttpContext.RequestServices.GetRequiredService<IReceiptService>();
                await receiptService.TryAutoIssueOnSettlementAsync(
                    order.OrderId, order.RestaurantId, null, 0, order.StatusId);
            }
            catch { /* best-effort */ }

            int? nextRoleId = GetNextRoleId(request.StatusId);
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
        public async Task<IActionResult> GetOrdersByRestaurant(int restaurantId)
        {
            var denied = EnsureRestaurantAccess(restaurantId);
            if (denied != null) return denied;

            var statusIds = new List<int> { 9, 10, 11 };
            var orders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .Where(o => o.RestaurantId == restaurantId)
                .Where(o => !statusIds.Contains(o.StatusId))
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
                .Include(o => o.OrderItems)
                .FirstOrDefault(o => o.OrderId == orderId);

            if (order == null)
                return NotFound(new { success = false, message = "سفارش یافت نشد" });

            var denied = EnsureRestaurantAccess(order.RestaurantId);
            if (denied != null) return denied;

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

            int? nextRoleId = GetNextRoleId(request.StatusId);
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
            var order = _context.Orders.Find(orderId);
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

            var previousStatusId = order.StatusId;
            order.StatusId = dto.NewStatusId;
            order.UpdatedAt = DateTime.Now;

            int? nextRoleId = GetNextRoleId(dto.NewStatusId);
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
        public IActionResult CheckOrderUpdates(int restaurantId, int role2, int role3, int role4, long lastCheck)
        {
            var denied = EnsureRestaurantAccess(restaurantId);
            if (denied != null) return denied;

            bool hasUpdates = _context.OrderUpdates.Any(u =>
                u.RestaurantId == restaurantId &&
                (
                    (role2 == 1 && u.TargetRoleId == 2) ||
                    (role3 == 1 && u.TargetRoleId == 3) ||
                    (role4 == 1 && u.TargetRoleId == 4)
                ) &&
                u.UpdateTime > lastCheck);

            return Ok(new { success = true, hasUpdates });
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

        private IReceiptService GetReceiptService()
            => HttpContext.RequestServices.GetRequiredService<IReceiptService>();
    }
}
