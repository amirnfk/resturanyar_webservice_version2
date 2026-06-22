using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using resturanyar.Models.ViewModels.Admin;
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

        public AdminController(AppDbContext context, ILogger<HomeController> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
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
                return StatusCode(500, "خطای داخلی سرور");
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

            var adminUsername = "anoorafkan";
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

                // دریافت لیست رستوران‌ها (با فیلتر مالک‌های حذف‌شده)
                var restaurants = await _context.Restaurants
                    .Where(r => !excludedIds.Contains(r.owner_id))
                    .Select(r => new { r.restaurant_id, r.name })
                    .ToListAsync();

                var result = new List<object>();

                foreach (var r in restaurants)
                {
                    // محاسبه تعداد سفارش‌ها
                    var orders1Day = await _context.Orders
                        .Where(o => o.RestaurantId == r.restaurant_id && o.CreatedAt >= now.AddDays(-1))
                        .CountAsync();

                    var orders7Day = await _context.Orders
                        .Where(o => o.RestaurantId == r.restaurant_id && o.CreatedAt >= now.AddDays(-7))
                        .CountAsync();

                    var orders30Day = await _context.Orders
                        .Where(o => o.RestaurantId == r.restaurant_id && o.CreatedAt >= now.AddDays(-30))
                        .CountAsync();

                    // محاسبه تعداد غذاهای ثبت‌شده
                    var foodItems1Day = await _context.FoodItems
                        .Where(f => f.RestaurantId == r.restaurant_id && f.CreatedAt >= now.AddDays(-1))
                        .CountAsync();

                    var foodItems7Day = await _context.FoodItems
                        .Where(f => f.RestaurantId == r.restaurant_id && f.CreatedAt >= now.AddDays(-7))
                        .CountAsync();

                    var foodItems30Day = await _context.FoodItems
                        .Where(f => f.RestaurantId == r.restaurant_id && f.CreatedAt >= now.AddDays(-30))
                        .CountAsync();

                    // اضافه کردن شیء نهایی به لیست نتیجه
                    result.Add(new
                    {
                        RestaurantId = r.restaurant_id,
                        RestaurantName = r.name,
                        Orders1Day = orders1Day,
                        Orders7Day = orders7Day,
                        Orders30Day = orders30Day,
                        FoodItems1Day = foodItems1Day,
                        FoodItems7Day = foodItems7Day,
                        FoodItems30Day = foodItems30Day
                    });
                }

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت آمار جزئی رستوران‌ها");
                return Json(new { success = false, message = "خطا در دریافت داده‌ها" });
            }
        }
    }
}