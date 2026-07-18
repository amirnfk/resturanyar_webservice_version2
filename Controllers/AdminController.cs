using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Models.AdminMessage;
using resturanyar.Models.Copoun;
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
                134, 135, 137, 139, 140, 142, 143, 144, 145, 146,
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
                viewModel.TotalRestaurants = await _context.Restaurants
                    .Where(r => !excludedIds.Contains(r.owner_id))
                    .CountAsync();

                viewModel.TotalOwners = await _context.Owners
                    .Where(o => !excludedIds.Contains(o.Id))
                    .CountAsync();



                viewModel.TotalSubscriptions = await _context.Subscriptions
                    .Where(s => !excludedIds.Contains(s.OwnerId))
                    .CountAsync();

                viewModel.ActiveSubscriptions = await _context.Subscriptions
                    .Where(s => s.Status == "Active" && s.EndDate >= today && !excludedIds.Contains(s.OwnerId))
                    .CountAsync();

                viewModel.TotalRevenue = await _context.Subscriptions
                    .Where(s => s.IsPaid == true && !excludedIds.Contains(s.OwnerId))
                    .SumAsync(s => s.PricePaid);

                // ===== لیست رستوران‌ها (با فیلتر) =====
                var restaurantsQuery = from r in _context.Restaurants
                                       join o in _context.Owners on r.owner_id equals o.Id
                                       where !excludedIds.Contains(o.Id)
                                       select new RestaurantStatusViewModel
                                       {
                                           RestaurantId = r.restaurant_id,
                                           Name = r.name,
                                           OwnerName = o.Name,
                                           OwnerPhone = o.Phone,
                                           TotalSubscriptions = _context.Subscriptions
                               .Count(s => s.RestaurantId == r.restaurant_id && s.IsPaid == true),
                                           SubscriptionStatus = _context.Subscriptions
                                               .Where(s => s.RestaurantId == r.restaurant_id && s.Status == "Active" && s.EndDate >= today)
                                               .Any() ? "Active" :
                                               _context.Subscriptions.Any(s => s.RestaurantId == r.restaurant_id) ? "Expired" : "None",
                                           SubscriptionEndDate = _context.Subscriptions
                                               .Where(s => s.RestaurantId == r.restaurant_id)
                                               .OrderByDescending(s => s.EndDate)
                                               .Select(s => (DateTime?)s.EndDate)
                                               .FirstOrDefault(),
                                           PlanName = (from s in _context.Subscriptions
                                                       join p in _context.SubscriptionPlans on s.SubscriptionPlanId equals p.Id
                                                       where s.RestaurantId == r.restaurant_id
                                                       orderby s.EndDate descending
                                                       select p.Name).FirstOrDefault()
                                       };

                var startDate = new DateTime(today.Year, today.Month, 1).AddMonths(-5);

                var monthlyStats = new List<MonthlyStatsViewModel>();

                for (int i = 0; i < 6; i++)
                {
                    var month = startDate.AddMonths(i);
                    var monthStart = new DateTime(month.Year, month.Month, 1);
                    var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                    var revenue = await _context.Subscriptions
                        .Where(s => s.IsPaid == true
                            && s.PurchaseDate >= monthStart
                            && s.PurchaseDate <= monthEnd
                            && !excludedIds.Contains(s.OwnerId))
                        .SumAsync(s => s.PricePaid);

                    var newSubs = await _context.Subscriptions
                        .Where(s => s.PurchaseDate >= monthStart
                            && s.PurchaseDate <= monthEnd
                            && !excludedIds.Contains(s.OwnerId))
                        .CountAsync();

                    monthlyStats.Add(new MonthlyStatsViewModel
                    {
                        Label = month.ToString("yyyy/MM"),
                        Revenue = revenue,
                        NewSubscriptions = newSubs
                    });
                }

                viewModel.MonthlyStats = monthlyStats;

                viewModel.Restaurants = await restaurantsQuery.ToListAsync();

                // ===== لیست مالک‌ها (با فیلتر) =====
                var ownersQuery = from o in _context.Owners
                                  where !excludedIds.Contains(o.Id)
                                  select new OwnerSummaryViewModel
                                  {
                                      OwnerId = o.Id,
                                      Name = o.Name,
                                      Phone = o.Phone,
                                      RestaurantCount = _context.Restaurants.Count(r => r.owner_id == o.Id),
                                      ActiveSubscriptionCount = _context.Subscriptions
                                          .Count(s => s.OwnerId == o.Id && s.Status == "Active" && s.EndDate >= today),
                                      TotalSpent = _context.Subscriptions
                                          .Where(s => s.OwnerId == o.Id && s.IsPaid == true)
                                          .Sum(s => s.PricePaid),
                                      LastPurchaseDate = _context.Subscriptions
                                          .Where(s => s.OwnerId == o.Id && s.IsPaid == true)
                                          .OrderByDescending(s => s.PurchaseDate)
                                          .Select(s => (DateTime?)s.PurchaseDate)
                                          .FirstOrDefault()
                                  };

                viewModel.Owners = await ownersQuery.ToListAsync();

                // ===== اشتراک‌های در حال انقضا (با فیلتر) =====
                var expiringData = await (from s in _context.Subscriptions
                                          join r in _context.Restaurants on s.RestaurantId equals r.restaurant_id
                                          join o in _context.Owners on s.OwnerId equals o.Id
                                          join p in _context.SubscriptionPlans on s.SubscriptionPlanId equals p.Id
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
                                          })
                                          .ToListAsync();

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

                // ===== آخرین اشتراک‌های خریداری شده (با فیلتر) =====
                var recentQuery = (from s in _context.Subscriptions
                                   join r in _context.Restaurants on s.RestaurantId equals r.restaurant_id
                                   join o in _context.Owners on s.OwnerId equals o.Id
                                   join p in _context.SubscriptionPlans on s.SubscriptionPlanId equals p.Id
                                   where !excludedIds.Contains(o.Id)
                                   orderby s.PurchaseDate descending
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
                                   .Take(10);

                viewModel.RecentSubscriptions = await recentQuery.ToListAsync();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بارگذاری AdminPanel");
                return StatusCode(500, "خطای داخلی سرور"+ex.ToString);
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

                // یک کوئری واحد برای دریافت اطلاعات تمام رستوران‌ها و شمارش‌های مورد نیاز
                var query = from r in _context.Restaurants
                            where !excludedIds.Contains(r.owner_id)
                            select new
                            {
                                RestaurantId = r.restaurant_id,
                                RestaurantName = r.name,
                                Orders1Day = _context.Orders.Count(o => o.RestaurantId == r.restaurant_id && o.CreatedAt >= oneDayAgo),
                                Orders7Day = _context.Orders.Count(o => o.RestaurantId == r.restaurant_id && o.CreatedAt >= sevenDaysAgo),
                                Orders30Day = _context.Orders.Count(o => o.RestaurantId == r.restaurant_id && o.CreatedAt >= thirtyDaysAgo),
                                FoodItems1Day = _context.FoodItems.Count(f => f.RestaurantId == r.restaurant_id && f.CreatedAt >= oneDayAgo),
                                FoodItems7Day = _context.FoodItems.Count(f => f.RestaurantId == r.restaurant_id && f.CreatedAt >= sevenDaysAgo),
                                FoodItems30Day = _context.FoodItems.Count(f => f.RestaurantId == r.restaurant_id && f.CreatedAt >= thirtyDaysAgo)
                            };

                var result = await query.AsNoTracking().ToListAsync();

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
    }
}