using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using resturanyar.Models.CustomerModels;
using resturanyar.Models.ViewModels;
using resturanyar.Utility;
using Resturanyar.Data;
using Resturanyar.Hubs;

namespace resturanyar.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomersController : Controller
    {
    
        private readonly IHubContext<OrderHub> _hubContext;

        private readonly AppDbContext _context;


        public CustomersController(AppDbContext context, IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;

        }

        [HttpPost("addcustomer")]
        public IActionResult AddCustomer([FromBody] AddCustomerRequest request)
        {
            try
            {
                // بررسی وجود رستوران
                var restaurant = _context.Restaurants.Find(request.RestaurantId);
                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد" });

                // جستجوی مشتری با این شماره موبایل در همان رستوران (حتی غیرفعال‌ها)
                var existingCustomer = _context.Customers
                    .FirstOrDefault(c => c.RestaurantId == request.RestaurantId && c.Mobile == request.Mobile);

                if (existingCustomer != null)
                {
                    // اگر مشتری وجود دارد ولی غیرفعال است
                    if (!existingCustomer.IsActive)
                    {
                        // فعال کردن مجدد و به‌روزرسانی اطلاعات
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
                    else
                    {
                        // مشتری فعال وجود دارد
                        return Ok(new { success = false, message = "این شماره موبایل قبلاً برای این رستوران ثبت شده است" });
                    }
                }

                // اگر مشتری وجود نداشت، مشتری جدید بساز
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
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { success = false, message = innerMessage });
            }
        }


        [HttpGet("getcustomersstats/{restaurantId}")]
        public IActionResult GetCustomersStats(int restaurantId)
        {
            try
            {
                var restaurant = _context.Restaurants.Find(restaurantId);
                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد" });

                var totalCount = _context.Customers
                    .Count(c => c.RestaurantId == restaurantId && c.IsActive);

                var activeCount = _context.Customers
                    .Count(c => c.RestaurantId == restaurantId && c.IsActive);

                return Ok(new
                {
                    success = true,
                    totalCount = totalCount,
                    activeCount = activeCount
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }
        [HttpPost("editcustomer")]
        public IActionResult EditCustomer([FromBody] EditCustomerRequest request)
        {
            try
            {
                var customer = _context.Customers
                    .FirstOrDefault(c => c.CustomerId == request.CustomerId && c.RestaurantId == request.RestaurantId);
                if (customer == null)
                    return NotFound(new { success = false, message = "مشتری یافت نشد" });

                // بررسی یکتایی شماره موبایل (به جز خود این مشتری)
                bool mobileExists = _context.Customers
                    .Any(c => c.RestaurantId == request.RestaurantId && c.Mobile == request.Mobile && c.CustomerId != request.CustomerId);
                if (mobileExists)
                    return Ok(new { success = false, message = "این شماره موبایل قبلاً برای مشتری دیگری در این رستوران ثبت شده است" });

                customer.Mobile = request.Mobile;
                customer.FullName = request.FullName;
                customer.Description = request.Description;
                customer.UpdatedAt = DateTime.Now;

                _context.SaveChanges();

                return Ok(new { success = true, message = "مشتری با موفقیت ویرایش شد" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpPost("deletecustomer")] // soft delete
        public IActionResult DeleteCustomer([FromBody] DeleteCustomerRequest request)
        {
            try
            {
                var customer = _context.Customers
                    .FirstOrDefault(c => c.CustomerId == request.CustomerId && c.RestaurantId == request.RestaurantId);
                if (customer == null)
                    return NotFound(new { success = false, message = "مشتری یافت نشد" });

                customer.IsActive = false;
                customer.UpdatedAt = DateTime.Now;
                _context.SaveChanges();

                return Ok(new { success = true, message = "مشتری با موفقیت حذف شد (غیرفعال)" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpGet("getcustomers/{restaurantId}")]
        public IActionResult GetCustomers(int restaurantId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string search = null)
        {
            try
            {
                var restaurant = _context.Restaurants.Find(restaurantId);
                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد" });

                var query = _context.Customers
                    .Where(c => c.RestaurantId == restaurantId && c.IsActive);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(c => c.Mobile.Contains(search) ||
                                             (c.FullName != null && c.FullName.Contains(search)) ||
                                             (c.Description != null && c.Description.Contains(search)));
                }

                var totalCount = query.Count();
                var customers = query
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new
                    {
                        c.CustomerId,
                        c.Mobile,
                        c.FullName,
                        c.Description,
                        c.CreatedAt,
                        c.UpdatedAt
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data = customers,
                    totalCount = totalCount,
                    currentPage = page,
                    pageSize = pageSize,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }


        [HttpPost("addaddress")]
        public IActionResult AddAddress([FromBody] AddAddressRequest request)
        {
            try
            {
                var customer = _context.Customers.Find(request.CustomerId);
                if (customer == null)
                    return NotFound(new { success = false, message = "مشتری یافت نشد" });

                // اگر آدرس جدید به عنوان پیش‌فرض انتخاب شده، سایر آدرس‌های آن مشتری را غیرپیش‌فرض کن
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
                _context.SaveChanges();

                return Ok(new { success = true, message = "آدرس با موفقیت اضافه شد", addressId = address.AddressId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpPost("editaddress")]
        public IActionResult EditAddress([FromBody] EditAddressRequest request)
        {
            try
            {
                var address = _context.CustomerAddresses
                    .FirstOrDefault(a => a.AddressId == request.AddressId && a.CustomerId == request.CustomerId);
                if (address == null)
                    return NotFound(new { success = false, message = "آدرس یافت نشد" });

                // اگر این آدرس را پیش‌فرض می‌کنیم، سایر آدرس‌های مشتری را غیرپیش‌فرض کن
                if (request.IsDefault && !address.IsDefault)
                {
                    var otherAddresses = _context.CustomerAddresses
                        .Where(a => a.CustomerId == request.CustomerId && a.AddressId != request.AddressId);
                    foreach (var a in otherAddresses)
                        a.IsDefault = false;
                }

                address.Title = request.Title;
                address.AddressText = request.AddressText;
                address.Unit = request.Unit;
                address.Floor = request.Floor;
                address.PlateNumber = request.PlateNumber;
                address.Latitude = request.Latitude;
                address.Longitude = request.Longitude;
                address.IsDefault = request.IsDefault;
                address.Description = request.Description;
                address.UpdatedAt = DateTime.Now;

                _context.SaveChanges();

                return Ok(new { success = true, message = "آدرس با موفقیت ویرایش شد" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpPost("deleteaddress")]
        public IActionResult DeleteAddress([FromBody] DeleteAddressRequest request)
        {
            try
            {
                var address = _context.CustomerAddresses
                    .FirstOrDefault(a => a.AddressId == request.AddressId && a.CustomerId == request.CustomerId);
                if (address == null)
                    return NotFound(new { success = false, message = "آدرس یافت نشد" });

                _context.CustomerAddresses.Remove(address);
                _context.SaveChanges();

                return Ok(new { success = true, message = "آدرس با موفقیت حذف شد" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpGet("getaddresses/{customerId}")]
        public IActionResult GetAddresses(int customerId)
        {
            try
            {
                var customer = _context.Customers.Find(customerId);
                if (customer == null)
                    return NotFound(new { success = false, message = "مشتری یافت نشد" });

                var addresses = _context.CustomerAddresses
                    .Where(a => a.CustomerId == customerId)
                    .OrderByDescending(a => a.IsDefault)
                    .ThenByDescending(a => a.CreatedAt)
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
                    .ToList();

                return Ok(new { success = true, data = addresses });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }


        [HttpGet("getcustomerswithstats/{restaurantId}")]
        public async Task<IActionResult> GetCustomersWithStats(
            int restaurantId,
            int page = 1,
            int pageSize = 12,
            string search = "",
            string sortBy = "TotalSpent",    
            string period = "all",           
            DateTime? from = null,
            DateTime? to = null)
        {
            // تعیین بازه زمانی
            DateTime startDate, endDate;
            if (period != "all" && !from.HasValue)
            {
                var now = DateTime.Now;
                if (period == "today")
                { startDate = now.Date; endDate = now.Date.AddDays(1).AddTicks(-1); }
                else if (period == "week")
                { startDate = now.Date.AddDays(-7); endDate = now; }
                else if (period == "month")
                { startDate = new DateTime(now.Year, now.Month, 1); endDate = now; }
                else if (period == "year")
                { startDate = now.Date.AddYears(-1); endDate = now; }
                else
                { startDate = DateTime.MinValue; endDate = DateTime.MaxValue; }
            }
            else
            {
                startDate = from ?? DateTime.MinValue;
                endDate = to ?? DateTime.MaxValue;
                if (endDate.TimeOfDay == TimeSpan.Zero)
                    endDate = endDate.Date.AddDays(1).AddTicks(-1);
            }

            // کوئری مشتریان (فقط فعال‌ها - بنا به نیاز می‌توانید IsActive را حذف کنید)
            var customersQuery = _context.Customers
                .Where(c => c.RestaurantId == restaurantId && c.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.Trim().ToLower();
                customersQuery = customersQuery.Where(c =>
                    (c.FullName != null && c.FullName.ToLower().Contains(searchLower)) ||
                    c.Mobile.Contains(search) ||
                    (c.Description != null && c.Description.ToLower().Contains(searchLower)));
            }

            var customers = await customersQuery.ToListAsync();
            var customerIds = customers.Select(c => c.CustomerId).ToList();

            // کوئری سفارشات (فقط وضعیت نهایی - عدد 11 را با وضعیت خودتان جایگزین کنید)
            var ordersQuery = _context.Orders
                .Where(o => customerIds.Contains(o.CustomerId.Value) &&
                            o.RestaurantId == restaurantId &&
                            o.CreatedAt >= startDate && o.CreatedAt <= endDate &&
                            o.StatusId == 11) // وضعیت "بسته شده" یا هر وضعیت نهایی دیگر
                .Include(o => o.OrderItems)
                .AsQueryable();

            var orders = await ordersQuery.ToListAsync();

            // محاسبه آمار برای هر مشتری
            var stats = customers.Select(c => new CustomerStatsViewModel
            {
                CustomerId = c.CustomerId,
                FullName = c.FullName,
                Mobile = c.Mobile,
                Description = c.Description,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                CreatedAtShamsi = DateHelper.ToShamsi(c.CreatedAt),

                TotalOrders = orders.Where(o => o.CustomerId == c.CustomerId).Count(),
                TotalDistinctDays = orders.Where(o => o.CustomerId == c.CustomerId)
                                           .Select(o => o.CreatedAt.Date).Distinct().Count(),
                TotalSpent = orders.Where(o => o.CustomerId == c.CustomerId)
                                   .Sum(o => o.OrderItems.Sum(oi =>
                                       (oi.UnitPriceWithDiscount ?? oi.UnitPrice) * oi.Quantity)),

                LastOrderDate = orders.Where(o => o.CustomerId == c.CustomerId)
                                      .Max(o => (DateTime?)o.CreatedAt),

                // ✅ محاسبه مبلغ آخرین خرید
                LastOrderAmount = orders.Where(o => o.CustomerId == c.CustomerId)
                                        .OrderByDescending(o => o.CreatedAt)
                                        .Take(1)
                                        .Select(o => o.OrderItems.Sum(oi =>
                                            (oi.UnitPriceWithDiscount ?? oi.UnitPrice) * oi.Quantity))
                                        .FirstOrDefault()
            }).ToList();

            // محاسبه میانگین و تاریخ شمسی آخرین سفارش
            foreach (var s in stats)
            {
                s.AverageOrderValue = s.TotalOrders > 0 ? s.TotalSpent / s.TotalOrders : 0;
                s.LastOrderDateShamsi = s.LastOrderDate.HasValue ? DateHelper.ToShamsi(s.LastOrderDate.Value) : "-";
                // اگر مقدار LastOrderAmount صفر باشد (یعنی سفارشی ندارد) می‌توانید آن را null کنید، اما عدد 0 هم قابل قبول است
            }

            // مرتب‌سازی (اختیاری اضافه کردن LastOrderAmount)
            var orderedStats = sortBy switch
            {
                "TotalOrders" => stats.OrderByDescending(x => x.TotalOrders),
                "TotalDistinctDays" => stats.OrderByDescending(x => x.TotalDistinctDays),
                "LastOrderAmount" => stats.OrderByDescending(x => x.LastOrderAmount),
                _ => stats.OrderByDescending(x => x.TotalSpent)
            };

            // صفحه‌بندی
            var totalItems = orderedStats.Count();
            var pagedStats = orderedStats.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Ok(new
            {
                success = true,
                data = pagedStats,
                totalCount = totalItems,
                currentPage = page,
                pageSize = pageSize,
                totalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
            });
        }

        [HttpGet("getdashboardstats/{restaurantId}")]
        public async Task<IActionResult> GetCustomerDashboardStats(int restaurantId)
        {
            try
            {
                var today = DateTime.Today;
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek + 1); // فرض شروع هفته از شنبه (1=Monday? باید تنظیم کنید)
                var startOfMonth = new DateTime(today.Year, today.Month, 1);

                // مشتریان کل
                var allActiveCustomers = _context.Customers
                    .Where(c => c.RestaurantId == restaurantId && c.IsActive);

                var newToday = await allActiveCustomers.CountAsync(c => c.CreatedAt >= today);
                var newThisWeek = await allActiveCustomers.CountAsync(c => c.CreatedAt >= startOfWeek);
                var newThisMonth = await allActiveCustomers.CountAsync(c => c.CreatedAt >= startOfMonth);
                var totalActive = await allActiveCustomers.CountAsync();

                // سفارشات بسته شده (وضعیت نهایی 11)
                var closedOrders = _context.Orders
                    .Where(o => o.RestaurantId == restaurantId && o.StatusId == 11)
                    .Include(o => o.OrderItems);

                var totalOrders = await closedOrders.CountAsync();
                var totalRevenue = await closedOrders
                    .SumAsync(o => o.OrderItems.Sum(oi =>
                        (oi.UnitPriceWithDiscount ?? oi.UnitPrice) * oi.Quantity));

                var avgRevenuePerCustomer = totalActive > 0 ? totalRevenue / totalActive : 0;
                var avgOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;

                // مشتری با بیشترین خرید
                var customerSpending = await _context.Orders
                    .Where(o => o.RestaurantId == restaurantId && o.StatusId == 11 && o.CustomerId != null)
                    .GroupBy(o => o.CustomerId)
                    .Select(g => new
                    {
                        CustomerId = g.Key,
                        TotalSpent = g.Sum(o => o.OrderItems.Sum(oi =>
                            (oi.UnitPriceWithDiscount ?? oi.UnitPrice) * oi.Quantity)),
                        OrderCount = g.Count()
                    })
                    .OrderByDescending(x => x.TotalSpent)
                    .FirstOrDefaultAsync();

                string topCustomerName = "-";
                decimal topCustomerTotal = 0;
                int topCustomerOrders = 0;

                if (customerSpending != null)
                {
                    var customer = await _context.Customers
                        .Where(c => c.CustomerId == customerSpending.CustomerId)
                        .Select(c => c.FullName)
                        .FirstOrDefaultAsync();
                    topCustomerName = customer ?? "مشتری ناشناس";
                    topCustomerTotal = customerSpending.TotalSpent;
                    topCustomerOrders = customerSpending.OrderCount;
                }

                // آمار ۷ روز اخیر (برای نمودار)
                var last7Days = new List<DailyCustomerStat>();
                for (int i = 6; i >= 0; i--)
                {
                    var day = today.AddDays(-i);
                    var nextDay = day.AddDays(1);
                    var newCustomers = await _context.Customers
                        .CountAsync(c => c.RestaurantId == restaurantId &&
                                         c.CreatedAt >= day && c.CreatedAt < nextDay);
                    var revenue = await _context.Orders
                        .Where(o => o.RestaurantId == restaurantId && o.StatusId == 11 &&
                                    o.CreatedAt >= day && o.CreatedAt < nextDay)
                        .SumAsync(o => o.OrderItems.Sum(oi =>
                            (oi.UnitPriceWithDiscount ?? oi.UnitPrice) * oi.Quantity));

                    last7Days.Add(new DailyCustomerStat
                    {
                        Date = day,
                        PersianDate = DateHelper.ToShamsi(day),
                        NewCustomers = newCustomers,
                        Revenue = revenue
                    });
                }

                var stats = new CustomerDashboardStatsViewModel
                {
                    NewCustomersToday = newToday,
                    NewCustomersThisWeek = newThisWeek,
                    NewCustomersThisMonth = newThisMonth,
                    TotalActiveCustomers = totalActive,
                    TotalRevenue = totalRevenue,
                    AverageRevenuePerCustomer = avgRevenuePerCustomer,
                    AverageOrderValue = avgOrderValue,
                    TotalOrders = totalOrders,
                    TopCustomerName = topCustomerName,
                    TopCustomerTotalSpent = topCustomerTotal,
                    TopCustomerOrders = topCustomerOrders,
                    Last7DaysStats = last7Days
                };

                return Ok(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("getcustomerinsights/{restaurantId}")]
        public async Task<IActionResult> GetCustomerInsights(int restaurantId)
        {
            try
            {
               
                var today = DateTime.Today;
                var last7DaysStart = today.AddDays(-7);
                var last30DaysStart = today.AddDays(-30);

               
                var allActiveCustomers = _context.Customers
                    .Where(c => c.RestaurantId == restaurantId && c.IsActive);

                // سفارشات بسته شده (وضعیت نهایی)
                var closedOrders = _context.Orders
                    .Where(o => o.RestaurantId == restaurantId && o.StatusId == 11)
                    .Include(o => o.OrderItems)
                    .AsQueryable();

                // ---------- کارت اول: رشد مشتریان ۷ روزه (سه مشتری با بیشترین خرید در ۷ روز اخیر) ----------
                var topCustomersLast7Days = await closedOrders
                    .Where(o => o.CreatedAt >= last7DaysStart && o.CustomerId != null)
                    .GroupBy(o => o.CustomerId)
                    .Select(g => new
                    {
                        CustomerId = g.Key,
                        TotalSpent = g.Sum(o => o.OrderItems.Sum(oi =>
                            (oi.UnitPriceWithDiscount ?? oi.UnitPrice) * oi.Quantity)),
                        OrdersCount = g.Count()
                    })
                    .OrderByDescending(x => x.TotalSpent)
                    .Take(3)
                    .ToListAsync();

                var growthCustomers = new List<object>();
                foreach (var item in topCustomersLast7Days)
                {
                    var customer = await _context.Customers
                        .Where(c => c.CustomerId == item.CustomerId)
                        .Select(c => c.FullName)
                        .FirstOrDefaultAsync();
                    growthCustomers.Add(new
                    {
                        Name = customer ?? "مشتری ناشناس",
                        Amount = item.TotalSpent,
                        OrderCount = item.OrdersCount
                    });
                }

                // ---------- کارت دوم: نرخ بازگشت مشتری (درصد مشتریانی که حداقل ۲ بار، ۳ بار و ۵ بار خرید کرده‌اند) ----------
                var customerPurchaseCounts = await closedOrders
                    .Where(o => o.CustomerId != null)
                    .GroupBy(o => o.CustomerId)
                    .Select(g => new { CustomerId = g.Key, OrderCount = g.Count() })
                    .ToListAsync();

                int totalBuyingCustomers = customerPurchaseCounts.Count;
                int moreThan1 = customerPurchaseCounts.Count(x => x.OrderCount >= 2);
                int moreThan2 = customerPurchaseCounts.Count(x => x.OrderCount >= 3);
                int moreThan4 = customerPurchaseCounts.Count(x => x.OrderCount >= 5);

                double rate2 = totalBuyingCustomers > 0 ? (moreThan1 * 100.0 / totalBuyingCustomers) : 0;
                double rate3 = totalBuyingCustomers > 0 ? (moreThan2 * 100.0 / totalBuyingCustomers) : 0;
                double rate5 = totalBuyingCustomers > 0 ? (moreThan4 * 100.0 / totalBuyingCustomers) : 0;

                // ---------- کارت سوم: بهترین مشتریان (سه مشتری با بیشترین خرید کل) ----------
                var topCustomersOverall = await closedOrders
                    .Where(o => o.CustomerId != null)
                    .GroupBy(o => o.CustomerId)
                    .Select(g => new
                    {
                        CustomerId = g.Key,
                        TotalSpent = g.Sum(o => o.OrderItems.Sum(oi =>
                            (oi.UnitPriceWithDiscount ?? oi.UnitPrice) * oi.Quantity))
                    })
                    .OrderByDescending(x => x.TotalSpent)
                    .Take(3)
                    .ToListAsync();

                var bestCustomers = new List<object>();
                foreach (var item in topCustomersOverall)
                {
                    var customer = await _context.Customers
                        .Where(c => c.CustomerId == item.CustomerId)
                        .Select(c => c.FullName)
                        .FirstOrDefaultAsync();
                    bestCustomers.Add(new
                    {
                        Name = customer ?? "مشتری ناشناس",
                        Amount = item.TotalSpent
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        growthCustomers,      // لیست سه مشتری برتر ۷ روزه
                        returnRates = new { rate2, rate3, rate5 }, // نرخ بازگشت ۲،۳،۵ بار
                        bestCustomers         // سه مشتری برتر کل
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}
