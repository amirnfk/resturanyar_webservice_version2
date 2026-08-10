using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Models.AdminMessage;
using resturanyar.Models.Copoun;
using resturanyar.Models.Inventory;
using resturanyar.Models.Receipt;
using resturanyar.Models.ViewModels.Admin;
using resturanyar.Models.ViewModels.CopounViewModel;
using resturanyar.Utility;
using Resturanyar.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace resturanyar.Controllers
{
    public class AdminController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly MessageService _messageService;

        public AdminController(AppDbContext context, ILogger<HomeController> logger, IConfiguration configuration, MessageService messageService)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _messageService = messageService;
        }

        // ===== متد کمکی برای دریافت لیست OwnerIdهای حذف شده =====
        private List<int> GetExcludedOwnerIds()
        {
            // لیست کامل OwnerIdهایی که باید از همه گزارش‌ها حذف شوند
            return new List<int>
            {
                 135, 137, 139, 140, 142, 143, 144, 145, 146,
                172, 173, 174, 175, 183, 184, 185
            };

            // برای خواندن از Session (در صورت نیاز):
            // var sessionValue = HttpContext.Session.GetString("ExcludedOwnerIds");
            // if (!string.IsNullOrEmpty(sessionValue))
            // {
            //     return sessionValue.Split(',').Select(int.Parse).ToList();
            // }
            // return new List<int>();

            // برای خواندن از appsettings.json (در صورت نیاز):
            // return _configuration.GetSection("ExcludedOwnerIds").Get<List<int>>() ?? new List<int>();
        }

        public async Task<IActionResult> AdminPanel()
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                {
                    return RedirectToAction("AdminLogin");
                }

                var excludedIds = GetExcludedOwnerIds();
                var today = DateTime.Now;
                var viewModel = new AdminDashboardViewModel();

                // ===== آمار کلی (با اعمال فیلتر) =====
                viewModel.TotalRestaurants = await _context.Restaurants.AsNoTracking()
                    .Where(r => !excludedIds.Contains(r.owner_id))
                    .CountAsync();

                viewModel.TotalOwners = await _context.Owners.AsNoTracking()
                    .Where(o => !excludedIds.Contains(o.Id))
                    .CountAsync();

                viewModel.TotalSubscriptions = await _context.Subscriptions.AsNoTracking()
                    .Where(s => !excludedIds.Contains(s.OwnerId))
                    .CountAsync();

                viewModel.ActiveSubscriptions = await _context.Subscriptions.AsNoTracking()
                    .Where(s => s.Status == "Active" && s.EndDate >= today && !excludedIds.Contains(s.OwnerId))
                    .CountAsync();

                viewModel.TotalRevenue = await _context.Subscriptions.AsNoTracking()
                    .Where(s => s.IsPaid && !excludedIds.Contains(s.OwnerId))
                    .SumAsync(s => (decimal?)s.PricePaid) ?? 0m;

                // ===== لیست رستوران‌ها (بدون ساب‌کوئری وابسته به ازای هر ردیف) =====
                var restaurants = await (
                    from r in _context.Restaurants.AsNoTracking()
                    join o in _context.Owners.AsNoTracking() on r.owner_id equals o.Id
                    where !excludedIds.Contains(o.Id)
                    select new RestaurantStatusViewModel
                    {
                        RestaurantId = r.restaurant_id,
                        Name = r.name,
                        OwnerName = o.Name,
                        OwnerPhone = o.Phone
                    }).ToListAsync();

                var restaurantIds = restaurants.Select(r => r.RestaurantId).ToList();

                if (restaurantIds.Count > 0)
                {
                    var paidCounts = await _context.Subscriptions.AsNoTracking()
                        .Where(s => restaurantIds.Contains(s.RestaurantId) && s.IsPaid)
                        .GroupBy(s => s.RestaurantId)
                        .Select(g => new { RestaurantId = g.Key, Count = g.Count() })
                        .ToDictionaryAsync(x => x.RestaurantId, x => x.Count);

                    var hasAnySub = await _context.Subscriptions.AsNoTracking()
                        .Where(s => restaurantIds.Contains(s.RestaurantId))
                        .Select(s => s.RestaurantId)
                        .Distinct()
                        .ToListAsync();
                    var hasAnySubSet = hasAnySub.ToHashSet();

                    var activeEnds = await _context.Subscriptions.AsNoTracking()
                        .Where(s => restaurantIds.Contains(s.RestaurantId)
                                    && s.Status == "Active"
                                    && s.EndDate >= today)
                        .GroupBy(s => s.RestaurantId)
                        .Select(g => new { RestaurantId = g.Key, EndDate = g.Max(x => x.EndDate) })
                        .ToDictionaryAsync(x => x.RestaurantId, x => x.EndDate);

                    var latestSubEnds = await _context.Subscriptions.AsNoTracking()
                        .Where(s => restaurantIds.Contains(s.RestaurantId))
                        .GroupBy(s => s.RestaurantId)
                        .Select(g => new { RestaurantId = g.Key, EndDate = g.Max(x => x.EndDate) })
                        .ToDictionaryAsync(x => x.RestaurantId, x => (DateTime?)x.EndDate);

                    var latestPlanIds = await _context.Subscriptions.AsNoTracking()
                        .Where(s => restaurantIds.Contains(s.RestaurantId))
                        .GroupBy(s => s.RestaurantId)
                        .Select(g => new
                        {
                            RestaurantId = g.Key,
                            PlanId = g.OrderByDescending(x => x.EndDate)
                                      .ThenByDescending(x => x.Id)
                                      .Select(x => x.SubscriptionPlanId)
                                      .FirstOrDefault()
                        })
                        .ToListAsync();

                    var planIds = latestPlanIds.Select(x => x.PlanId).Distinct().ToList();
                    var planNames = planIds.Count == 0
                        ? new Dictionary<int, string>()
                        : await _context.SubscriptionPlans.AsNoTracking()
                            .Where(p => planIds.Contains(p.Id))
                            .ToDictionaryAsync(p => p.Id, p => p.Name);

                    var planByRestaurant = latestPlanIds.ToDictionary(
                        x => x.RestaurantId,
                        x => planNames.GetValueOrDefault(x.PlanId));

                    var orderCounts = await _context.Orders.AsNoTracking()
                        .Where(o => restaurantIds.Contains(o.RestaurantId))
                        .GroupBy(o => o.RestaurantId)
                        .Select(g => new { RestaurantId = g.Key, Count = g.Count() })
                        .ToDictionaryAsync(x => x.RestaurantId, x => x.Count);

                    foreach (var restaurant in restaurants)
                    {
                        restaurant.TotalSubscriptions = paidCounts.GetValueOrDefault(restaurant.RestaurantId);
                        restaurant.TotalOrders = orderCounts.GetValueOrDefault(restaurant.RestaurantId);
                        restaurant.SubscriptionEndDate = latestSubEnds.GetValueOrDefault(restaurant.RestaurantId);
                        restaurant.PlanName = planByRestaurant.GetValueOrDefault(restaurant.RestaurantId);

                        if (activeEnds.ContainsKey(restaurant.RestaurantId))
                            restaurant.SubscriptionStatus = "Active";
                        else if (hasAnySubSet.Contains(restaurant.RestaurantId))
                            restaurant.SubscriptionStatus = "Expired";
                        else
                            restaurant.SubscriptionStatus = "None";
                    }
                }

                viewModel.Restaurants = restaurants;

                // ===== آمار ماهانه در یک کوئری =====
                var monthWindowStart = new DateTime(today.Year, today.Month, 1).AddMonths(-5);
                var monthWindowEnd = new DateTime(today.Year, today.Month, 1).AddMonths(1);

                var monthlyRows = await _context.Subscriptions.AsNoTracking()
                    .Where(s => s.PurchaseDate >= monthWindowStart
                                && s.PurchaseDate < monthWindowEnd
                                && !excludedIds.Contains(s.OwnerId))
                    .Select(s => new { s.PurchaseDate, s.PricePaid, s.IsPaid })
                    .ToListAsync();

                var monthlyStats = new List<MonthlyStatsViewModel>();
                for (int i = 0; i < 6; i++)
                {
                    var month = monthWindowStart.AddMonths(i);
                    var monthStart = new DateTime(month.Year, month.Month, 1);
                    var nextMonth = monthStart.AddMonths(1);

                    var inMonth = monthlyRows.Where(s => s.PurchaseDate >= monthStart && s.PurchaseDate < nextMonth);
                    monthlyStats.Add(new MonthlyStatsViewModel
                    {
                        Label = month.ToString("yyyy/MM"),
                        Revenue = inMonth.Where(s => s.IsPaid).Sum(s => s.PricePaid),
                        NewSubscriptions = inMonth.Count()
                    });
                }

                viewModel.MonthlyStats = monthlyStats;

                // ===== لیست مالک‌ها (تجمیع گروهی به جای ساب‌کوئری در هر ردیف) =====
                var owners = await _context.Owners.AsNoTracking()
                    .Where(o => !excludedIds.Contains(o.Id))
                    .Select(o => new OwnerSummaryViewModel
                    {
                        OwnerId = o.Id,
                        Name = o.Name,
                        Phone = o.Phone
                    })
                    .ToListAsync();

                var ownerIds = owners.Select(o => o.OwnerId).ToList();
                if (ownerIds.Count > 0)
                {
                    var restaurantCounts = await _context.Restaurants.AsNoTracking()
                        .Where(r => ownerIds.Contains(r.owner_id))
                        .GroupBy(r => r.owner_id)
                        .Select(g => new { OwnerId = g.Key, Count = g.Count() })
                        .ToDictionaryAsync(x => x.OwnerId, x => x.Count);

                    var activeSubCounts = await _context.Subscriptions.AsNoTracking()
                        .Where(s => ownerIds.Contains(s.OwnerId) && s.Status == "Active" && s.EndDate >= today)
                        .GroupBy(s => s.OwnerId)
                        .Select(g => new { OwnerId = g.Key, Count = g.Count() })
                        .ToDictionaryAsync(x => x.OwnerId, x => x.Count);

                    var spendStats = await _context.Subscriptions.AsNoTracking()
                        .Where(s => ownerIds.Contains(s.OwnerId) && s.IsPaid)
                        .GroupBy(s => s.OwnerId)
                        .Select(g => new
                        {
                            OwnerId = g.Key,
                            TotalSpent = g.Sum(x => x.PricePaid),
                            LastPurchaseDate = (DateTime?)g.Max(x => x.PurchaseDate)
                        })
                        .ToDictionaryAsync(x => x.OwnerId, x => x);

                    foreach (var owner in owners)
                    {
                        owner.RestaurantCount = restaurantCounts.GetValueOrDefault(owner.OwnerId);
                        owner.ActiveSubscriptionCount = activeSubCounts.GetValueOrDefault(owner.OwnerId);
                        if (spendStats.TryGetValue(owner.OwnerId, out var spend))
                        {
                            owner.TotalSpent = spend.TotalSpent;
                            owner.LastPurchaseDate = spend.LastPurchaseDate;
                        }
                    }
                }

                viewModel.Owners = owners;

                // ===== اشتراک‌های در حال انقضا =====
                var expiringData = await (
                    from s in _context.Subscriptions.AsNoTracking()
                    join r in _context.Restaurants.AsNoTracking() on s.RestaurantId equals r.restaurant_id
                    join o in _context.Owners.AsNoTracking() on s.OwnerId equals o.Id
                    join p in _context.SubscriptionPlans.AsNoTracking() on s.SubscriptionPlanId equals p.Id
                    where s.Status == "Active" && s.EndDate >= today && s.EndDate <= today.AddDays(7)
                          && !excludedIds.Contains(o.Id)
                    select new
                    {
                        s.Id,
                        RestaurantName = r.name,
                        OwnerName = o.Name,
                        PlanName = p.Name,
                        s.EndDate,
                        s.PaymentMethod
                    }).ToListAsync();

                viewModel.ExpiringSubscriptions = expiringData
                    .Select(item => new ExpiringSubscriptionViewModel
                    {
                        SubscriptionId = item.Id,
                        RestaurantName = item.RestaurantName,
                        OwnerName = item.OwnerName,
                        PlanName = item.PlanName,
                        EndDate = item.EndDate,
                        DaysLeft = (int)(item.EndDate - today).TotalDays,
                        PaymentMethod = item.PaymentMethod
                    })
                    .OrderBy(s => s.DaysLeft)
                    .ToList();

                // ===== آخرین اشتراک‌ها — مرتب‌سازی با PK به‌جای PurchaseDate بدون ایندکس =====
                viewModel.RecentSubscriptions = await (
                    from s in _context.Subscriptions.AsNoTracking()
                    join r in _context.Restaurants.AsNoTracking() on s.RestaurantId equals r.restaurant_id
                    join o in _context.Owners.AsNoTracking() on s.OwnerId equals o.Id
                    join p in _context.SubscriptionPlans.AsNoTracking() on s.SubscriptionPlanId equals p.Id
                    where !excludedIds.Contains(o.Id)
                    orderby s.Id descending
                    select new RecentSubscriptionViewModel
                    {
                        SubscriptionId = s.Id,
                        RestaurantName = r.name,
                        OwnerName = o.Name,
                        PlanName = p.Name,
                        PricePaid = s.PricePaid,
                        PurchaseDate = s.PurchaseDate,
                        Status = s.Status,
                        PaymentMethod = s.PaymentMethod
                    })
                    .Take(10)
                    .ToListAsync();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بارگذاری AdminPanel");
                return StatusCode(500, "خطای داخلی سرور: " + ex.Message);
            }
        }

        // ===== متد جداگانه برای دریافت لیست Ownerهای حذف شده =====
        public async Task<IActionResult> GetExcludedOwners()
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                {
                    return Unauthorized();
                }

                var excludedIds = GetExcludedOwnerIds();
                var excludedOwners = await _context.Owners
                    .Where(o => excludedIds.Contains(o.Id))
                    .Select(o => new { o.Id, o.Name, o.Phone })
                    .ToListAsync();

                // دریافت اطلاعات کامل Ownerهایی که حذف شده‌اند
                var allOwners = await _context.Owners
                    .Select(o => new { o.Id, o.Name, o.Phone })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    excludedIds = excludedIds,
                    excludedOwners = excludedOwners,
                    allOwners = allOwners,
                    count = excludedIds.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت لیست Ownerهای حذف شده");
                return Json(new { success = false, message = "خطا در دریافت اطلاعات" });
            }
        }

        // ===== متد برای به‌روزرسانی لیست Ownerهای حذف شده =====
        // در فایل AdminController.cs اضافه کنید

        // ===== دریافت آمار اشتراک‌ها به تفکیک پلن و روش پرداخت =====
        [HttpGet]
        public async Task<IActionResult> GetSubscriptionStats()
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                {
                    return Unauthorized();
                }

                var excludedIds = GetExcludedOwnerIds();
                var today = DateTime.Now;

                // دریافت همه اشتراک‌های فعال با اطلاعات پلن
                var activeSubscriptions = await _context.Subscriptions
                    .Where(s => s.Status == "Active"
                        && s.EndDate >= today
                        && s.IsPaid == true
                        && !excludedIds.Contains(s.OwnerId))
                    .Join(_context.SubscriptionPlans,
                        s => s.SubscriptionPlanId,
                        p => p.Id,
                        (s, p) => new
                        {
                            s.Id,
                            s.PaymentMethod,
                            PlanName = p.Name,
                            s.PricePaid,
                            s.OwnerId,
                            s.RestaurantId
                        })
                    .ToListAsync();

                // تعریف رنگ‌ها برای هر ترکیب
                var colorMap = new Dictionary<string, string>
        {
            { "رایگان-FreeTrial", "#94a3b8" },
            { "رایگان-CafeBazar", "#94a3b8" },
            { "رایگان-Zarinpal", "#94a3b8" },
            { "رایگان-نامشخص", "#94a3b8" },

            { "برنزی-FreeTrial", "#cd7f32" },
            { "برنزی-CafeBazar", "#cd7f32" },
            { "برنزی-Zarinpal", "#cd7f32" },
            { "برنزی-نامشخص", "#cd7f32" },

            { "نقره‌ای-FreeTrial", "#c0c0c0" },
            { "نقره‌ای-CafeBazar", "#c0c0c0" },
            { "نقره‌ای-Zarinpal", "#c0c0c0" },
            { "نقره‌ای-نامشخص", "#c0c0c0" },

            { "طلایی-FreeTrial", "#ffd700" },
            { "طلایی-CafeBazar", "#ffd700" },
            { "طلایی-Zarinpal", "#ffd700" },
            { "طلایی-نامشخص", "#ffd700" }
        };

                // دسته‌بندی بر اساس پلن و روش پرداخت
                var stats = new List<SubscriptionStatsViewModel>();

                var planNames = new[] { "رایگان", "برنزی", "نقره‌ای", "طلایی" };
                var paymentMethods = new[] { "FreeTrial", "CafeBazar", "Zarinpal" };

                foreach (var plan in planNames)
                {
                    foreach (var method in paymentMethods)
                    {
                        var items = activeSubscriptions
                            .Where(s => s.PlanName == plan && s.PaymentMethod == method)
                            .ToList();

                        if (items.Any())
                        {
                            var key = $"{plan}-{method}";
                            stats.Add(new SubscriptionStatsViewModel
                            {
                                PlanName = plan,
                                PaymentMethod = method,
                                Count = items.Count,
                                TotalRevenue = items.Sum(s => s.PricePaid),
                                DisplayName = GetDisplayName(plan, method),
                                Color = colorMap.ContainsKey(key) ? colorMap[key] : "#e2e8f0"
                            });
                        }
                    }

                    // اشتراک‌هایی که PaymentMethod ندارند یا null هستند
                    var nullMethodItems = activeSubscriptions
                        .Where(s => s.PlanName == plan && string.IsNullOrEmpty(s.PaymentMethod))
                        .ToList();

                    if (nullMethodItems.Any())
                    {
                        var key = $"{plan}-نامشخص";
                        stats.Add(new SubscriptionStatsViewModel
                        {
                            PlanName = plan,
                            PaymentMethod = "نامشخص",
                            Count = nullMethodItems.Count,
                            TotalRevenue = nullMethodItems.Sum(s => s.PricePaid),
                            DisplayName = $"{plan} (نامشخص)",
                            Color = colorMap.ContainsKey(key) ? colorMap[key] : "#e2e8f0"
                        });
                    }
                }

                // محاسبه مجموع کل
                var totalActive = activeSubscriptions.Count;
                var totalRevenue = activeSubscriptions.Sum(s => s.PricePaid);

                return Json(new
                {
                    success = true,
                    data = stats,
                    summary = new
                    {
                        totalActiveSubscriptions = totalActive,
                        totalRevenue = totalRevenue
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت آمار اشتراک‌ها");
                return Json(new { success = false, message = "خطا در دریافت آمار" });
            }
        }


        // متد کمکی برای تولید نام نمایشی
        private string GetDisplayName(string planName, string paymentMethod)
        {
            var displayNames = new Dictionary<string, string>
    {
        { "FreeTrial", "فری‌ترایال" },
        { "CafeBazar", "کافه‌بازار" },
        { "Zarinpal", "زرین‌پال" }
    };

            var methodDisplay = displayNames.ContainsKey(paymentMethod)
                ? displayNames[paymentMethod]
                : paymentMethod;

            return $"{planName} - {methodDisplay}";
        }

        // ===== دریافت آمار خلاصه اشتراک‌ها به تفکیک پلن =====
        [HttpGet]
        public async Task<IActionResult> GetSubscriptionSummary()
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                {
                    return Unauthorized();
                }

                var excludedIds = GetExcludedOwnerIds();
                var today = DateTime.Now;

                var query = from s in _context.Subscriptions
                            join p in _context.SubscriptionPlans on s.SubscriptionPlanId equals p.Id
                            where s.Status == "Active"
                                && s.EndDate >= today
                                && s.IsPaid == true
                                && !excludedIds.Contains(s.OwnerId)
                            group s by new { PlanName = p.Name, s.PaymentMethod } into g
                            select new
                            {
                                PlanName = g.Key.PlanName,
                                PaymentMethod = g.Key.PaymentMethod ?? "نامشخص",
                                Count = g.Count(),
                                TotalRevenue = g.Sum(s => s.PricePaid)
                            };

                var result = await query.ToListAsync();

                // مرتب‌سازی بر اساس پلن
                var planOrder = new Dictionary<string, int>
        {
            { "رایگان", 1 },
            { "برنزی", 2 },
            { "نقره‌ای", 3 },
            { "طلایی", 4 }
        };

                var sortedResult = result
                    .OrderBy(x => planOrder.ContainsKey(x.PlanName) ? planOrder[x.PlanName] : 999)
                    .ThenBy(x => x.PaymentMethod)
                    .ToList();

                return Json(new
                {
                    success = true,
                    data = sortedResult
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت خلاصه آمار اشتراک‌ها");
                return Json(new { success = false, message = "خطا در دریافت آمار" });
            }
        }
        [HttpPost]
        public IActionResult UpdateExcludedOwners([FromBody] List<int> ownerIds)
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                {
                    return Unauthorized();
                }

                if (ownerIds == null)
                {
                    return Json(new { success = false, message = "لیست ارسال شده نامعتبر است" });
                }

                // حذف مقادیر تکراری
                var uniqueIds = ownerIds.Distinct().ToList();

                // ذخیره در Session
                HttpContext.Session.SetString("ExcludedOwnerIds", string.Join(",", uniqueIds));

                // همچنین می‌توانید در دیتابیس یا فایل ذخیره کنید
                // System.IO.File.WriteAllText("excluded_owners.txt", string.Join(",", uniqueIds));

                return Json(new
                {
                    success = true,
                    message = "لیست با موفقیت به‌روزرسانی شد",
                    count = uniqueIds.Count,
                    excludedIds = uniqueIds
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی لیست Ownerهای حذف شده");
                return Json(new { success = false, message = "خطا در به‌روزرسانی لیست" });
            }
        }

        // ===== متد برای اضافه کردن Owner به لیست حذف =====
        [HttpPost]
        public IActionResult AddExcludedOwner([FromBody] int ownerId)
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                {
                    return Unauthorized();
                }

                var currentList = GetExcludedOwnerIdsFromSession();
                if (!currentList.Contains(ownerId))
                {
                    currentList.Add(ownerId);
                    HttpContext.Session.SetString("ExcludedOwnerIds", string.Join(",", currentList));
                }

                return Json(new { success = true, message = "Owner با موفقیت اضافه شد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در اضافه کردن Owner به لیست حذف");
                return Json(new { success = false, message = "خطا در انجام عملیات" });
            }
        }

        // ===== متد برای حذف Owner از لیست حذف =====
        [HttpPost]
        public IActionResult RemoveExcludedOwner([FromBody] int ownerId)
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                {
                    return Unauthorized();
                }

                var currentList = GetExcludedOwnerIdsFromSession();
                if (currentList.Contains(ownerId))
                {
                    currentList.Remove(ownerId);
                    HttpContext.Session.SetString("ExcludedOwnerIds", string.Join(",", currentList));
                }

                return Json(new { success = true, message = "Owner با موفقیت حذف شد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف Owner از لیست حذف");
                return Json(new { success = false, message = "خطا در انجام عملیات" });
            }
        }

        // ===== متد کمکی برای دریافت لیست از Session =====
        private List<int> GetExcludedOwnerIdsFromSession()
        {
            var sessionValue = HttpContext.Session.GetString("ExcludedOwnerIds");
            if (!string.IsNullOrEmpty(sessionValue))
            {
                return sessionValue.Split(',').Select(int.Parse).ToList();
            }
            return new List<int>();
        }

        // ================ دیتای نمودار درآمد ماهانه (۶ ماه اخیر) ================
        [HttpGet]
        public async Task<IActionResult> GetMonthlyStats()
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                {
                    return Unauthorized();
                }

                var excludedIds = GetExcludedOwnerIds();
                var today = DateTime.Now;
                var startDate = new DateTime(today.Year, today.Month, 1).AddMonths(-5);

                var paidSubs = await _context.Subscriptions
                    .Where(s => s.IsPaid == true && s.PurchaseDate >= startDate && !excludedIds.Contains(s.OwnerId))
                    .Select(s => new { s.PurchaseDate, s.PricePaid })
                    .ToListAsync();

                var allNewSubs = await _context.Subscriptions
                    .Where(s => s.PurchaseDate >= startDate && !excludedIds.Contains(s.OwnerId))
                    .Select(s => s.PurchaseDate)
                    .ToListAsync();

                var months = Enumerable.Range(0, 6)
                    .Select(i => startDate.AddMonths(i))
                    .ToList();

                var result = months.Select(m => new
                {
                    Label = m.ToString("yyyy/MM"),
                    Revenue = paidSubs
                        .Where(s => s.PurchaseDate.Year == m.Year && s.PurchaseDate.Month == m.Month)
                        .Sum(s => s.PricePaid),
                    NewSubscriptions = allNewSubs
                        .Count(d => d.Year == m.Year && d.Month == m.Month)
                }).ToList();

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت آمار ماهانه");
                return Json(new { success = false, message = "خطا در دریافت آمار" });
            }
        }

        // ===== متدهای مدیریت احراز هویت =====
        [HttpGet]
        public IActionResult AdminLogin()
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") == "true")
            {
                return RedirectToAction("AdminPanel");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AdminLogin(AdminLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var adminUsername = "@noorafkan";
            var adminPassword = "24602460";

            if (model.Username == adminUsername && model.Password == adminPassword)
            {
                HttpContext.Session.SetString("AdminLoggedIn", "true");
                HttpContext.Session.SetString("AdminUsername", model.Username);
                HttpContext.Session.SetString("LoginTime", DateTime.Now.ToString());

                if (model.RememberMe)
                {
                    CookieOptions options = new CookieOptions
                    {
                        Expires = DateTime.Now.AddDays(30),
                        HttpOnly = true,
                        IsEssential = true
                    };
                    Response.Cookies.Append("AdminRemember", model.Username, options);
                }

                return RedirectToAction("AdminPanel");
            }

            ModelState.AddModelError("", "نام کاربری یا رمز عبور اشتباه است");
            return View(model);
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete("AdminRemember");
            return RedirectToAction("AdminLogin");
        }

        // ===== دریافت آمار سفارش‌ها و غذاهای ثبت‌شده در بازه‌های زمانی =====
        // ===== دریافت آمار فعالیت به تفکیک هر رستوران =====
        [HttpGet]
        public async Task<IActionResult> GetRestaurantActivityStatsDetailed()
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                    return Unauthorized();

                var excludedIds = GetExcludedOwnerIds();
                var now = DateTime.Now;
                var oneDayAgo = now.AddDays(-1);
                var sevenDaysAgo = now.AddDays(-7);
                var thirtyDaysAgo = now.AddDays(-30);

                var restaurants = await _context.Restaurants.AsNoTracking()
                    .Where(r => !excludedIds.Contains(r.owner_id))
                    .Select(r => new { RestaurantId = r.restaurant_id, RestaurantName = r.name })
                    .ToListAsync();

                var orderStats = await _context.Orders.AsNoTracking()
                    .Where(o => o.CreatedAt >= thirtyDaysAgo)
                    .GroupBy(o => o.RestaurantId)
                    .Select(g => new
                    {
                        RestaurantId = g.Key,
                        Orders1Day = g.Count(x => x.CreatedAt >= oneDayAgo),
                        Orders7Day = g.Count(x => x.CreatedAt >= sevenDaysAgo),
                        Orders30Day = g.Count()
                    })
                    .ToDictionaryAsync(x => x.RestaurantId, x => x);

                var foodStats = await _context.FoodItems.AsNoTracking()
                    .Where(f => f.CreatedAt != null && f.CreatedAt >= thirtyDaysAgo)
                    .GroupBy(f => f.RestaurantId)
                    .Select(g => new
                    {
                        RestaurantId = g.Key,
                        FoodItems1Day = g.Count(x => x.CreatedAt >= oneDayAgo),
                        FoodItems7Day = g.Count(x => x.CreatedAt >= sevenDaysAgo),
                        FoodItems30Day = g.Count()
                    })
                    .ToDictionaryAsync(x => x.RestaurantId, x => x);

                var result = restaurants.Select(r =>
                {
                    orderStats.TryGetValue(r.RestaurantId, out var orders);
                    foodStats.TryGetValue(r.RestaurantId, out var foods);
                    return new
                    {
                        r.RestaurantId,
                        r.RestaurantName,
                        Orders1Day = orders?.Orders1Day ?? 0,
                        Orders7Day = orders?.Orders7Day ?? 0,
                        Orders30Day = orders?.Orders30Day ?? 0,
                        FoodItems1Day = foods?.FoodItems1Day ?? 0,
                        FoodItems7Day = foods?.FoodItems7Day ?? 0,
                        FoodItems30Day = foods?.FoodItems30Day ?? 0
                    };
                }).ToList();

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت آمار جزئی رستوران‌ها");
                return Json(new { success = false, message = "خطا در دریافت داده‌ها" });
            }
        }


        // در AdminController.cs اضافه کنید

        // ===== نمایش فرم ایجاد کد تخفیف =====
        // ===== نمایش فرم ایجاد کد تخفیف =====
        [HttpGet]
        public async Task<IActionResult> CreateCoupon()
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                {
                    return RedirectToAction("AdminLogin");
                }

                var model = new CouponViewModel
                {
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddMonths(1),
                    CouponScope = "General",
                    IsActive = true,
                    LimitPerOwner = 1
                };

                // بارگذاری لیست‌ها
                await LoadCouponSelectLists(model);

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بارگذاری فرم ایجاد کد تخفیف");
                TempData["ErrorMessage"] = $"خطا در بارگذاری فرم: {ex.Message}";
                return RedirectToAction("AdminPanel");
            }
        }

        // ===== ذخیره کد تخفیف جدید =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCoupon(CouponViewModel model)
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                {
                    return RedirectToAction("AdminLogin");
                }

                // اعتبارسنجی دستی
                if (model.StartDate >= model.EndDate)
                {
                    ModelState.AddModelError("", "تاریخ شروع باید قبل از تاریخ پایان باشد");
                }

                if (model.DiscountType == "Percentage" && model.DiscountValue > 100)
                {
                    ModelState.AddModelError("DiscountValue", "مقدار تخفیف درصدی نمی‌تواند بیشتر از 100 باشد");
                }

                if (model.DiscountType == "Percentage" && model.DiscountValue <= 0)
                {
                    ModelState.AddModelError("DiscountValue", "مقدار تخفیف درصدی باید بین 1 تا 100 باشد");
                }

                if (model.DiscountType == "FixedAmount" && model.DiscountValue <= 0)
                {
                    ModelState.AddModelError("DiscountValue", "مقدار تخفیف باید بیشتر از صفر باشد");
                }

                // بررسی تکراری نبودن کد
                var existingCoupon = await _context.Coupons
                    .FirstOrDefaultAsync(c => c.Code == model.Code && c.EndDate > DateTime.Now);

                if (existingCoupon != null)
                {
                    ModelState.AddModelError("Code", "این کد تخفیف قبلاً ثبت شده است");
                }

                // اگر مدل معتبر نبود
                if (!ModelState.IsValid)
                {
                    await LoadCouponSelectLists(model);
                    return View(model);
                }

                // ایجاد کد تخفیف جدید
                var coupon = new Coupon
                {
                    Code = model.Code.Trim().ToUpper(), // ذخیره کد به صورت بزرگ
                    DiscountType = model.DiscountType,
                    DiscountValue = model.DiscountValue,
                    MaxDiscountAmount = model.DiscountType == "Percentage" ? model.MaxDiscountAmount : null,
                    MinPurchaseAmount = model.MinPurchaseAmount,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    IsActive = model.IsActive,
                    UsageLimit = model.UsageLimit,
                    UsedCount = 0,
                    LimitPerOwner = model.LimitPerOwner,
                    SpecificOwnerId = model.CouponScope == "Owner" ? model.SpecificOwnerId : null,
                    SpecificRestaurantId = model.CouponScope == "Restaurant" ? model.SpecificRestaurantId : null,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Coupons.Add(coupon);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"کد تخفیف {coupon.Code} با موفقیت ایجاد شد";
                return RedirectToAction("CouponList");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "خطای دیتابیس در ایجاد کد تخفیف");
                ModelState.AddModelError("", "خطا در ذخیره‌سازی در دیتابیس: " + dbEx.InnerException?.Message ?? dbEx.Message);
                await LoadCouponSelectLists(model);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ایجاد کد تخفیف");
                ModelState.AddModelError("", "خطا در ایجاد کد تخفیف: " + ex.Message);
                await LoadCouponSelectLists(model);
                return View(model);
            }
        }

        // ===== متد کمکی برای بارگذاری لیست‌های انتخاب =====
        private async Task LoadCouponSelectLists(CouponViewModel model)
        {
            try
            {
               

                // لیست انواع تخفیف
                model.DiscountTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "Percentage", Text = "درصدی" },
            new SelectListItem { Value = "FixedAmount", Text = "مبلغ ثابت" }
        };

                // لیست مالکان
                var owners = await _context.Owners
                   
                    .OrderBy(o => o.Name)
                    .Select(o => new SelectListItem
                    {
                        Value = o.Id.ToString(),
                        Text = $"{o.Name} - {o.Phone}"
                    })
                    .ToListAsync();

                model.Owners = new List<SelectListItem>();
                model.Owners.Add(new SelectListItem { Value = "", Text = "همه مالکان" });
                model.Owners.AddRange(owners);

                // لیست رستوران‌ها
                var restaurants = await _context.Restaurants
                   
                    .OrderBy(r => r.name)
                    .Select(r => new SelectListItem
                    {
                        Value = r.restaurant_id.ToString(),
                        Text = r.name
                    })
                    .ToListAsync();

                model.Restaurants = new List<SelectListItem>();
                model.Restaurants.Add(new SelectListItem { Value = "", Text = "همه رستوران‌ها" });
                model.Restaurants.AddRange(restaurants);

                // تنظیم مقادیر انتخاب شده (برای ویرایش)
                if (!string.IsNullOrEmpty(model.CouponScope))
                {
                    if (model.CouponScope == "Owner" && model.SpecificOwnerId.HasValue)
                    {
                        // مقدار انتخاب شده در لیست مالکان تنظیم می‌شود
                    }
                    else if (model.CouponScope == "Restaurant" && model.SpecificRestaurantId.HasValue)
                    {
                        // مقدار انتخاب شده در لیست رستوران‌ها تنظیم می‌شود
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بارگذاری لیست‌های انتخاب");
                throw;
            }
        }

        // ===== متد کمکی برای بارگذاری لیست‌های انتخاب =====
        

        // ===== نمایش لیست کدهای تخفیف =====
        [HttpGet]
        public async Task<IActionResult> CouponList()
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                {
                    return RedirectToAction("AdminLogin");
                }

             

                // ✅ روش صحیح: دریافت کوپن‌ها و سپس دریافت نام‌ها به صورت جداگانه
                var coupons = await _context.Coupons
                    
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                // دریافت لیست OwnerId و RestaurantId برای یکبار Query
                var ownerIds = coupons.Where(c => c.SpecificOwnerId.HasValue).Select(c => c.SpecificOwnerId.Value).Distinct().ToList();
                var restaurantIds = coupons.Where(c => c.SpecificRestaurantId.HasValue).Select(c => c.SpecificRestaurantId.Value).Distinct().ToList();

                // دریافت همه Ownerها و Restaurantها با یک کوئری
                var owners = await _context.Owners
                    .Where(o => ownerIds.Contains(o.Id))
                    .ToDictionaryAsync(o => o.Id, o => o.Name);

                var restaurants = await _context.Restaurants
                    .Where(r => restaurantIds.Contains(r.restaurant_id))
                    .ToDictionaryAsync(r => r.restaurant_id, r => r.name);

                // ساخت ViewModel
                var couponViewModels = coupons.Select(c => new CouponListViewModel
                {
                    Id = c.Id,
                    Code = c.Code,
                    DiscountType = c.DiscountType,
                    DiscountValue = c.DiscountValue,
                    MaxDiscountAmount = c.MaxDiscountAmount,
                    MinPurchaseAmount = c.MinPurchaseAmount,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    IsActive = c.IsActive,
                    UsedCount = c.UsedCount,
                    UsageLimit = c.UsageLimit,
                    SpecificOwnerName = c.SpecificOwnerId.HasValue && owners.ContainsKey(c.SpecificOwnerId.Value)
                        ? owners[c.SpecificOwnerId.Value]
                        : "همه مالکان",
                    SpecificRestaurantName = c.SpecificRestaurantId.HasValue && restaurants.ContainsKey(c.SpecificRestaurantId.Value)
                        ? restaurants[c.SpecificRestaurantId.Value]
                        : "همه رستوران‌ها",
                    IsExpired = c.EndDate < DateTime.Now,
                    DaysRemaining = c.EndDate > DateTime.Now ? (int?)(c.EndDate - DateTime.Now).TotalDays : null,
                    UsagePercentage = c.UsageLimit.HasValue && c.UsageLimit.Value > 0
                        ? (double)c.UsedCount / c.UsageLimit.Value * 100
                        : null
                }).ToList();

                return View(couponViewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت لیست کدهای تخفیف");
                TempData["ErrorMessage"] = $"خطا در دریافت لیست کدهای تخفیف: {ex.Message}";
                return View(new List<CouponListViewModel>());
            }
        }
        [HttpPost]
        public async Task<IActionResult> DeleteCoupon([FromBody] int id) // ← افزودن [FromBody]
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                    return Unauthorized();

                // ۱. بررسی وجود کوپن
                var coupon = await _context.Coupons.FindAsync(id);
                if (coupon == null)
                    return Json(new { success = false, message = $"کد تخفیف با ID {id} یافت نشد" });

                // ۲. بررسی وابستگی‌ها (وجود در CouponUsages)
                bool hasUsages = await _context.CouponUsages.AnyAsync(u => u.CouponId == id);
                if (hasUsages)
                {
                    return Json(new
                    {
                        success = false,
                        message = "این کد تخفیف قبلاً در سفارش‌ها استفاده شده و قابل حذف نیست. ابتدا استفاده‌های آن را حذف کنید."
                    });
                }

                // ۳. حذف کوپن
                _context.Coupons.Remove(coupon);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "کد تخفیف با موفقیت حذف شد" });
            }
            catch (DbUpdateException dbEx)
            {
                var inner = dbEx.InnerException?.Message ?? dbEx.Message;
                _logger.LogError(dbEx, "خطای دیتابیس در حذف کد تخفیف");

                if (inner.Contains("REFERENCE") || inner.Contains("FK_CouponUsages_Coupons"))
                    return Json(new { success = false, message = "این کد تخفیف در جدول استفاده‌ها وجود دارد و قابل حذف نیست." });

                return Json(new { success = false, message = $"خطای دیتابیس: {inner}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف کد تخفیف");
                return Json(new { success = false, message = "خطا در حذف کد تخفیف: " + ex.Message });
            }
        }

        // ===== ویرایش کد تخفیف =====
        [HttpGet]
        public async Task<IActionResult> EditCoupon(int id)
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                {
                    return RedirectToAction("AdminLogin");
                }

                var coupon = await _context.Coupons.FindAsync(id);
                if (coupon == null)
                {
                    return NotFound();
                }

                var model = new CouponViewModel
                {
                    Id = coupon.Id,
                    Code = coupon.Code,
                    DiscountType = coupon.DiscountType,
                    DiscountValue = coupon.DiscountValue,
                    MaxDiscountAmount = coupon.MaxDiscountAmount,
                    MinPurchaseAmount = coupon.MinPurchaseAmount,
                    StartDate = coupon.StartDate,
                    EndDate = coupon.EndDate,
                    IsActive = coupon.IsActive,
                    UsageLimit = coupon.UsageLimit,
                    LimitPerOwner = coupon.LimitPerOwner,
                    SpecificOwnerId = coupon.SpecificOwnerId,
                    SpecificRestaurantId = coupon.SpecificRestaurantId,
                    CouponScope = coupon.SpecificOwnerId.HasValue ? "Owner" :
                                 coupon.SpecificRestaurantId.HasValue ? "Restaurant" : "General"
                };

                await LoadCouponSelectLists(model);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بارگذاری فرم ویرایش کد تخفیف");
                return StatusCode(500, "خطای داخلی سرور");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCoupon(CouponViewModel model)
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                {
                    return RedirectToAction("AdminLogin");
                }

                // اعتبارسنجی‌ها
                if (model.StartDate >= model.EndDate)
                {
                    ModelState.AddModelError("", "تاریخ شروع باید قبل از تاریخ پایان باشد");
                }

                if (model.DiscountType == "Percentage" && model.DiscountValue > 100)
                {
                    ModelState.AddModelError("DiscountValue", "مقدار تخفیف درصدی نمی‌تواند بیشتر از 100 باشد");
                }

                if (!ModelState.IsValid)
                {
                    await LoadCouponSelectLists(model);
                    return View(model);
                }

                var coupon = await _context.Coupons.FindAsync(model.Id);
                if (coupon == null)
                {
                    return NotFound();
                }

                // به‌روزرسانی اطلاعات
                coupon.Code = model.Code;
                coupon.DiscountType = model.DiscountType;
                coupon.DiscountValue = model.DiscountValue;
                coupon.MaxDiscountAmount = model.DiscountType == "Percentage" ? model.MaxDiscountAmount : null;
                coupon.MinPurchaseAmount = model.MinPurchaseAmount;
                coupon.StartDate = model.StartDate;
                coupon.EndDate = model.EndDate;
                coupon.IsActive = model.IsActive;
                coupon.UsageLimit = model.UsageLimit;
                coupon.LimitPerOwner = model.LimitPerOwner;
                coupon.SpecificOwnerId = model.CouponScope == "Owner" ? model.SpecificOwnerId : null;
                coupon.SpecificRestaurantId = model.CouponScope == "Restaurant" ? model.SpecificRestaurantId : null;
                coupon.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "کد تخفیف با موفقیت به‌روزرسانی شد";
                return RedirectToAction("CouponList");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ویرایش کد تخفیف");
                ModelState.AddModelError("", "خطا در ویرایش کد تخفیف: " + ex.Message);
                await LoadCouponSelectLists(model);
                return View(model);
            }
        }

        private async Task<List<RestaurantPickerItem>> LoadRestaurantPickerAsync()
        {
            return await _context.Restaurants
                .Join(_context.Owners,
                    r => r.owner_id,
                    o => o.Id,
                    (r, o) => new RestaurantPickerItem
                    {
                        RestaurantId = r.restaurant_id,
                        RestaurantName = r.name,
                        OwnerName = o.Name,
                        OwnerPhone = o.Phone
                    })
                .OrderBy(x => x.RestaurantName)
                .ToListAsync();
        }

        [HttpGet]
        public async Task<IActionResult> SendMessage()
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                return RedirectToAction("AdminLogin");

            var model = new SendRestaurantMessageViewModel
            {
                Restaurants = await LoadRestaurantPickerAsync()
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(SendRestaurantMessageViewModel model)
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                return RedirectToAction("AdminLogin");

            model.Restaurants = await LoadRestaurantPickerAsync();

            if (model.MessageType == AdminMessageType.Private &&
                (model.SelectedRestaurantIds == null || model.SelectedRestaurantIds.Length == 0))
            {
                ModelState.AddModelError(nameof(model.SelectedRestaurantIds), "برای پیام خصوصی حداقل یک رستوران انتخاب کنید.");
            }

            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var adminName = HttpContext.Session.GetString("AdminUsername");
                await _messageService.CreateMessageAsync(
                    model.Title,
                    model.Body,
                    model.MessageType,
                    model.SelectedRestaurantIds ?? Array.Empty<int>(),
                    adminName);

                TempData["SuccessMessage"] = "پیام با موفقیت ارسال شد.";
                return RedirectToAction(nameof(MessageList));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ارسال پیام");
                ModelState.AddModelError("", "خطا در ارسال پیام: " + ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> MessageList()
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                return RedirectToAction("AdminLogin");

            var messages = await _context.AdminMessages
                .AsNoTracking()
                .Include(m => m.Recipients)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            var totalRestaurants = await _context.Restaurants.CountAsync();

            var viewModels = messages.Select(m => new AdminMessageListViewModel
            {
                Id = m.Id,
                Title = m.Title,
                MessageType = m.MessageType,
                CreatedAt = m.CreatedAt,
                CreatedByAdmin = m.CreatedByAdmin,
                IsActive = m.IsActive,
                RecipientCount = m.MessageType == AdminMessageType.Public
                    ? totalRestaurants
                    : m.Recipients.Count
            }).ToList();

            return View(viewModels);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateMessage(int id)
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                return RedirectToAction("AdminLogin");

            await _messageService.DeactivateMessageAsync(id);
            TempData["SuccessMessage"] = "پیام غیرفعال شد.";
            return RedirectToAction(nameof(MessageList));
        }

        // ===== مدیریت مقالات =====
        [HttpGet]
        public async Task<IActionResult> ArticleList()
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                    return RedirectToAction("AdminLogin");

                var articles = await _context.Articles
                    .OrderByDescending(a => a.PublishedAt)
                    .ThenByDescending(a => a.Id)
                    .Select(a => new ArticleListItemViewModel
                    {
                        Id = a.Id,
                        Title = a.Title,
                        Slug = a.Slug,
                        PublishedAt = a.PublishedAt,
                        IsPublished = a.IsPublished,
                        Author = a.Author
                    })
                    .ToListAsync();

                return View(articles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت لیست مقالات");
                TempData["ErrorMessage"] = $"خطا در دریافت لیست مقالات: {ex.Message}";
                return View(new List<ArticleListItemViewModel>());
            }
        }

        [HttpGet]
        public IActionResult CreateArticle()
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                return RedirectToAction("AdminLogin");

            return View(new ArticleAdminViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateArticle(ArticleAdminViewModel model)
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                    return RedirectToAction("AdminLogin");

                await PrepareArticleModelAsync(model, isEdit: false);

                if (!ModelState.IsValid)
                    return View(model);

                var featuredImageUrl = await SaveFeaturedImageAsync(model);
                var slug = await SlugHelper.EnsureUniqueSlugAsync(_context, model.Slug.Trim());

                var article = new Article
                {
                    Title = model.Title.Trim(),
                    Slug = slug,
                    MetaDescription = model.MetaDescription.Trim(),
                    Keywords = string.IsNullOrWhiteSpace(model.Keywords) ? null : model.Keywords.Trim(),
                    Content = model.Content,
                    PublishedAt = model.PublishedAt,
                    IsPublished = model.IsPublished,
                    Author = model.Author.Trim(),
                    FeaturedImageUrl = featuredImageUrl ?? model.FeaturedImageUrl?.Trim(),
                    UpdatedAt = DateTime.Now
                };

                _context.Articles.Add(article);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"مقاله «{article.Title}» با موفقیت ایجاد شد.";
                return RedirectToAction(nameof(ArticleList));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ایجاد مقاله");
                ModelState.AddModelError("", "خطا در ایجاد مقاله: " + ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditArticle(int id)
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                    return RedirectToAction("AdminLogin");

                var article = await _context.Articles.FindAsync(id);
                if (article == null)
                    return NotFound();

                var model = new ArticleAdminViewModel
                {
                    Id = article.Id,
                    Title = article.Title,
                    Slug = article.Slug,
                    MetaDescription = article.MetaDescription,
                    Keywords = article.Keywords,
                    Content = article.Content,
                    PublishedAt = article.PublishedAt,
                    IsPublished = article.IsPublished,
                    Author = article.Author,
                    FeaturedImageUrl = article.FeaturedImageUrl
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بارگذاری فرم ویرایش مقاله");
                return StatusCode(500, "خطای داخلی سرور");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditArticle(ArticleAdminViewModel model)
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                    return RedirectToAction("AdminLogin");

                if (!model.Id.HasValue)
                    return NotFound();

                await PrepareArticleModelAsync(model, isEdit: true);

                if (!ModelState.IsValid)
                    return View(model);

                var article = await _context.Articles.FindAsync(model.Id.Value);
                if (article == null)
                    return NotFound();

                var featuredImageUrl = await SaveFeaturedImageAsync(model);
                var slug = await SlugHelper.EnsureUniqueSlugAsync(_context, model.Slug.Trim(), model.Id);

                article.Title = model.Title.Trim();
                article.Slug = slug;
                article.MetaDescription = model.MetaDescription.Trim();
                article.Keywords = string.IsNullOrWhiteSpace(model.Keywords) ? null : model.Keywords.Trim();
                article.Content = model.Content;
                article.PublishedAt = model.PublishedAt;
                article.IsPublished = model.IsPublished;
                article.Author = model.Author.Trim();
                article.FeaturedImageUrl = featuredImageUrl ?? model.FeaturedImageUrl?.Trim();
                article.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "مقاله با موفقیت به‌روزرسانی شد.";
                return RedirectToAction(nameof(ArticleList));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ویرایش مقاله");
                ModelState.AddModelError("", "خطا در ویرایش مقاله: " + ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteArticle([FromBody] int id)
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                    return Unauthorized();

                var article = await _context.Articles.FindAsync(id);
                if (article == null)
                    return Json(new { success = false, message = $"مقاله با ID {id} یافت نشد" });

                _context.Articles.Remove(article);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "مقاله با موفقیت حذف شد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف مقاله");
                return Json(new { success = false, message = "خطا در حذف مقاله: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleArticlePublish([FromBody] int id)
        {
            try
            {
                if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                    return Unauthorized();

                var article = await _context.Articles.FindAsync(id);
                if (article == null)
                    return Json(new { success = false, message = $"مقاله با ID {id} یافت نشد" });

                article.IsPublished = !article.IsPublished;
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    isPublished = article.IsPublished,
                    message = article.IsPublished ? "مقاله منتشر شد." : "مقاله از حالت انتشار خارج شد."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در تغییر وضعیت انتشار مقاله");
                return Json(new { success = false, message = "خطا در تغییر وضعیت: " + ex.Message });
            }
        }

        private async Task PrepareArticleModelAsync(ArticleAdminViewModel model, bool isEdit)
        {
            if (string.IsNullOrWhiteSpace(model.Slug) && !string.IsNullOrWhiteSpace(model.Title))
                model.Slug = SlugHelper.GenerateSlug(model.Title);

            if (string.IsNullOrWhiteSpace(model.Slug))
                ModelState.AddModelError(nameof(model.Slug), "اسلاگ الزامی است. برای عنوان فارسی، اسلاگ انگلیسی را دستی وارد کنید.");

            var slugExists = await _context.Articles.AnyAsync(a =>
                a.Slug == model.Slug.Trim() && (!isEdit || !model.Id.HasValue || a.Id != model.Id.Value));

            if (slugExists)
                ModelState.AddModelError(nameof(model.Slug), "این اسلاگ قبلاً استفاده شده است");

            if (model.FeaturedImage != null && model.FeaturedImage.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(model.FeaturedImage.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                    ModelState.AddModelError(nameof(model.FeaturedImage), "فرمت تصویر مجاز نیست. فرمت‌های مجاز: jpg, png, gif, webp");

                if (model.FeaturedImage.Length > 5 * 1024 * 1024)
                    ModelState.AddModelError(nameof(model.FeaturedImage), "حجم تصویر نباید بیشتر از 5 مگابایت باشد");
            }
        }

        private async Task<string?> SaveFeaturedImageAsync(ArticleAdminViewModel model)
        {
            if (model.FeaturedImage == null || model.FeaturedImage.Length == 0)
                return null;

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "articles");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(model.FeaturedImage.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.FeaturedImage.CopyToAsync(stream);
            }

            return $"/uploads/articles/{uniqueFileName}";
        }

        public async Task<IActionResult> ReceiptChargeFeatures()
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                return RedirectToAction("AdminLogin");

            var excludedIds = GetExcludedOwnerIds();

            var restaurants = await (
                from r in _context.Restaurants
                join o in _context.Owners on r.owner_id equals o.Id
                where !excludedIds.Contains(o.Id)
                orderby r.name
                select new ReceiptChargeFeatureItemViewModel
                {
                    RestaurantId = r.restaurant_id,
                    RestaurantName = r.name,
                    OwnerName = o.Name,
                    OwnerPhone = o.Phone,
                    ReceiptChargesEnabled = r.ReceiptChargesEnabled
                }).ToListAsync();

            if (restaurants.Count > 0)
            {
                var restaurantIds = restaurants.Select(r => r.RestaurantId).ToList();

                var definitionCounts = await _context.RestaurantChargeDefinitions
                    .Where(d => restaurantIds.Contains(d.RestaurantId))
                    .GroupBy(d => d.RestaurantId)
                    .Select(g => new { RestaurantId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.RestaurantId, x => x.Count);

                var snapshotCounts = await _context.OrderReceiptSnapshots
                    .Where(s => restaurantIds.Contains(s.RestaurantId))
                    .GroupBy(s => s.RestaurantId)
                    .Select(g => new { RestaurantId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.RestaurantId, x => x.Count);

                foreach (var restaurant in restaurants)
                {
                    restaurant.ChargeDefinitionCount = definitionCounts.GetValueOrDefault(restaurant.RestaurantId);
                    restaurant.IssuedReceiptCount = snapshotCounts.GetValueOrDefault(restaurant.RestaurantId);
                }
            }

            var viewModel = new ReceiptChargeFeaturesViewModel
            {
                Restaurants = restaurants,
                TotalCount = restaurants.Count,
                EnabledCount = restaurants.Count(r => r.ReceiptChargesEnabled)
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> SetReceiptChargesEnabled(int restaurantId, bool enabled)
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                return Json(new { success = false, message = "دسترسی غیرمجاز" });

            var restaurant = await _context.Restaurants
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId);

            if (restaurant == null)
                return Json(new { success = false, message = "رستوران یافت نشد." });

            restaurant.ReceiptChargesEnabled = enabled;
            restaurant.ReceiptChargesEnabledAt = enabled ? DateTime.Now : null;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = enabled
                    ? "قابلیت فاکتور با کارمزد برای این رستوران فعال شد."
                    : "قابلیت فاکتور با کارمزد برای این رستوران غیرفعال شد."
            });
        }

        public async Task<IActionResult> SupportChat([FromServices] resturanyar.Services.SupportChat.ISupportChatService chatService)
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                return RedirectToAction("AdminLogin");

            ViewData["Title"] = "چت پشتیبانی";
            ViewBag.InitialUnread = await chatService.GetTotalUnreadBySupportAsync();
            ViewBag.Settings = await chatService.GetSettingsAsync();
            return View();
        }

        // ===== مانیتورینگ / فعالیت‌های اخیر =====
        [HttpGet]
        public async Task<IActionResult> RecentActivity()
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                return RedirectToAction("AdminLogin");

            try
            {
                var excludedIds = GetExcludedOwnerIds();
                const int perSection = 3;

                var orderRows = await (
                    from o in _context.Orders.AsNoTracking()
                    join r in _context.Restaurants.AsNoTracking() on o.RestaurantId equals r.restaurant_id
                    where !excludedIds.Contains(r.owner_id)
                    orderby o.OrderId descending
                    select new
                    {
                        o.OrderId,
                        o.RestaurantId,
                        RestaurantName = r.name,
                        o.TableNumber,
                        o.OrderType,
                        o.CreatedAt
                    })
                    .Take(perSection)
                    .ToListAsync();

                var subscriptionRows = await (
                    from s in _context.Subscriptions.AsNoTracking()
                    join r in _context.Restaurants.AsNoTracking() on s.RestaurantId equals r.restaurant_id
                    join p in _context.SubscriptionPlans.AsNoTracking() on s.SubscriptionPlanId equals p.Id
                    where s.IsPaid && !excludedIds.Contains(s.OwnerId)
                    orderby s.Id descending
                    select new
                    {
                        s.Id,
                        s.RestaurantId,
                        RestaurantName = r.name,
                        PlanName = p.Name,
                        s.PricePaid,
                        s.PaymentMethod,
                        s.PurchaseDate
                    })
                    .Take(perSection)
                    .ToListAsync();

                var restaurantRows = await (
                    from r in _context.Restaurants.AsNoTracking()
                    join o in _context.Owners.AsNoTracking() on r.owner_id equals o.Id
                    where !excludedIds.Contains(r.owner_id)
                    orderby r.restaurant_id descending
                    select new
                    {
                        RestaurantId = r.restaurant_id,
                        Name = r.name,
                        OwnerName = o.Name,
                        r.CreatedAt
                    })
                    .Take(perSection)
                    .ToListAsync();

                var supportRows = await (
                    from c in _context.SupportConversations.AsNoTracking()
                    where c.OwnerId == null || !excludedIds.Contains(c.OwnerId.Value)
                    orderby c.LastMessageAtUtc descending
                    select new
                    {
                        c.Id,
                        c.RestaurantId,
                        DisplayName = c.RestaurantName ?? c.OwnerName ?? "مهمان",
                        c.UnreadBySupport,
                        c.LastMessageAtUtc
                    })
                    .Take(perSection)
                    .ToListAsync();

                var foodRows = await (
                    from f in _context.FoodItems.AsNoTracking()
                    join r in _context.Restaurants.AsNoTracking() on f.RestaurantId equals r.restaurant_id
                    where !excludedIds.Contains(r.owner_id)
                    orderby f.FoodItemId descending
                    select new
                    {
                        f.FoodItemId,
                        f.RestaurantId,
                        RestaurantName = r.name,
                        FoodName = f.Name,
                        CategoryName = f.CategoryName,
                        f.Price,
                        f.CreatedAt
                    })
                    .Take(perSection)
                    .ToListAsync();

                var customerRows = await (
                    from c in _context.Customers.AsNoTracking()
                    join r in _context.Restaurants.AsNoTracking() on c.RestaurantId equals r.restaurant_id
                    where !excludedIds.Contains(r.owner_id)
                    orderby c.CustomerId descending
                    select new
                    {
                        c.CustomerId,
                        c.RestaurantId,
                        RestaurantName = r.name,
                        c.FullName,
                        c.Mobile,
                        c.CreatedAt
                    })
                    .Take(perSection)
                    .ToListAsync();

                var ownerBaseRows = await _context.Owners.AsNoTracking()
                    .Where(o => !excludedIds.Contains(o.Id))
                    .OrderByDescending(o => o.Id)
                    .Select(o => new { o.Id, o.Name, o.Phone })
                    .Take(perSection)
                    .ToListAsync();

                var ownerIds = ownerBaseRows.Select(o => o.Id).ToList();
                var ownerRestaurantCounts = ownerIds.Count == 0
                    ? new Dictionary<int, int>()
                    : await _context.Restaurants.AsNoTracking()
                        .Where(r => ownerIds.Contains(r.owner_id))
                        .GroupBy(r => r.owner_id)
                        .Select(g => new { OwnerId = g.Key, Count = g.Count() })
                        .ToDictionaryAsync(x => x.OwnerId, x => x.Count);

                var couponUsageRows = await (
                    from u in _context.CouponUsages.AsNoTracking()
                    join c in _context.Coupons.AsNoTracking() on u.CouponId equals c.Id
                    join r in _context.Restaurants.AsNoTracking() on u.RestaurantId equals r.restaurant_id
                    where !excludedIds.Contains(u.OwnerId)
                    orderby u.Id descending
                    select new
                    {
                        u.Id,
                        CouponCode = c.Code,
                        u.RestaurantId,
                        RestaurantName = r.name,
                        u.DiscountAmount,
                        u.UsedAt,
                        u.Status
                    })
                    .Take(perSection)
                    .ToListAsync();

                var inventoryCandidates = await (
                    from m in _context.InventoryMovements.AsNoTracking()
                    join r in _context.Restaurants.AsNoTracking() on m.RestaurantId equals r.restaurant_id
                    join i in _context.InventoryItems.AsNoTracking() on m.InventoryItemId equals i.InventoryItemId
                    where !excludedIds.Contains(r.owner_id)
                    orderby m.MovementId descending
                    select new
                    {
                        m.MovementId,
                        m.RestaurantId,
                        RestaurantName = r.name,
                        ItemName = i.Name,
                        m.Reason,
                        m.DeltaQuantity,
                        m.QuantityAfter,
                        m.CreatedAt
                    })
                    .Take(80)
                    .ToListAsync();

                var inventoryRows = inventoryCandidates
                    .Where(m => !string.Equals(m.Reason, InventoryMovementReasons.SaleConsumption, StringComparison.OrdinalIgnoreCase))
                    .Take(perSection)
                    .ToList();

                var ownerLoginRows = await (
                    from t in _context.RefreshTokens.AsNoTracking()
                    join o in _context.Owners.AsNoTracking() on t.OwnerId equals o.Id
                    where !excludedIds.Contains(o.Id)
                    orderby t.Id descending
                    select new
                    {
                        t.Id,
                        OwnerId = o.Id,
                        OwnerName = o.Name,
                        OwnerPhone = o.Phone,
                        t.ExpiryTime
                    })
                    .Take(perSection)
                    .ToListAsync();

                var staffLoginRows = await (
                    from t in _context.StaffRefreshTokens.AsNoTracking()
                    join u in _context.Users.AsNoTracking() on t.UserId equals u.user_id
                    join r in _context.Restaurants.AsNoTracking() on t.RestaurantId equals r.restaurant_id
                    join role in _context.Roles.AsNoTracking() on u.role_id equals role.role_id into roles
                    from role in roles.DefaultIfEmpty()
                    where !excludedIds.Contains(r.owner_id)
                    orderby t.Id descending
                    select new
                    {
                        t.Id,
                        UserId = u.user_id,
                        StaffName = u.name,
                        RoleName = role != null ? role.role_name : null,
                        t.RestaurantId,
                        RestaurantName = r.name,
                        t.CreatedAtUtc
                    })
                    .Take(perSection)
                    .ToListAsync();

                var refreshLifetime = GetRefreshTokenLifetime();

                var viewModel = new RecentActivityPageViewModel
                {
                    Orders = orderRows.Select(row => new ActivityFeedItem
                    {
                        Type = ActivityFeedTypes.OrderCreated,
                        EntityId = row.OrderId,
                        RestaurantId = row.RestaurantId,
                        Title = row.RestaurantName,
                        Subtitle = $"سفارش #{row.OrderId} · {GetOrderTypeLabel(row.OrderType)} · میز {row.TableNumber}",
                        OccurredAt = row.CreatedAt,
                        BadgeLabel = "سفارش",
                        BadgeClass = "badge-order",
                        IconClass = "fas fa-receipt"
                    }).ToList(),
                    Subscriptions = subscriptionRows.Select(row => new ActivityFeedItem
                    {
                        Type = ActivityFeedTypes.SubscriptionPurchased,
                        EntityId = row.Id,
                        RestaurantId = row.RestaurantId,
                        Title = row.RestaurantName,
                        Subtitle = $"{row.PlanName} · {row.PricePaid:N0} تومان · {row.PaymentMethod}",
                        OccurredAt = row.PurchaseDate,
                        BadgeLabel = "اشتراک",
                        BadgeClass = "badge-sub",
                        IconClass = "fas fa-crown"
                    }).ToList(),
                    Restaurants = restaurantRows.Select(row => new ActivityFeedItem
                    {
                        Type = ActivityFeedTypes.RestaurantCreated,
                        EntityId = row.RestaurantId,
                        RestaurantId = row.RestaurantId,
                        Title = row.Name,
                        Subtitle = $"مالک: {row.OwnerName}",
                        OccurredAt = row.CreatedAt,
                        BadgeLabel = "رستوران",
                        BadgeClass = "badge-restaurant",
                        IconClass = "fas fa-store"
                    }).ToList(),
                    Support = supportRows.Select(row =>
                    {
                        var unreadPart = row.UnreadBySupport > 0 ? $" · {row.UnreadBySupport} خوانده‌نشده" : "";
                        return new ActivityFeedItem
                        {
                            Type = ActivityFeedTypes.SupportActivity,
                            EntityId = row.Id,
                            RestaurantId = row.RestaurantId,
                            Title = row.DisplayName,
                            Subtitle = $"چت پشتیبانی{unreadPart}",
                            OccurredAt = row.LastMessageAtUtc.ToLocalTime(),
                            BadgeLabel = "پشتیبانی",
                            BadgeClass = "badge-support",
                            IconClass = "fas fa-comments"
                        };
                    }).ToList(),
                    Foods = foodRows.Select(row => new ActivityFeedItem
                    {
                        Type = ActivityFeedTypes.FoodCreated,
                        EntityId = row.FoodItemId,
                        RestaurantId = row.RestaurantId,
                        Title = row.FoodName,
                        Subtitle = $"{row.RestaurantName} · {(string.IsNullOrWhiteSpace(row.CategoryName) ? "بدون دسته" : row.CategoryName)} · {row.Price:N0} تومان",
                        OccurredAt = row.CreatedAt ?? DateTime.MinValue,
                        BadgeLabel = "غذا",
                        BadgeClass = "badge-food",
                        IconClass = "fas fa-utensils"
                    }).ToList(),
                    Customers = customerRows.Select(row => new ActivityFeedItem
                    {
                        Type = ActivityFeedTypes.CustomerCreated,
                        EntityId = row.CustomerId,
                        RestaurantId = row.RestaurantId,
                        Title = string.IsNullOrWhiteSpace(row.FullName) ? row.Mobile : row.FullName,
                        Subtitle = $"{row.RestaurantName} · {row.Mobile}",
                        OccurredAt = row.CreatedAt,
                        BadgeLabel = "مشتری",
                        BadgeClass = "badge-customer",
                        IconClass = "fas fa-user"
                    }).ToList(),
                    Owners = ownerBaseRows.Select(row => new ActivityFeedItem
                    {
                        Type = ActivityFeedTypes.OwnerRegistered,
                        EntityId = row.Id,
                        RestaurantId = null,
                        Title = row.Name,
                        Subtitle = $"{row.Phone} · {ownerRestaurantCounts.GetValueOrDefault(row.Id)} رستوران",
                        OccurredAt = DateTime.MinValue,
                        BadgeLabel = "مالک",
                        BadgeClass = "badge-owner",
                        IconClass = "fas fa-user-tie"
                    }).ToList(),
                    CouponUsages = couponUsageRows.Select(row => new ActivityFeedItem
                    {
                        Type = ActivityFeedTypes.CouponUsed,
                        EntityId = row.Id,
                        RestaurantId = row.RestaurantId,
                        Title = row.CouponCode,
                        Subtitle = $"{row.RestaurantName} · تخفیف {row.DiscountAmount:N0} تومان · {row.Status}",
                        OccurredAt = row.UsedAt,
                        BadgeLabel = "کد تخفیف",
                        BadgeClass = "badge-coupon",
                        IconClass = "fas fa-ticket-alt"
                    }).ToList(),
                    InventoryMovements = inventoryRows.Select(row => new ActivityFeedItem
                    {
                        Type = ActivityFeedTypes.InventoryMovement,
                        EntityId = row.MovementId,
                        RestaurantId = row.RestaurantId,
                        Title = row.ItemName,
                        Subtitle = $"{row.RestaurantName} · {GetInventoryReasonLabel(row.Reason)} · {FormatSignedQuantity(row.DeltaQuantity)}",
                        OccurredAt = row.CreatedAt.Kind == DateTimeKind.Utc ? row.CreatedAt.ToLocalTime() : row.CreatedAt,
                        BadgeLabel = "انبار",
                        BadgeClass = "badge-inventory",
                        IconClass = "fas fa-boxes"
                    }).ToList(),
                    OwnerLogins = ownerLoginRows.Select(row =>
                    {
                        var estimatedLogin = EstimateLoginFromExpiry(row.ExpiryTime, refreshLifetime);
                        return new ActivityFeedItem
                        {
                            Type = ActivityFeedTypes.OwnerLogin,
                            EntityId = row.Id,
                            RestaurantId = null,
                            Title = row.OwnerName,
                            Subtitle = $"{row.OwnerPhone} · ورود اپ/API",
                            OccurredAt = estimatedLogin ?? DateTime.MinValue,
                            BadgeLabel = "ورود مالک",
                            BadgeClass = "badge-owner-login",
                            IconClass = "fas fa-sign-in-alt"
                        };
                    }).ToList(),
                    StaffLogins = staffLoginRows.Select(row => new ActivityFeedItem
                    {
                        Type = ActivityFeedTypes.StaffLogin,
                        EntityId = row.Id,
                        RestaurantId = row.RestaurantId,
                        Title = row.StaffName,
                        Subtitle = $"{row.RestaurantName} · {(string.IsNullOrWhiteSpace(row.RoleName) ? "پرسنل" : row.RoleName)}",
                        OccurredAt = row.CreatedAtUtc.ToLocalTime(),
                        BadgeLabel = "ورود پرسنل",
                        BadgeClass = "badge-staff-login",
                        IconClass = "fas fa-id-badge"
                    }).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بارگذاری فعالیت‌های اخیر");
                return StatusCode(500, "خطای داخلی سرور: " + ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetActivityDetail(string type, long id)
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != "true")
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(type) || id <= 0)
                return Json(new { success = false, message = "درخواست نامعتبر است" });

            try
            {
                var excludedIds = GetExcludedOwnerIds();

                switch (type)
                {
                    case ActivityFeedTypes.OrderCreated:
                    {
                        var orderId = (int)id;
                        var header = await (
                            from o in _context.Orders.AsNoTracking()
                            join r in _context.Restaurants.AsNoTracking() on o.RestaurantId equals r.restaurant_id
                            join st in _context.OrderStatus.AsNoTracking() on o.StatusId equals st.OrderStatusId
                            where o.OrderId == orderId && !excludedIds.Contains(r.owner_id)
                            select new
                            {
                                o.OrderId,
                                o.RestaurantId,
                                RestaurantName = r.name,
                                o.TableNumber,
                                o.OrderType,
                                o.Description,
                                o.CreatedAt,
                                StatusName = st.StatusName,
                                o.CustomerId
                            }).FirstOrDefaultAsync();

                        if (header == null)
                            return Json(new { success = false, message = "سفارش یافت نشد" });

                        string customerName = null;
                        string customerMobile = null;
                        if (header.CustomerId.HasValue)
                        {
                            var customer = await _context.Customers.AsNoTracking()
                                .Where(c => c.CustomerId == header.CustomerId.Value)
                                .Select(c => new { c.FullName, c.Mobile })
                                .FirstOrDefaultAsync();
                            if (customer != null)
                            {
                                customerName = customer.FullName;
                                customerMobile = customer.Mobile;
                            }
                        }

                        var lineItems = await _context.OrderItems.AsNoTracking()
                            .Where(oi => oi.OrderId == orderId)
                            .Select(oi => new ActivityOrderItemDto
                            {
                                FoodName = oi.FoodName ?? ("#" + oi.FoodItemId),
                                Quantity = oi.Quantity,
                                UnitPrice = oi.UnitPriceWithDiscount ?? oi.UnitPrice,
                                LineTotal = oi.Quantity * (oi.UnitPriceWithDiscount ?? oi.UnitPrice)
                            })
                            .ToListAsync();

                        var detail = new ActivityOrderDetailDto
                        {
                            OrderId = header.OrderId,
                            RestaurantId = header.RestaurantId,
                            RestaurantName = header.RestaurantName,
                            TableNumber = header.TableNumber,
                            StatusName = header.StatusName,
                            OrderTypeLabel = GetOrderTypeLabel(header.OrderType),
                            Description = header.Description,
                            CreatedAt = header.CreatedAt,
                            CustomerName = customerName,
                            CustomerMobile = customerMobile,
                            Items = lineItems,
                            ItemsTotal = lineItems.Sum(i => i.LineTotal)
                        };

                        return Json(new { success = true, type, data = detail });
                    }

                    case ActivityFeedTypes.SubscriptionPurchased:
                    {
                        var subscriptionId = (int)id;
                        var detail = await (
                            from s in _context.Subscriptions.AsNoTracking()
                            join r in _context.Restaurants.AsNoTracking() on s.RestaurantId equals r.restaurant_id
                            join o in _context.Owners.AsNoTracking() on s.OwnerId equals o.Id
                            join p in _context.SubscriptionPlans.AsNoTracking() on s.SubscriptionPlanId equals p.Id
                            where s.Id == subscriptionId && !excludedIds.Contains(s.OwnerId)
                            select new ActivitySubscriptionDetailDto
                            {
                                SubscriptionId = s.Id,
                                RestaurantId = s.RestaurantId,
                                RestaurantName = r.name,
                                OwnerName = o.Name,
                                OwnerPhone = o.Phone,
                                PlanName = p.Name,
                                Period = s.SubscriptionPeriod,
                                Status = s.Status,
                                PricePaid = s.PricePaid,
                                DiscountApplied = s.DiscountApplied,
                                PaymentMethod = s.PaymentMethod,
                                TransactionId = s.TransactionId,
                                PurchaseDate = s.PurchaseDate,
                                StartDate = s.StartDate,
                                EndDate = s.EndDate
                            }).FirstOrDefaultAsync();

                        if (detail == null)
                            return Json(new { success = false, message = "اشتراک یافت نشد" });

                        return Json(new { success = true, type, data = detail });
                    }

                    case ActivityFeedTypes.RestaurantCreated:
                    {
                        var restaurantId = (int)id;
                        var detail = await (
                            from r in _context.Restaurants.AsNoTracking()
                            join o in _context.Owners.AsNoTracking() on r.owner_id equals o.Id
                            where r.restaurant_id == restaurantId && !excludedIds.Contains(r.owner_id)
                            select new ActivityRestaurantDetailDto
                            {
                                RestaurantId = r.restaurant_id,
                                Name = r.name,
                                RestaurantCode = r.restaurant_code,
                                OwnerName = o.Name,
                                OwnerPhone = o.Phone,
                                CreatedAt = r.CreatedAt,
                                ReceiptChargesEnabled = r.ReceiptChargesEnabled
                            }).FirstOrDefaultAsync();

                        if (detail == null)
                            return Json(new { success = false, message = "رستوران یافت نشد" });

                        return Json(new { success = true, type, data = detail });
                    }

                    case ActivityFeedTypes.SupportActivity:
                    {
                        var conversation = await _context.SupportConversations.AsNoTracking()
                            .Where(c => c.Id == id && (c.OwnerId == null || !excludedIds.Contains(c.OwnerId.Value)))
                            .Select(c => new
                            {
                                c.Id,
                                c.RestaurantId,
                                c.RestaurantName,
                                c.OwnerName,
                                c.OwnerPhone,
                                c.UnreadBySupport,
                                c.CreatedAtUtc,
                                c.LastMessageAtUtc,
                                c.LastPageUrl
                            })
                            .FirstOrDefaultAsync();

                        if (conversation == null)
                            return Json(new { success = false, message = "گفتگو یافت نشد" });

                        var lastMessage = await _context.SupportMessages.AsNoTracking()
                            .Where(m => m.ConversationId == id)
                            .OrderByDescending(m => m.CreatedAtUtc)
                            .Select(m => m.Body)
                            .FirstOrDefaultAsync();

                        var detail = new ActivitySupportDetailDto
                        {
                            ConversationId = conversation.Id,
                            RestaurantId = conversation.RestaurantId,
                            RestaurantName = conversation.RestaurantName,
                            OwnerName = conversation.OwnerName,
                            OwnerPhone = conversation.OwnerPhone,
                            UnreadBySupport = conversation.UnreadBySupport,
                            CreatedAtUtc = conversation.CreatedAtUtc,
                            LastMessageAtUtc = conversation.LastMessageAtUtc,
                            LastMessagePreview = TruncateText(lastMessage, 160),
                            LastPageUrl = conversation.LastPageUrl
                        };

                        return Json(new { success = true, type, data = detail });
                    }

                    case ActivityFeedTypes.FoodCreated:
                    {
                        var foodId = (int)id;
                        var detail = await (
                            from f in _context.FoodItems.AsNoTracking()
                            join r in _context.Restaurants.AsNoTracking() on f.RestaurantId equals r.restaurant_id
                            where f.FoodItemId == foodId && !excludedIds.Contains(r.owner_id)
                            select new ActivityFoodDetailDto
                            {
                                FoodItemId = f.FoodItemId,
                                RestaurantId = f.RestaurantId,
                                RestaurantName = r.name,
                                Name = f.Name,
                                CategoryName = f.CategoryName,
                                Price = f.Price,
                                DiscountPrice = f.DiscountPrice,
                                IsAvailable = f.IsAvailable,
                                IsActive = f.IsActive,
                                CreatedAt = f.CreatedAt,
                                ImageUrl = f.ImageUrl
                            }).FirstOrDefaultAsync();

                        if (detail == null)
                            return Json(new { success = false, message = "غذا یافت نشد" });

                        return Json(new { success = true, type, data = detail });
                    }

                    case ActivityFeedTypes.CustomerCreated:
                    {
                        var customerId = (int)id;
                        var detail = await (
                            from c in _context.Customers.AsNoTracking()
                            join r in _context.Restaurants.AsNoTracking() on c.RestaurantId equals r.restaurant_id
                            where c.CustomerId == customerId && !excludedIds.Contains(r.owner_id)
                            select new ActivityCustomerDetailDto
                            {
                                CustomerId = c.CustomerId,
                                RestaurantId = c.RestaurantId,
                                RestaurantName = r.name,
                                FullName = c.FullName,
                                Mobile = c.Mobile,
                                IsActive = c.IsActive,
                                CreatedAt = c.CreatedAt,
                                Description = c.Description
                            }).FirstOrDefaultAsync();

                        if (detail == null)
                            return Json(new { success = false, message = "مشتری یافت نشد" });

                        return Json(new { success = true, type, data = detail });
                    }

                    case ActivityFeedTypes.OwnerRegistered:
                    {
                        var ownerId = (int)id;
                        var owner = await _context.Owners.AsNoTracking()
                            .Where(o => o.Id == ownerId && !excludedIds.Contains(o.Id))
                            .Select(o => new { o.Id, o.Name, o.Phone })
                            .FirstOrDefaultAsync();

                        if (owner == null)
                            return Json(new { success = false, message = "مالک یافت نشد" });

                        var restaurantCount = await _context.Restaurants.AsNoTracking()
                            .CountAsync(r => r.owner_id == ownerId);

                        var detail = new ActivityOwnerDetailDto
                        {
                            OwnerId = owner.Id,
                            Name = owner.Name,
                            Phone = owner.Phone,
                            RestaurantCount = restaurantCount
                        };

                        return Json(new { success = true, type, data = detail });
                    }

                    case ActivityFeedTypes.CouponUsed:
                    {
                        var usageId = (int)id;
                        var detail = await (
                            from u in _context.CouponUsages.AsNoTracking()
                            join c in _context.Coupons.AsNoTracking() on u.CouponId equals c.Id
                            join r in _context.Restaurants.AsNoTracking() on u.RestaurantId equals r.restaurant_id
                            join o in _context.Owners.AsNoTracking() on u.OwnerId equals o.Id
                            where u.Id == usageId && !excludedIds.Contains(u.OwnerId)
                            select new ActivityCouponUsageDetailDto
                            {
                                UsageId = u.Id,
                                CouponCode = c.Code,
                                RestaurantId = u.RestaurantId,
                                RestaurantName = r.name,
                                OwnerName = o.Name,
                                OwnerPhone = o.Phone,
                                DiscountAmount = u.DiscountAmount,
                                AppliedPrice = u.AppliedPrice,
                                Status = u.Status,
                                UsedAt = u.UsedAt,
                                SubscriptionId = u.SubscriptionId
                            }).FirstOrDefaultAsync();

                        if (detail == null)
                            return Json(new { success = false, message = "استفاده از کد تخفیف یافت نشد" });

                        return Json(new { success = true, type, data = detail });
                    }

                    case ActivityFeedTypes.InventoryMovement:
                    {
                        var movementId = (int)id;
                        var detail = await (
                            from m in _context.InventoryMovements.AsNoTracking()
                            join r in _context.Restaurants.AsNoTracking() on m.RestaurantId equals r.restaurant_id
                            join i in _context.InventoryItems.AsNoTracking() on m.InventoryItemId equals i.InventoryItemId
                            where m.MovementId == movementId && !excludedIds.Contains(r.owner_id)
                            select new ActivityInventoryDetailDto
                            {
                                MovementId = m.MovementId,
                                RestaurantId = m.RestaurantId,
                                RestaurantName = r.name,
                                ItemName = i.Name,
                                Reason = m.Reason,
                                ReasonLabel = null,
                                DeltaQuantity = m.DeltaQuantity,
                                QuantityAfter = m.QuantityAfter,
                                UnitPrice = m.UnitPrice,
                                Note = m.Note,
                                CreatedAt = m.CreatedAt
                            }).FirstOrDefaultAsync();

                        if (detail == null)
                            return Json(new { success = false, message = "حرکت انبار یافت نشد" });

                        detail.ReasonLabel = GetInventoryReasonLabel(detail.Reason);
                        if (detail.CreatedAt.Kind == DateTimeKind.Utc)
                            detail.CreatedAt = detail.CreatedAt.ToLocalTime();

                        return Json(new { success = true, type, data = detail });
                    }

                    case ActivityFeedTypes.OwnerLogin:
                    {
                        var tokenId = (int)id;
                        var row = await (
                            from t in _context.RefreshTokens.AsNoTracking()
                            join o in _context.Owners.AsNoTracking() on t.OwnerId equals o.Id
                            where t.Id == tokenId && !excludedIds.Contains(o.Id)
                            select new
                            {
                                t.Id,
                                OwnerId = o.Id,
                                OwnerName = o.Name,
                                OwnerPhone = o.Phone,
                                t.ExpiryTime
                            }).FirstOrDefaultAsync();

                        if (row == null)
                            return Json(new { success = false, message = "ورود مالک یافت نشد" });

                        var detail = new ActivityOwnerLoginDetailDto
                        {
                            TokenId = row.Id,
                            OwnerId = row.OwnerId,
                            OwnerName = row.OwnerName,
                            OwnerPhone = row.OwnerPhone,
                            ExpiryTime = row.ExpiryTime,
                            EstimatedLoginAt = EstimateLoginFromExpiry(row.ExpiryTime, GetRefreshTokenLifetime())
                        };

                        return Json(new { success = true, type, data = detail });
                    }

                    case ActivityFeedTypes.StaffLogin:
                    {
                        var tokenId = (int)id;
                        var detail = await (
                            from t in _context.StaffRefreshTokens.AsNoTracking()
                            join u in _context.Users.AsNoTracking() on t.UserId equals u.user_id
                            join r in _context.Restaurants.AsNoTracking() on t.RestaurantId equals r.restaurant_id
                            join role in _context.Roles.AsNoTracking() on u.role_id equals role.role_id into roles
                            from role in roles.DefaultIfEmpty()
                            where t.Id == tokenId && !excludedIds.Contains(r.owner_id)
                            select new ActivityStaffLoginDetailDto
                            {
                                TokenId = t.Id,
                                UserId = u.user_id,
                                StaffName = u.name,
                                RoleName = role != null ? role.role_name : null,
                                RestaurantId = t.RestaurantId,
                                RestaurantName = r.name,
                                CreatedAtUtc = t.CreatedAtUtc,
                                ExpiryTime = t.ExpiryTime
                            }).FirstOrDefaultAsync();

                        if (detail == null)
                            return Json(new { success = false, message = "ورود پرسنل یافت نشد" });

                        return Json(new { success = true, type, data = detail });
                    }

                    default:
                        return Json(new { success = false, message = "نوع فعالیت نامعتبر است" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت جزئیات فعالیت {Type}/{Id}", type, id);
                return Json(new { success = false, message = "خطا در دریافت جزئیات" });
            }
        }

        private static string GetOrderTypeLabel(OrderTypeKind orderType)
        {
            return orderType switch
            {
                OrderTypeKind.Takeaway => "بیرون‌بر",
                OrderTypeKind.Delivery => "ارسال",
                _ => "حضوری"
            };
        }

        private static string GetInventoryReasonLabel(string reason)
        {
            return reason switch
            {
                InventoryMovementReasons.Opening => "موجودی اولیه",
                InventoryMovementReasons.Purchase => "خرید",
                InventoryMovementReasons.Adjustment => "تعدیل",
                InventoryMovementReasons.Waste => "ضایعات",
                InventoryMovementReasons.Correction => "اصلاح",
                InventoryMovementReasons.SaleConsumption => "مصرف فروش",
                _ => string.IsNullOrWhiteSpace(reason) ? "نامشخص" : reason
            };
        }

        private static string FormatSignedQuantity(decimal quantity)
        {
            var formatted = quantity.ToString("0.###");
            return quantity > 0 ? "+" + formatted : formatted;
        }

        private TimeSpan GetRefreshTokenLifetime()
        {
            var refreshMinutes = _configuration.GetValue<int>("Jwt:RefreshExpirationMinutes");
            if (refreshMinutes <= 0)
                refreshMinutes = _configuration.GetValue<int>("JwtSettings:RefreshExpirationMinutes");
            if (refreshMinutes > 0)
                return TimeSpan.FromMinutes(refreshMinutes);

            var refreshDays = _configuration.GetValue<int>("Jwt:RefreshExpirationDays");
            if (refreshDays <= 0)
                refreshDays = _configuration.GetValue<int>("JwtSettings:RefreshExpirationDays");
            if (refreshDays <= 0)
                refreshDays = 30;

            return TimeSpan.FromDays(refreshDays);
        }

        private static DateTime? EstimateLoginFromExpiry(DateTime expiryTime, TimeSpan lifetime)
        {
            if (lifetime <= TimeSpan.Zero)
                return null;

            var estimated = expiryTime - lifetime;
            if (estimated.Kind == DateTimeKind.Utc)
                estimated = estimated.ToLocalTime();
            return estimated;
        }

        private static string TruncateText(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            value = value.Trim();
            if (value.Length <= maxLength)
                return value;
            return value.Substring(0, maxLength) + "…";
        }
    }
}