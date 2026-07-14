using ClosedXML.Excel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using resturanyar.Models;
using resturanyar.Models.Copoun;
using resturanyar.Models.ViewModels;
using resturanyar.Models.ViewModels.DashboardStat;
using resturanyar.Utility;
using Resturanyar.Data;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using static Resturanyar.Controllers.Api.UserApiController;
using CreateZarinpalPaymentRequest = resturanyar.Models.Copoun.CreateZarinpalPaymentRequest;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;


namespace resturanyar.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public HomeController(AppDbContext context, ILogger<HomeController> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }
        public IActionResult Manage(int restaurantId)
        {
            var restaurant = _context.Restaurants
                .FirstOrDefault(r => r.restaurant_id == restaurantId);

            var users = _context.Users
                .Where(u => u.restaurant_id == restaurantId)
                .Include(u => u.Role)
                .ToList();

            ViewBag.RestaurantName = restaurant?.name;
            return View(users);
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [Authorize]
        public IActionResult PrepareUpgrade(int restaurantId)
        {

            HttpContext.Session.SetInt32("UpgradeRestaurantId", restaurantId);

            var restaurant = _context.Restaurants.Find(restaurantId);
            if (restaurant != null)
                HttpContext.Session.SetString("UpgradeRestaurantName", restaurant.name);


            return RedirectToAction("Upgrade");
        }

        [Authorize]
        public IActionResult Upgrade()
        {
            var restaurantId = HttpContext.Session.GetInt32("UpgradeRestaurantId");
            if (restaurantId == null)
            {
                return RedirectToAction("ChooseRestaurant", "Home");
            }

            var restaurant = _context.Restaurants.Find(restaurantId.Value);
            ViewBag.RestaurantId = restaurantId.Value;
            ViewBag.RestaurantName = restaurant?.name ?? "";

            return View();
        }

        public IActionResult Register()
        {
            return View();
        }


        public IActionResult ManagerLogin()
        {
            // جلوگیری از کش شدن صفحه
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            if (User.Identity.IsAuthenticated)
                return RedirectToAction("ChooseRestaurant", "Home");

            return View();
        }


        [HttpPost]
        public async Task<IActionResult> ManagerLogin(OwnerLoginRequest request)
        {
            request.Phone = request.Phone?.Trim().Replace(" ", "");


            if (request == null || string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Password))
            {
                ViewBag.Error = "شماره یا رمز عبور نمی‌تواند خالی باشد.";
                return View();
            }

            try
            {
                var owner = _context.Owners.FirstOrDefault(o => o.Phone == request.Phone);
                if (owner == null)
                {
                    ViewBag.Error = "شماره تلفن یافت نشد.";
                    return View();
                }

                if (DecodePassword(owner.Password) != request.Password)
                {
                    ViewBag.Error = "رمز عبور نادرست است.";
                    return View();
                }

                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, owner.Name ?? ""),
            new Claim("OwnerId", owner.Id.ToString()),
            new Claim(ClaimTypes.Role, "Owner")
        };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                return RedirectToAction("ChooseRestaurant", "Home");
            }
            catch (Exception ex)
            {

                ViewBag.Error = "خطای غیرمنتظره‌ای رخ داد. لطفاً دوباره تلاش کنید.";
                return View(request);

            }
        }


        public IActionResult StaffLogin()
        {
            return View();
        }
        public IActionResult Error()
        {
            return View();
        }



        public async Task<IActionResult> Dashboard()
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return RedirectToAction("ChooseRestaurant");

            var restaurant = await _context.Restaurants
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId);

            if (restaurant == null)
                return RedirectToAction("ChooseRestaurant");

            // ========== تعریف بازه‌های زمانی ==========
            var now = DateTime.Now;
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var yesterday = today.AddDays(-1);
            var yesterdaySameTime = now.AddDays(-1); // دیروز همین ساعت

            // بازه هفت روز گذشته برای پرفروش‌ترین‌ها
            var sevenDaysAgo = today.AddDays(-7);

            // ========== ۱. پرفروش‌ترین سه غذا ==========
            var topFoods = await _context.OrderItems
       .AsNoTracking()
       .Where(oi => oi.Order.RestaurantId == restaurantId
                    && oi.Order.CreatedAt >= sevenDaysAgo
                    && oi.Order.CreatedAt <= now)
       .GroupBy(oi => oi.FoodItemId)
       .Select(g => new TopFoodDto
       {
           FoodItemId = g.Key,
           FoodName = g.FirstOrDefault().FoodName ?? "بدون نام",
           ImageUrl = g.FirstOrDefault().FoodImageUrl ?? "/uploads/food_default.jpg",
           TotalQuantity = g.Sum(x => x.Quantity)
       })
       .OrderByDescending(x => x.TotalQuantity)
       .Take(3)
       .ToListAsync();


            // ========== ۲. وضعیت سفارش‌ها (امروز) ==========
            // ۲-۱. تعداد کل سفارش‌های امروز
            var totalOrdersToday = await _context.Orders
                .AsNoTracking()
                .Where(o => o.RestaurantId == restaurantId
                            && o.CreatedAt >= today
                            && o.CreatedAt < tomorrow)
                .CountAsync();

            // ۲-۲. تفکیک وضعیت‌ها به سه گروه
            // گروه‌بندی پیشنهادی (قابل ویرایش)
            var waiterStatuses = new[] { 3, 5 };
            var chefStatuses = new[] { 5, 4 };
            var cashierStatuses = new[] { 6, 7, 8, 11 };

            var statusGroups = await _context.Orders
                .AsNoTracking()
                .Where(o => o.RestaurantId == restaurantId
                            && o.CreatedAt >= today
                            && o.CreatedAt < tomorrow)
                .GroupBy(o => o.StatusId)
                .Select(g => new { StatusId = g.Key, Count = g.Count() })
                .ToListAsync();

            int waiterCount = statusGroups.Where(x => waiterStatuses.Contains(x.StatusId)).Sum(x => x.Count);
            int chefCount = statusGroups.Where(x => chefStatuses.Contains(x.StatusId)).Sum(x => x.Count);
            int cashierCount = statusGroups.Where(x => cashierStatuses.Contains(x.StatusId)).Sum(x => x.Count);

            // ۲-۳. تغییر نسبت به دیروز همین ساعت
            var ordersTodayUpToNow = await _context.Orders
                .AsNoTracking()
                .Where(o => o.RestaurantId == restaurantId
                            && o.CreatedAt >= today
                            && o.CreatedAt <= now)
                .CountAsync();

            var ordersYesterdaySameTime = await _context.Orders
                .AsNoTracking()
                .Where(o => o.RestaurantId == restaurantId
                            && o.CreatedAt >= yesterday
                            && o.CreatedAt <= yesterdaySameTime)
                .CountAsync();

            int changeCount = ordersTodayUpToNow - ordersYesterdaySameTime;
            double changePercent = 0;
            if (ordersYesterdaySameTime > 0)
                changePercent = Math.Round((double)changeCount / ordersYesterdaySameTime * 100, 2);

            // ========== ۳. وضعیت فروش ==========
            // ۳-۱. مبلغ فروش امروز
            var todayRevenue = await _context.OrderItems
                .AsNoTracking()
                .Where(oi => oi.Order.RestaurantId == restaurantId
                             && oi.Order.CreatedAt >= today
                             && oi.Order.CreatedAt < tomorrow)
                .SumAsync(oi => oi.Quantity * (oi.UnitPriceWithDiscount ?? oi.UnitPrice));

            // ۳-۲. مبلغ فروش دیروز تا همین ساعت
            var yesterdayRevenue = await _context.OrderItems
                .AsNoTracking()
                .Where(oi => oi.Order.RestaurantId == restaurantId
                             && oi.Order.CreatedAt >= yesterday
                             && oi.Order.CreatedAt <= yesterdaySameTime)
                .SumAsync(oi => oi.Quantity * (oi.UnitPriceWithDiscount ?? oi.UnitPrice));

            decimal revenueChange = todayRevenue - yesterdayRevenue;
            double revenueChangePercent = 0;
            if (yesterdayRevenue > 0)
                revenueChangePercent = Math.Round((double)(revenueChange / yesterdayRevenue * 100), 2);
            else if (todayRevenue > 0 && yesterdayRevenue == 0)
                revenueChangePercent = 100; // رشد ۱۰۰٪ نسبت به روز قبل
            else if (todayRevenue == 0 && yesterdayRevenue == 0)
                revenueChangePercent = 0;
            // ========== ساخت ViewModel ==========
            var vm = new DashboardStatsViewModel
            {
                RestaurantName = restaurant.name,

                TopFoods = topFoods,

                TotalOrdersToday = totalOrdersToday,
                WaiterOrdersCount = waiterCount,
                ChefOrdersCount = chefCount,
                CashierOrdersCount = cashierCount,
                OrdersChangeCount = changeCount,
                OrdersChangePercent = changePercent,
                // بخش فروش
                TodayRevenue = todayRevenue,
                RevenueChange = revenueChange,
                RevenueChangePercent = revenueChangePercent,
                // (اختیاری) اطلاعات قبلی را هم می‌توانید نگه دارید یا حذف کنید
                UsersCount = 0, // یا حذف کنید
                MenuItemsCount = 0,
                OrdersTodayCount = totalOrdersToday, // این همان است
                PublicMenuToken = ViewBag.PublicMenuToken
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetHourlySales()
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return Unauthorized();

            var now = DateTime.Now;
            var todayStart = DateTime.Today;
            var yesterdayStart = todayStart.AddDays(-1);

            // امروز: از ابتدای روز تا همین لحظه
            var todayQuery = await _context.OrderItems
                .Where(oi => oi.Order.RestaurantId == restaurantId &&
                             oi.Order.CreatedAt >= todayStart &&
                             oi.Order.CreatedAt <= now)
                .GroupBy(oi => oi.Order.CreatedAt.Hour)
                .Select(g => new { Hour = g.Key, Total = g.Sum(oi => oi.Quantity * (oi.UnitPriceWithDiscount ?? oi.UnitPrice)) })
                .ToListAsync();

            // دیروز: کامل
            var yesterdayQuery = await _context.OrderItems
                .Where(oi => oi.Order.RestaurantId == restaurantId &&
                             oi.Order.CreatedAt >= yesterdayStart &&
                             oi.Order.CreatedAt < todayStart)
                .GroupBy(oi => oi.Order.CreatedAt.Hour)
                .Select(g => new { Hour = g.Key, Total = g.Sum(oi => oi.Quantity * (oi.UnitPriceWithDiscount ?? oi.UnitPrice)) })
                .ToListAsync();

            // پر کردن آرایه‌های ۲۴ ساعته
            var todayTotals = new decimal[24];
            foreach (var item in todayQuery)
                todayTotals[item.Hour] = item.Total;

            var yesterdayTotals = new decimal[24];
            foreach (var item in yesterdayQuery)
                yesterdayTotals[item.Hour] = item.Total;

            return Json(new
            {
                today = todayTotals,
                yesterday = yesterdayTotals,
                labels = Enumerable.Range(0, 24).Select(h => $"{h:00}:00").ToArray()
            });
        }


        public IActionResult ManageUsers(int? restaurantId = null)
        {
            // اگر restaurantId به عنوان پارامتر نیامده، سعی کن از سشن بخوانی
            if (restaurantId == null)
            {
                restaurantId = User.GetRestaurantId();
                if (restaurantId == null)
                {
                    return RedirectToAction("ChooseRestaurant");
                }
            }

            if (restaurantId == null)
            {
                // اگر رستوران تعیین نشده، به صفحه‌ای مثلا انتخاب رستوران یا صفحه‌ای دیگر ریدایرکت کن
                return RedirectToAction("ChooseRestaurant");
            }

            var restaurant = _context.Restaurants.Find(restaurantId);
            if (restaurant == null)
            {
                ViewBag.Error = "رستوران یافت نشد.";
                return View("Error");
            }

            // دریافت کاربران رستوران با Include برای گرفتن نقش‌ها
            var users = _context.Users
                .Where(u => u.restaurant_id == restaurantId)
                .Include(u => u.Role)
                .ToList();

            // ارسال اطلاعات به ویو
            ViewBag.RestaurantName = restaurant.name;
            ViewBag.RestaurantId = restaurantId; // اضافه شده برای ارسال به ویو

            return View(users);  // ارسال لیست کاربران به ویو
        }


        [HttpPost]
        public IActionResult Register(AddOwnerRequest request)
        {
            var existingOwner = _context.Owners.FirstOrDefault(o => o.Phone == request.Phone);
            if (existingOwner != null)
            {
                ViewBag.Error = "این شماره تلفن قبلاً ثبت شده است.";
                return View();
            }

            var owner = new Owner
            {
                Name = request.Name,
                Phone = request.Phone,
                Password = request.Password,
                role_id = 1
            };

            _context.Owners.Add(owner);
            _context.SaveChanges();

            // Save OwnerId in session
            HttpContext.Session.SetInt32("OwnerId", owner.Id);

            // Redirect to ChooseRestaurant
            return RedirectToAction("ChooseRestaurant", "Home");
        }

        [HttpGet]
        public IActionResult ChooseRestaurant()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("ManagerLogin", "Home");

            var ownerId = User.FindFirst("OwnerId")?.Value;
            if (string.IsNullOrEmpty(ownerId))
                return RedirectToAction("ManagerLogin", "Home");

            var restaurants = _context.Restaurants
                .Where(r => r.owner_id == int.Parse(ownerId))
                .AsNoTracking()
                .ToList();

            ViewBag.Restaurants = restaurants;


            return View();
        }


        //[HttpPost]
        //public IActionResult AddRestaurant(AddRestaurantRequest request)
        //{
        //    int? ownerId = HttpContext.Session.GetInt32("OwnerId");
        //    if (ownerId == null)
        //        return RedirectToAction("ManagerLogin", "Home");


        //    request.owner_id = ownerId.Value;

        //    bool isDuplicate = _context.Restaurants.Any(r =>
        //        r.owner_id == request.owner_id &&
        //        r.name.ToLower().Trim() == request.name.ToLower().Trim()
        //    );

        //    if (isDuplicate)
        //    {
        //        ViewBag.Error = "این رستوران قبلاً ثبت شده است.";
        //        return RedirectToAction("ChooseRestaurant");
        //    }

        //    var restaurant = new Restaurant
        //    {
        //        name = request.name.Trim(),
        //        owner_id = request.owner_id,
        //        restaurant_code = GenerateUniqueRestaurantCode(),
        //        PublicMenuToken = Guid.NewGuid().ToString("N") // یک مقدار یونیک و رندوم
        //    };

        //    _context.Restaurants.Add(restaurant);
        //    _context.SaveChanges();

        //    return RedirectToAction("ChooseRestaurant");
        //}

        [HttpPost("addrestaurant")]
        public IActionResult AddRestaurant(AddRestaurantRequest request)
        {
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                var owner = _context.Owners.Find(request.owner_id);
                if (owner == null)
                    return NotFound(new { success = false, message = "مالک با این شناسه یافت نشد" });


                bool hasActiveGold = _context.Subscriptions
                    .Include(s => s.SubscriptionPlan)
                    .Any(s =>
                        s.OwnerId == request.owner_id &&
                        s.Status == "Active" &&
                        s.EndDate > DateTime.Now &&
                        s.SubscriptionPlan.Name == "طلایی"
                    );


                int restaurantCount = _context.Restaurants.Count(r => r.owner_id == request.owner_id);


                if (restaurantCount > 0 && !hasActiveGold)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "برای افزودن رستوران جدید، باید حداقل یک اشتراک طلایی فعال داشته باشید."
                    });
                }


                bool isDuplicate = _context.Restaurants.Any(r =>
                    r.owner_id == request.owner_id &&
                    r.name.ToLower().Trim() == request.name.ToLower().Trim()
                );

                if (isDuplicate)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "رستورانی با این نام قبلاً برای این مالک ثبت شده است."
                    });
                }

                // ✅ ساخت رستوران جدید
                var restaurant = new Restaurant
                {
                    name = request.name.Trim(),
                    owner_id = request.owner_id,
                    restaurant_code = GenerateUniqueRestaurantCode(),
                    PublicMenuToken = Guid.NewGuid().ToString("N"),
                };
                _context.Restaurants.Add(restaurant);
                _context.SaveChanges(); // ذخیره تا ID رستوران تولید شود

                // 👥 افزودن کاربران پیش‌فرض
                var defaultUsers = new List<User>
        {
            new User { name = "waiter1", role_id = 2, password = EncodePassword("123456"), restaurant_id = restaurant.restaurant_id, order_management_permission = true },
            new User { name = "chief1", role_id = 3, password = EncodePassword("123456"), restaurant_id = restaurant.restaurant_id, kitchen_management_permission = true },
            new User { name = "cashier1", role_id = 4, password = EncodePassword("123456"), restaurant_id = restaurant.restaurant_id, payment_management_permission = true }
        };
                _context.Users.AddRange(defaultUsers);
                _context.SaveChanges();

                // 🍽️ افزودن میز پیش‌فرض
                _context.RestaurantTables.Add(new RestaurantTable
                {
                    TableName = "میز اصلی",
                    Seats = 1,
                    RestaurantId = restaurant.restaurant_id,
                    CreatedAt = DateTime.Now
                });
                _context.SaveChanges();

                // 🆕 ==========================================
                // 🎁 منطق اعطای اشتراک طلایی رایگان برای اولین رستوران
                // ==========================================

                // چون قبلاً restaurantCount را گرفتیم، اگر 0 بود یعنی این اولین رستوران است
                if (restaurantCount == 0)
                {
                    // پیدا کردن پلن طلایی (فرض بر اینکه ID=4 است یا نام آن "طلایی" است)
                    // برای اطمینان بیشتر از نام یا کد استفاده می‌کنیم
                    var goldPlan = _context.SubscriptionPlans
                        .FirstOrDefault(p => p.Name == "طلایی" || p.Id == 4);

                    if (goldPlan != null)
                    {
                        var freeSubscription = new Subscription
                        {
                            RestaurantId = restaurant.restaurant_id,
                            OwnerId = owner.Id,
                            SubscriptionPlanId = goldPlan.Id,
                            SubscriptionPeriod = "3 روز", // ۱ ماهه
                            Status = "Active",
                            StartDate = DateTime.Now,
                            EndDate = DateTime.Now.AddDays(3),
                            PurchaseDate = DateTime.Now,
                            PricePaid = 0, // رایگان
                            DiscountApplied = 0,
                            PaymentMethod = "FreeTrial", // روش پرداخت آزمایشی
                            TransactionId = "",
                            IsPaid = true,
                            CafeBazarPurchaseToken = "",
                            CafeBazarOrderId = "",
                            AutoRenew = false,
                            NextRenewalDate = null,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now,
                            CanceledAt = null // یا null بسته به طراحی دیتابیس شما
                        };

                        _context.Subscriptions.Add(freeSubscription);
                        _context.SaveChanges();
                    }
                }


                bool isFirstRestaurantAndFreeTrialGiven = (restaurantCount == 0);

                transaction.Commit();


                string responseMessage = "رستوران جدید با موفقیت ثبت شد.";

                if (isFirstRestaurantAndFreeTrialGiven)
                {
                    responseMessage = "تبریک! رستوران شما با موفقیت ثبت شد و یک اشتراک طلایی 3 روزه رایگان به حساب شما اضافه گردید.";
                }

                return Ok(new
                {
                    success = true,
                    message = responseMessage,
                    restaurant_id = restaurant.restaurant_id,
                    restaurant_code = restaurant.restaurant_code,
                    has_free_trial = isFirstRestaurantAndFreeTrialGiven
                });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return Ok(new
                {
                    success = false,
                    message = "خطا در سرور: " + ex.GetBaseException().Message
                });
            }
        }

        private static string EncodePassword(string plainPassword)
        {
            if (string.IsNullOrEmpty(plainPassword)) return null;
            byte[] bytes = Encoding.UTF8.GetBytes(plainPassword);
            return Convert.ToBase64String(bytes);
        }

        private static string DecodePassword(string encodedPassword)
        {
            if (string.IsNullOrEmpty(encodedPassword)) return null;
            byte[] bytes = Convert.FromBase64String(encodedPassword);
            return Encoding.UTF8.GetString(bytes);
        }
        private string GenerateUniqueRestaurantCode()
        {
            Random rnd = new Random();
            string code;

            do
            {
                code = rnd.Next(100000, 999999).ToString();
            }
            while (_context.Restaurants.Any(r => r.restaurant_code == code));

            return code;
        }

        public async Task<IActionResult> SelectRestaurant(int restaurantId)
        {
            var subscription = await _context.Subscriptions
                .Include(s => s.SubscriptionPlan)
                .FirstOrDefaultAsync(s =>
                    s.RestaurantId == restaurantId &&
                    s.Status == "Active" &&
                    s.EndDate >= DateTime.Now);

            if (subscription == null || subscription.SubscriptionPlan == null || !subscription.SubscriptionPlan.CanUseWeb)
            {
                // ذخیره restaurantId و نام رستوران در Session
                HttpContext.Session.SetInt32("UpgradeRestaurantId", restaurantId);
                var restaurant = await _context.Restaurants.FindAsync(restaurantId);
                if (restaurant != null)
                    HttpContext.Session.SetString("UpgradeRestaurantName", restaurant.name);

                // هدایت به Upgrade بدون پارامتر (بدون نمایش ID در URL)
                return RedirectToAction("Upgrade");
            }

            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("ManagerLogin");

            var claims = User.Claims.Where(c => c.Type != "RestaurantId").ToList();
            claims.Add(new Claim("RestaurantId", restaurantId.ToString()));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectToAction("Dashboard", "Home");
        }









        [HttpPost]
        public IActionResult StaffLogin(LoginUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "لطفاً تمام فیلدها را پر کنید.";
                return View(request);
            }

            try
            {
                var restaurant = _context.Restaurants
                    .FirstOrDefault(r => r.restaurant_code == request.restaurant_code);
                if (restaurant == null)
                {
                    ViewBag.Error = "کد رستوران معتبر نیست.";
                    return View(request);
                }

                var user = _context.Users
                    .FirstOrDefault(u =>
                        u.name == request.name &&
                        u.password == request.password &&
                        u.restaurant_id == restaurant.restaurant_id);


                if (user == null)
                {
                    ViewBag.Error = "کاربری با این مشخصات یافت نشد.";
                    return View(request);
                }

                var roleName = _context.Roles
                    .Where(r => r.role_id == user.role_id)
                    .Select(r => r.role_name)
                    .FirstOrDefault();
                var restaurantId = user.restaurant_id;


                TempData["Success"] = $"خوش آمدید {user.name}!";

                if (roleName == "صندوقدار")
                {
                    HttpContext.Session.SetInt32("RestaurantId", restaurantId);
                    return RedirectToAction("CashierDashboard", "Home");
                }
                else if (roleName == "آشپز" || roleName == "گارسون")
                {
                    ViewBag.RoleMessage =
                        "این قسمت فقط برای صندوقدار رستوران قابل استفاده است. گارسون و آشپز می توانند از نسخه‌ی اندروید استفاده کنند.";
                    return View(request);
                }
                else
                {
                    ViewBag.Error = "شما دسترسی به این بخش را ندارید.";
                    return View(request);
                }
            }
            catch
            {
                ViewBag.Error = "خطای غیرمنتظره در سرور.";
                return View(request);
            }
        }




        //[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        //public IActionResult Error()
        //{
        //    return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        //}

        // GET: /Home/AddUser
        public IActionResult AddUser()
        {
            return View();
        }

        // POST: /Home/AddUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddUser(string name, string password, int role_id)
        {
            try
            {
                // گرفتن restaurant_id از سشن
                int? restaurantId = User.GetRestaurantId();
                if (restaurantId == null)
                {
                    return RedirectToAction("ChooseRestaurant");
                }


                // بررسی وجود رستوران
                var restaurant = _context.Restaurants.Find(restaurantId.Value);
                if (restaurant == null)
                {
                    TempData["Error"] = "رستوران یافت نشد.";
                    return RedirectToAction("AddUser");
                }

                // جلوگیری از نام تکراری در یک رستوران
                var existingUser = _context.Users
                    .FirstOrDefault(u => u.name == name && u.restaurant_id == restaurantId.Value);
                if (existingUser != null)
                {
                    TempData["Error"] = "کاربری با این نام قبلاً ثبت شده است.";
                    return RedirectToAction("AddUser");
                }

                // ساخت یوزر جدید
                var user = new User
                {
                    name = name,
                    password = password, // هش کردن در محیط واقعی
                    role_id = role_id,
                    restaurant_id = restaurantId.Value
                };

                _context.Users.Add(user);
                _context.SaveChanges();

                TempData["Success"] = "کاربر با موفقیت ثبت شد.";
                return RedirectToAction("ManageUsers");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "خطا در سرور: " + ex.Message;
                return RedirectToAction("AddUser");
            }
        }


        [HttpGet]
        public async Task<IActionResult> ManagerReports(
              int statusId = -1,
              string? period = null,
              DateTime? from = null,
              DateTime? to = null,
              int topN = 8)
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null) return RedirectToAction("ChooseRestaurant");

            var statusMap = new Dictionary<int, string>
        {
            {1, "در انتظار ثبت نهایی"},
            {2, "در انتظار تایید"},
            {3, "تایید شده"},
            {4, "در حال آماده‌سازی"},
            {5, "آماده تحویل"},
            {6, "تحویل داده شده"},
            {7, "در انتظار پرداخت"},
            {8, "پرداخت شده"},
            {9, "لغو شده توسط مشتری"},
            {10,"لغو شده توسط رستوران"},
            {11,"بسته شده"},
            {12,"در انتظار اصلاح سفارش"}
        };

            var statusColors = new Dictionary<int, string>
        {
            {1, "#6c757d"},
            {2, "#0dcaf0"},
            {3, "#198754"},
            {4, "#ffc107"},
            {5, "#0d6efd"},
            {6, "#212529"},
            {7, "#dc3545"},
            {8, "#20c997"},
            {9, "#dc3545"},
            {10,"#dc3545"},
            {11,"#6c757d"},
            {12,"#ffc107"}
        };

            var activeStatuses = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 12 };

            // Period calculation
            if (!string.IsNullOrEmpty(period))
            {
                var today = DateTime.Today;
                if (period.Equals("today", StringComparison.OrdinalIgnoreCase))
                {
                    from = today;
                    to = today.AddDays(1).AddTicks(-1);
                }
                else if (period.Equals("week", StringComparison.OrdinalIgnoreCase))
                {
                    from = today.AddDays(-7);
                    to = DateTime.Now;
                }
                else if (period.Equals("month", StringComparison.OrdinalIgnoreCase))
                {
                    from = new DateTime(today.Year, today.Month, 1);
                    to = DateTime.Now;
                }
                else if (period.Equals("quarter", StringComparison.OrdinalIgnoreCase))
                {
                    from = today.AddMonths(-3);
                    to = DateTime.Now;
                }
                else if (period.Equals("year", StringComparison.OrdinalIgnoreCase))
                {
                    from = today.AddYears(-1);
                    to = DateTime.Now;
                }
            }

            if (to.HasValue && to.Value.TimeOfDay == TimeSpan.Zero)
                to = to.Value.Date.AddDays(1).AddTicks(-1);

            var ordersQuery = _context.Orders.Where(o => o.RestaurantId == restaurantId);

            if (statusId > 0)
                ordersQuery = ordersQuery.Where(o => o.StatusId == statusId);
            else if (statusId == 0)
                ordersQuery = ordersQuery.Where(o => activeStatuses.Contains(o.StatusId));

            if (from.HasValue) ordersQuery = ordersQuery.Where(o => o.CreatedAt >= from.Value);
            if (to.HasValue) ordersQuery = ordersQuery.Where(o => o.CreatedAt <= to.Value);

            var totalOrders = await ordersQuery.CountAsync();
            var paidOrders = await ordersQuery.Where(o => o.StatusId == 11).CountAsync();
            var cancelledOrders = await ordersQuery.Where(o => o.StatusId == 9 || o.StatusId == 10).CountAsync();

            var statusGroups = await ordersQuery
                .GroupBy(o => o.StatusId)
                .Select(g => new { StatusId = g.Key, Count = g.Count() })
                .ToListAsync();

            var orderItemsQuery = _context.OrderItems
                .Where(oi => oi.Order.RestaurantId == restaurantId);

            if (statusId > 0)
                orderItemsQuery = orderItemsQuery.Where(oi => oi.Order.StatusId == statusId);
            else if (statusId == 0)
                orderItemsQuery = orderItemsQuery.Where(oi => activeStatuses.Contains(oi.Order.StatusId));

            if (from.HasValue) orderItemsQuery = orderItemsQuery.Where(oi => oi.Order.CreatedAt >= from.Value);
            if (to.HasValue) orderItemsQuery = orderItemsQuery.Where(oi => oi.Order.CreatedAt <= to.Value);

            var totalRevenue = await orderItemsQuery.SumAsync(
    oi => (decimal)oi.Quantity *
    (
        oi.UnitPriceWithDiscount.HasValue &&
        oi.UnitPriceWithDiscount.Value > 0
            ? oi.UnitPriceWithDiscount.Value
            : oi.UnitPrice
    ));
            var paidRevenue = await orderItemsQuery
      .Where(oi => oi.Order.StatusId == 11)
      .SumAsync(oi => (decimal)oi.Quantity *
      (
          oi.UnitPriceWithDiscount.HasValue &&
          oi.UnitPriceWithDiscount.Value > 0
              ? oi.UnitPriceWithDiscount.Value
              : oi.UnitPrice
      ));

            var totalItemsCount = await orderItemsQuery.SumAsync(oi => (int?)oi.Quantity) ?? 0;

            var salesByDay = await orderItemsQuery
                .GroupBy(oi => oi.Order.CreatedAt.Date)
                .Select(g => new SalesPointDto
                {
                    Day = g.Key,
                    Revenue = g.Sum(oi =>
     (decimal)oi.Quantity *
     (
         oi.UnitPriceWithDiscount.HasValue &&
         oi.UnitPriceWithDiscount.Value > 0
             ? oi.UnitPriceWithDiscount.Value
             : oi.UnitPrice
     )),
                    Orders = g.Select(oi => oi.OrderId).Distinct().Count()
                })
                .OrderBy(x => x.Day)
                .ToListAsync();

            var topByQty = await orderItemsQuery
     .GroupBy(oi => oi.FoodItemId)  // فقط بر اساس FoodItemId گروه‌بندی کن
     .Select(g => new TopItemDto
     {
         FoodItemId = g.Key,
         Name = g.FirstOrDefault().FoodName ?? "بدون نام",
         ImageUrl = g.FirstOrDefault().FoodImageUrl ?? "/uploads/food_default.jpg",
         Quantity = g.Sum(x => x.Quantity),
         Revenue = g.Sum(x =>
             (decimal)x.Quantity *
             (
                 x.UnitPriceWithDiscount.HasValue &&
                 x.UnitPriceWithDiscount.Value > 0
                     ? x.UnitPriceWithDiscount.Value
                     : x.UnitPrice
             ))
     })
     .OrderByDescending(x => x.Quantity)
     .Take(topN)
     .ToListAsync();

            var topByRev = await orderItemsQuery
        .GroupBy(oi => oi.FoodItemId)  // فقط بر اساس FoodItemId گروه‌بندی کن
        .Select(g => new TopItemDto
        {
            FoodItemId = g.Key,
            Name = g.FirstOrDefault().FoodName ?? "بدون نام",
            ImageUrl = g.FirstOrDefault().FoodImageUrl ?? "/uploads/food_default.jpg",
            Quantity = g.Sum(x => x.Quantity),
            Revenue = g.Sum(x =>
                (decimal)x.Quantity *
                (
                    x.UnitPriceWithDiscount.HasValue &&
                    x.UnitPriceWithDiscount.Value > 0
                        ? x.UnitPriceWithDiscount.Value
                        : x.UnitPrice
                ))
        })
        .OrderByDescending(x => x.Revenue)
        .Take(topN)
        .ToListAsync();

            var vm = new ManagerReportViewModel
            {
                FromDate = from,
                ToDate = to,
                Period = period,
                TotalOrders = totalOrders,
                PaidOrders = paidOrders,
                CancelledOrders = cancelledOrders,
                TotalRevenue = totalRevenue,
                PaidRevenue = paidRevenue,
                AvgOrderValue = paidOrders > 0 ? Math.Round(paidRevenue / paidOrders, 0) : 0,
                AvgItemsPerOrder = totalOrders > 0 ? Math.Round((double)totalItemsCount / totalOrders, 2) : 0,
                CancelRate = totalOrders > 0 ? Math.Round((double)cancelledOrders * 100 / totalOrders, 2) : 0,
                PaidConversionRate = totalOrders > 0 ? Math.Round((double)paidOrders * 100 / totalOrders, 2) : 0,
                StatusMap = statusMap,
                StatusColors = statusColors,
                SalesByDay = salesByDay,
                TopItemsByQuantity = topByQty,
                TopItemsByRevenue = topByRev,
                TopN = topN
            };



            foreach (var sg in statusGroups)
                vm.StatusCounts[sg.StatusId] = sg.Count;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_ManagerReportsPartial", vm);

            return View("ManagerReports", vm);
        }



        [HttpGet("ExportOrdersToExcel")]
        public IActionResult ExportOrdersToExcel(
    int statusId = -1,
    string? period = null,
    DateTime? from = null,
    DateTime? to = null)
        {
            try
            {
                int? restaurantId = User.GetRestaurantId();
                if (restaurantId == null)
                    return BadRequest("شناسه رستوران مشخص نیست.");

                var today = DateTime.Today;

                if (!string.IsNullOrEmpty(period))
                {
                    if (period.Equals("today", StringComparison.OrdinalIgnoreCase))
                    {
                        from = today;
                        to = today.AddDays(1).AddTicks(-1);
                    }
                    else if (period.Equals("week", StringComparison.OrdinalIgnoreCase))
                    {
                        from = today.AddDays(-7);
                        to = DateTime.Now;
                    }
                    else if (period.Equals("month", StringComparison.OrdinalIgnoreCase))
                    {
                        from = new DateTime(today.Year, today.Month, 1);
                        to = DateTime.Now;
                    }
                    else if (period.Equals("quarter", StringComparison.OrdinalIgnoreCase))
                    {
                        from = today.AddMonths(-3);
                        to = DateTime.Now;
                    }
                    else if (period.Equals("year", StringComparison.OrdinalIgnoreCase))
                    {
                        from = today.AddYears(-1);
                        to = DateTime.Now;
                    }
                }

                if (!from.HasValue || !to.HasValue)
                {
                    from = today.AddDays(-30);
                    to = DateTime.Now;
                }

                if (to.Value.TimeOfDay == TimeSpan.Zero)
                    to = to.Value.Date.AddDays(1).AddTicks(-1);

                var fromDate = from.Value;
                var toDate = to.Value;

                // ✅ اضافه کردن Include برای Customer
                var ordersQuery = _context.Orders
                    .Include(o => o.OrderItems)
                    .Include(o => o.Customer)   // <-- این خط اضافه شده
                    .Where(o => o.RestaurantId == restaurantId)
                    .Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate);

                if (statusId > 0)
                    ordersQuery = ordersQuery.Where(o => o.StatusId == statusId);

                var orders = ordersQuery.OrderByDescending(o => o.CreatedAt).ToList();

                if (!orders.Any())
                    return BadRequest("هیچ سفارشی در این بازه زمانی یافت نشد.");

                using (var workbook = new XLWorkbook())
                {
                    // === Sheet 1: Orders Summary ===
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
                        var totalPrice = o.OrderItems.Sum(i => GetFinalPrice(i) * i.Quantity);
                        wsOrders.Cell(row, 1).Value = o.OrderId;
                        wsOrders.Cell(row, 2).Value = DateHelper.ToShamsi(o.CreatedAt);
                        wsOrders.Cell(row, 3).Value = o.TableNumber;
                        wsOrders.Cell(row, 4).Value = GetStatusName(o.StatusId);

                        wsOrders.Cell(row, 5).Value = o.Customer?.FullName ?? "مشتری مهمان";
                        string mobile = o.Customer?.Mobile ?? "-";
                        if (!string.IsNullOrEmpty(mobile) && mobile.StartsWith("991"))
                            mobile = "-";
                        wsOrders.Cell(row, 6).Value = mobile;
                        wsOrders.Cell(row, 7).Value = o.Description ?? "-";
                        wsOrders.Cell(row, 8).Value = o.OrderItems.Count;
                        wsOrders.Cell(row, 9).Value = totalPrice;
                        row++;
                    }

                    // تنظیم استایل هدر (با توجه به تعداد ستون‌های جدید)
                    var headerRange1 = wsOrders.Range($"A1:I1");
                    headerRange1.Style.Font.Bold = true;
                    headerRange1.Style.Fill.BackgroundColor = XLColor.LightGray;
                    wsOrders.Columns().AdjustToContents();

                    // === Sheet 2: Items Details (بدون تغییر) ===
                    var wsItems = workbook.Worksheets.Add("جزئیات سفارش‌ها");
                    wsItems.Cell(1, 1).Value = "شناسه سفارش";
                    wsItems.Cell(1, 2).Value = "شناسه آیتم";
                    wsItems.Cell(1, 3).Value = "نام غذا";
                    wsItems.Cell(1, 4).Value = "تعداد";
                    wsItems.Cell(1, 5).Value = "قیمت واحد";
                    wsItems.Cell(1, 6).Value = "قیمت با تخفیف";
                    wsItems.Cell(1, 7).Value = "مبلغ کل";
                    int itemRow = 2;

                    foreach (var o in orders)
                    {
                        foreach (var i in o.OrderItems)
                        {
                            var total = GetFinalPrice(i) * i.Quantity;
                            wsItems.Cell(itemRow, 1).Value = o.OrderId;
                            wsItems.Cell(itemRow, 2).Value = i.OrderItemId;
                            wsItems.Cell(itemRow, 3).Value = i.FoodName ?? "-";
                            wsItems.Cell(itemRow, 4).Value = i.Quantity;
                            wsItems.Cell(itemRow, 5).Value = i.UnitPrice;
                            wsItems.Cell(itemRow, 6).Value = GetFinalPrice(i);
                            wsItems.Cell(itemRow, 7).Value = total;
                            itemRow++;
                        }
                    }

                    var headerRange2 = wsItems.Range("A1:G1");
                    headerRange2.Style.Font.Bold = true;
                    headerRange2.Style.Fill.BackgroundColor = XLColor.LightGray;
                    wsItems.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        string fileName = $"OrdersReport_{restaurantId}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"خطا در تولید گزارش: {ex.Message}");
            }
        }

        private decimal GetFinalPrice(OrderItem item)
        {
            return (item.UnitPriceWithDiscount.HasValue &&
                    item.UnitPriceWithDiscount.Value > 0)
                ? item.UnitPriceWithDiscount.Value
                : item.UnitPrice;
        }




        private string GetStatusName(int statusId)
        {
            return statusId switch
            {
                1 => "در انتظار ثبت نهایی",
                2 => "در انتظار تایید",
                3 => "تایید شده",
                4 => "در حال آماده‌سازی",
                5 => "آماده تحویل",
                6 => "تحویل داده شده",
                7 => "در انتظار پرداخت",
                8 => "پرداخت شده",
                9 => "لغو شده توسط مشتری",
                10 => "لغو شده توسط رستوران",
                11 => "بسته شده",
                12 => "در انتظار اصلاح سفارش",
                _ => "-"
            };
        }


        public async Task<IActionResult> FoodList()
        {
            // گرفتن آیدی رستوران از سشن
            int? restaurantId = User.GetRestaurantId();
            var restaurant = _context.Restaurants
               .FirstOrDefault(r => r.restaurant_id == restaurantId);
            if (restaurantId == null)
            {
                return RedirectToAction("ChooseRestaurant");
            }

            var items = await _context.FoodItems
                       .Where(f => f.RestaurantId == restaurantId && f.IsActive) // فقط غذاهای فعال

                .Include(f => f.Category) // ✅ اضافه شد برای دسترسی به نام دسته‌بندی
                .Select(f => new FoodItemViewModel
                {
                    FoodItemId = f.FoodItemId,
                    RestaurantId = f.RestaurantId,
                    Name = f.Name ?? "",
                    Description = f.Description ?? "",
                    ImageUrl = f.ImageUrl ?? "",
                    CategoryId = f.CategoryId,
                    CategoryName = f.Category != null ? f.Category.CategoryName : "",
                    Price = f.Price,
                    DiscountPrice = f.DiscountPrice ?? 0,
                    CostPrice = f.CostPrice ?? 0,
                    IsAvailable = f.IsAvailable,
                    CreatedAt = f.CreatedAt.HasValue
                        ? f.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm")
                        : ""
                })
                .ToListAsync();

            ViewBag.RestaurantId = restaurantId.Value;
            ViewBag.RestaurantName = restaurant?.name;
            return View(items);
        }


        public async Task<IActionResult> AddOrder()
        {
            // گرفتن آیدی رستوران از سشن
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
            {
                return RedirectToAction("ChooseRestaurant");
            }

            var items = await _context.FoodItems
                       .Where(f => f.RestaurantId == restaurantId && f.IsActive) // فقط غذاهای فعال

                .Include(f => f.Category)
                .Select(f => new FoodItemViewModel
                {
                    FoodItemId = f.FoodItemId,
                    RestaurantId = f.RestaurantId,
                    Name = f.Name ?? "",
                    Description = f.Description ?? "",
                    ImageUrl = f.ImageUrl ?? "",
                    CategoryId = f.CategoryId,
                    CategoryName = f.Category != null ? f.Category.CategoryName : "", // ✅ مقداردهی نام دسته‌بندی
                    Price = f.Price,
                    DiscountPrice = f.DiscountPrice ?? 0,
                    CostPrice = f.CostPrice ?? 0,
                    IsAvailable = f.IsAvailable,
                    CreatedAt = f.CreatedAt.HasValue
                        ? f.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm")
                        : ""
                })
                .ToListAsync();

            ViewBag.RestaurantId = restaurantId.Value;

            return View(items);
        }


        public async Task<IActionResult> CustomersList()
        {
            // دریافت شناسه رستوران از کاربر جاری (از طریق Claim یا Session)
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
            {
                return RedirectToAction("ChooseRestaurant");
            }

            // واکشی مشتریان این رستوران به همراه آدرس‌ها (اختیاری)
            var customers = await _context.Customers
                .Where(c => c.RestaurantId == restaurantId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CustomerListViewModel
                {
                    CustomerId = c.CustomerId,
                    FullName = c.FullName,
                    Mobile = c.Mobile,
                    IsActive = c.IsActive,
                    CreatedAtShamsi = DateHelper.ToShamsi(c.CreatedAt), // اگر متد تبدیل تاریخ دارید
                    AddressCount = _context.CustomerAddresses.Count(a => a.CustomerId == c.CustomerId)
                })
                .ToListAsync();

            ViewBag.RestaurantId = restaurantId.Value;
            return View(customers);
        }




        [HttpGet]
        public async Task<IActionResult> ManagerOrderList(
    int page = 1,
    int pageSize = 20,
    int statusId = 0,
    string? period = null,
    string? search = null,
    DateTime? from = null,
    DateTime? to = null
     )

        {
            int? restaurantId = User.GetRestaurantId();

            if (restaurantId == null)
            {
                return RedirectToAction("ChooseRestaurant");
            }

            // ================ اضافه کردن نام رستوران =================
            var restaurant = await _context.Restaurants
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId.Value);

            if (restaurant == null)
            {
                ViewBag.Error = "رستوران یافت نشد.";
                return RedirectToAction("ChooseRestaurant");
            }

            ViewBag.RestaurantName = restaurant.name;


            var statusMap = new Dictionary<int, string>
    {
        {1, "در انتظار ثبت نهایی"},
        {2, "در انتظار تایید"},
        {3, "تایید شده"},
        {4, "در حال آماده‌سازی"},
        {5, "آماده تحویل"},
        {6, "تحویل داده شده"},
        {7, "در انتظار پرداخت"},
        {8, "پرداخت شده"},
        {9, "لغو شده توسط مشتری"},
        {10, "لغو شده توسط رستوران"},
        {11, "بسته شده"},
        {12, "در انتظار اصلاح سفارش"}
    };

            var statusColors = new Dictionary<int, string>
    {
        {1, "secondary"},
        {2, "info"},
        {3, "success"},
        {4, "warning"},
        {5, "primary"},
        {6, "dark"},
        {7, "danger"},
        {8, "success"},
        {9, "danger"},
        {10, "danger"},
        {11, "secondary"},
        {12, "warning"}
    };

            var activeStatuses = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

            var query = _context.Orders
                .AsQueryable()
                .Where(o => o.RestaurantId == restaurantId);

            if (statusId > 0)
            {
                query = query.Where(o => o.StatusId == statusId);
            }
            else if (statusId == -1)
            {
                // همه وضعیت‌ها بدون فیلتر
            }
            else
            {
                query = query.Where(o => activeStatuses.Contains(o.StatusId));
            }

            if (!string.IsNullOrEmpty(period))
            {
                var today = DateTime.Today;
                if (period.Equals("today", StringComparison.OrdinalIgnoreCase))
                {
                    from = today;
                    to = today.AddDays(1).AddTicks(-1);
                }
                else if (period.Equals("week", StringComparison.OrdinalIgnoreCase))
                {
                    from = today.AddDays(-7);
                    to = DateTime.Now;
                }
                else if (period.Equals("month", StringComparison.OrdinalIgnoreCase))
                {
                    from = new DateTime(today.Year, today.Month, 1);
                    to = DateTime.Now;
                }
                else if (period.Equals("quarter", StringComparison.OrdinalIgnoreCase))
                {
                    from = today.AddMonths(-3);
                    to = DateTime.Now;
                }
                else if (period.Equals("year", StringComparison.OrdinalIgnoreCase))
                {
                    from = today.AddYears(-1);
                    to = DateTime.Now;
                }
            }

            if (from.HasValue)
                query = query.Where(o => o.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(o => o.CreatedAt <= to.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                if (int.TryParse(search, out int numeric))
                {
                    query = query.Where(o => o.OrderId == numeric);
                }
                else
                {
                    query = query.Where(o => o.Description != null && o.Description.Contains(search));
                }
            }

            var totalItems = await query.CountAsync();

            var orders = await query
                 .Include(o => o.Customer)
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderDto
                {
                    OrderId = o.OrderId,
                    TableNumber = o.TableNumber,
                    StatusId = o.StatusId,
                    CreatedAt = o.CreatedAt,
                    UpdatedAt = o.UpdatedAt,
                    Description = o.Description,
                    CustomerId = o.CustomerId,
                    CustomerFullName = o.Customer != null ? o.Customer.FullName : null,
                    CustomerMobile = o.Customer != null ? o.Customer.Mobile : null,
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

            var vm = new OrderListViewModel
            {
                Orders = orders,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                FilterStatusId = statusId > 0 ? statusId : (int?)null,
                FromDate = from,
                ToDate = to,
                Search = search,

                StatusMap = statusMap,
                StatusColors = statusColors,
            };

            // ارسال period به ViewData برای فعال‌سازی دکمه‌ها
            ViewData["CurrentPeriod"] = period?.ToLower();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_ManagerOrdersPartial", vm);




            return View("ManagerOrderList", vm);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, int newStatusId)
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
            {
                return RedirectToAction("ChooseRestaurant");
            }
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && o.RestaurantId == restaurantId);
            if (order == null) return Json(new { success = false, message = "سفارش پیدا نشد." });

            order.StatusId = newStatusId;
            order.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "وضعیت با موفقیت به‌روز شد.", newStatusId });
        }

        [HttpGet]
        public async Task<IActionResult> CashierDashboard(

       int page = 1,
       int pageSize = 20,
       string? period = "month",
       string? search = null,
       DateTime? from = null,
       DateTime? to = null)
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
            {
                return RedirectToAction("ChooseRestaurant");
            }

            var statusMap = new Dictionary<int, string>
    {
        {6, "تحویل داده شده"},
        {7, "در انتظار پرداخت"},
        {8, "پرداخت شده"},
        {11, "بسته شده"},

    };

            var statusColors = new Dictionary<int, string>
    {
        {6, "dark"},
        {7, "danger"},
        {8, "success"},
        {9, "danger"},
        {10, "danger"}
    };

            var cashierStatuses = new[] { 6, 7, 8, 11 };

            var query = _context.Orders
                .Where(o => o.RestaurantId == restaurantId && cashierStatuses.Contains(o.StatusId));

            // فیلتر بر اساس بازه زمانی
            if (!string.IsNullOrEmpty(period))
            {
                var today = DateTime.Today;
                if (period.Equals("today", StringComparison.OrdinalIgnoreCase))
                {
                    from = today;
                    to = today.AddDays(1).AddTicks(-1);
                }
                else if (period.Equals("week", StringComparison.OrdinalIgnoreCase))
                {
                    from = today.AddDays(-7);
                    to = DateTime.Now;
                }
                else if (period.Equals("month", StringComparison.OrdinalIgnoreCase))
                {
                    from = new DateTime(today.Year, today.Month, 1);
                    to = DateTime.Now;
                }
            }

            if (from.HasValue) query = query.Where(o => o.CreatedAt >= from.Value);
            if (to.HasValue) query = query.Where(o => o.CreatedAt <= to.Value);

            // فیلتر سرچ
            if (!string.IsNullOrWhiteSpace(search))
            {
                if (int.TryParse(search, out int num))
                    query = query.Where(o => o.OrderId == num);
                else
                    query = query.Where(o => o.Description != null && o.Description.Contains(search));
            }

            var totalItems = await query.CountAsync();

            var orders = await query
     .OrderByDescending(o => o.CreatedAt)
     .Skip((page - 1) * pageSize)
     .Take(pageSize)
     .Select(o => new OrderDto
     {
         OrderId = o.OrderId,
         TableNumber = o.TableNumber,
         StatusId = o.StatusId,
         CreatedAt = o.CreatedAt,
         Description = o.Description,
         OrderItems = o.OrderItems.Select(oi => new OrderItemDto
         {
             OrderItemId = oi.OrderItemId,
             Quantity = oi.Quantity,
             UnitPrice = oi.UnitPrice,
             FoodName = oi.FoodName,
             FoodImageUrl = oi.FoodImageUrl
         }).ToList()
     })
     .ToListAsync();

            // محاسبه مبلغ کل هر سفارش بدون تغییر DTO
            var orderTotals = orders.ToDictionary(
                o => o.OrderId,
                o => o.OrderItems.Sum(oi => oi.UnitPrice * oi.Quantity)
            );

            var vm = new OrderListViewModel
            {
                Orders = orders,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                FromDate = from,
                ToDate = to,
                Search = search,
                StatusMap = statusMap,
                StatusColors = statusColors,

                // مبلغ‌ها رو می‌فرستیم به View از طریق ViewBag
            };

            ViewBag.OrderTotals = orderTotals;

            return View("CashierDashboard", vm);

        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetFilteredCashierOrders(OrderFilterModel filter)
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
            {
                return RedirectToAction("ChooseRestaurant");
            }
            var cashierStatuses = new[] { 6, 7, 8, 11 };

            var query = _context.Orders
                .Where(o => o.RestaurantId == restaurantId && cashierStatuses.Contains(o.StatusId));

            if (filter.Period != null)
            {
                var today = DateTime.Today;
                if (filter.Period.Equals("today", StringComparison.OrdinalIgnoreCase))
                {
                    filter.From = today;
                    filter.To = today.AddDays(1).AddTicks(-1);
                }
                else if (filter.Period.Equals("week", StringComparison.OrdinalIgnoreCase))
                {
                    filter.From = today.AddDays(-7);
                    filter.To = DateTime.Now;
                }
                else if (filter.Period.Equals("month", StringComparison.OrdinalIgnoreCase))
                {
                    filter.From = new DateTime(today.Year, today.Month, 1);
                    filter.To = DateTime.Now;
                }
            }

            if (filter.From.HasValue) query = query.Where(o => o.CreatedAt >= filter.From.Value);
            if (filter.To.HasValue) query = query.Where(o => o.CreatedAt <= filter.To.Value);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                if (int.TryParse(filter.Search, out int num))
                    query = query.Where(o => o.OrderId == num);
                else
                    query = query.Where(o => o.Description != null && o.Description.Contains(filter.Search));
            }

            if (filter.StatusId.HasValue && filter.StatusId != 0)
            {
                if (filter.StatusId == -1)
                {
                    // همه وضعیت‌ها
                }
                else
                {
                    query = query.Where(o => o.StatusId == filter.StatusId);
                }
            }

            int page = filter.Page ?? 1;
            int pageSize = filter.PageSize ?? 20;

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderDto
                {
                    OrderId = o.OrderId,
                    TableNumber = o.TableNumber,
                    StatusId = o.StatusId,
                    CreatedAt = o.CreatedAt,
                    Description = o.Description,
                    OrderItems = o.OrderItems.Select(oi => new OrderItemDto
                    {
                        OrderItemId = oi.OrderItemId,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        FoodName = oi.FoodName,
                        FoodImageUrl = oi.FoodImageUrl
                    }).ToList()
                })
                .ToListAsync();

            var vm = new OrderListViewModel
            {
                Orders = orders,
                Page = page,
                PageSize = pageSize,
                TotalItems = await query.CountAsync(),
                FromDate = filter.From,
                ToDate = filter.To,
                Search = filter.Search,
                FilterStatusId = filter.StatusId,
                StatusMap = new Dictionary<int, string> {
            {6, "تحویل داده شده"},
            {7, "در انتظار پرداخت"},
            {8, "پرداخت شده"},
            {11, "بسته شده"},
        }
            };

            return PartialView("_CashierOrdersPartial", vm);
        }


        // GET: /Home/CategoryList
        public async Task<IActionResult> CategoryList()
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return RedirectToAction("ChooseRestaurant");

            var categories = await _context.Categories
                .Where(c => c.RestaurantId == restaurantId)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.CreatedAt)
                .Select(c => new CategoryViewModel
                {
                    CategoryId = c.CategoryId,
                    CategoryName = c.CategoryName,
                    CreatedAt = c.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    DisplayOrder = c.DisplayOrder
                })
                .ToListAsync();

            ViewBag.RestaurantId = restaurantId.Value;
            return View(categories);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCategoriesOrder([FromBody] List<CategoryOrderDto> items)
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return Unauthorized();

            if (items == null || !items.Any())
                return BadRequest();

            foreach (var item in items)
            {
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.CategoryId == item.CategoryId && c.RestaurantId == restaurantId);
                if (category != null)
                {
                    category.DisplayOrder = item.DisplayOrder;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // DTO کمکی
        public class CategoryOrderDto
        {
            public int CategoryId { get; set; }
            public int DisplayOrder { get; set; }
        }




        public async Task<IActionResult> TableList()
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return RedirectToAction("ChooseRestaurant");

            var tables = await _context.RestaurantTables
                .Where(t => t.RestaurantId == restaurantId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TableViewModel
                {
                    TableId = t.TableId,
                    RestaurantId = t.RestaurantId,
                    TableName = t.TableName,
                    Seats = t.Seats,
                    CreatedAt = t.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                })
                .ToListAsync();

            ViewBag.RestaurantId = restaurantId.Value;
            return View(tables);
        }
        public async Task<IActionResult> RestaurantSubscription()
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return RedirectToAction("ChooseRestaurant");



            ViewBag.RestaurantId = restaurantId.Value;
            return View();
        }
        public async Task<IActionResult> Support()
        {
            int? restaurantId = User.GetRestaurantId();
            var restaurant = _context.Restaurants.Find(restaurantId.Value);
            ViewBag.RestaurantName = restaurant?.name ?? "";



            if (restaurantId == null)
                return RedirectToAction("ChooseRestaurant");


            ViewBag.RestaurantName = restaurant?.name;
            ViewBag.RestaurantId = restaurantId.Value;
            return View();







        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }


        [HttpPost]
        public async Task<IActionResult> OtpRequest(string phone)
        {
            // ۱. تمیز کردن شماره تلفن
            phone = phone?.Trim().Replace(" ", "");

            //// ۲. اعتبارسنجی سمت سرور
            if (string.IsNullOrWhiteSpace(phone) || !System.Text.RegularExpressions.Regex.IsMatch(phone, @"^09\d{9}$"))
            {
                return Json(new { success = false, message = "شماره موبایل نامعتبر است." });
            }

            // ۳. منطق استاتیک (فعلاً فرض می‌کنیم هر شماره‌ای معتبر است)
            // در اینجا باید کد OTP تولید شده و در دیتابیس یا Redis ذخیره شود
            // var generatedOtp = new Random().Next(1000, 9999).ToString(); 

            // شبیه‌سازی تاخیر ارسال پیامک
            await Task.Delay(500);

            return Json(new { success = true, message = "کد تایید برای شما ارسال شد." });
        }






        [HttpPost]
        public async Task<IActionResult> OtpVerify(string phone, string otpCode)
        {
            phone = phone?.Trim().Replace(" ", "");

            // ۱. اعتبارسنجی ورودی‌ها
            //if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(otpCode))
            //{
            //    return Json(new { success = false, message = "اطلاعات ناقص است." });
            //}

            // ۲. منطق استاتیک (فعلاً فرض می‌کنیم کد ۱۲۳۴ همیشه صحیح است)
            if (otpCode != "1234")
            {
                return Json(new { success = false, message = "کد وارد شده صحیح نیست." });
            }

            // ۳. پیدا کردن کاربر (شبیه‌سازی شده - در مرحله بعد از دیتابیس بخوانید)
            // فعلاً یک کاربر ساختگی می‌سازیم اگر در دیتابیس نباشد (فقط برای تست لاگین)
            // در واقعیت باید از _context.Owners استفاده کنید

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, "مدیر تست"), // نام را از دیتابیس بخوانید
        new Claim("OwnerId", "1"), // آیدی را از دیتابیس بخوانید
        new Claim(ClaimTypes.Role, "Owner")
    };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return Json(new { success = true, redirectUrl = Url.Action("ChooseRestaurant", "Home") });
        }









        [Authorize(Roles = "Owner")]
        [HttpPost("/zarinpal/create")]
        public async Task<IActionResult> CreateZarinpalPayment([FromBody] CreateZarinpalPaymentRequest request)
        {
            try
            {
                // --- 1) اعتبارسنجی ورودی ---
                if (request == null)
                    return BadRequest(new { success = false, message = "درخواست نامعتبر است." });
                if (request.RestaurantId <= 0 || request.SubscriptionPlanId <= 0)
                    return BadRequest(new { success = false, message = "پارامترهای ورودی معتبر نیست." });

                // --- 2) دریافت OwnerId از Claims ---
                var ownerIdClaim = User.FindFirstValue("OwnerId");
                if (string.IsNullOrWhiteSpace(ownerIdClaim) || !int.TryParse(ownerIdClaim, out var ownerId))
                    return Unauthorized(new { success = false, message = "احراز هویت نامعتبر است." });

                // --- 3) بررسی مالکیت رستوران ---
                var restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(r => r.restaurant_id == request.RestaurantId && r.owner_id == ownerId);
                if (restaurant == null)
                    return BadRequest(new { success = false, message = "شما به این رستوران دسترسی ندارید." });

                // --- 4) دریافت پلن اشتراک ---
                var plan = await _context.SubscriptionPlans
                    .FirstOrDefaultAsync(p => p.Id == request.SubscriptionPlanId && p.IsActive);
                if (plan == null)
                    return BadRequest(new { success = false, message = "پلن اشتراک یافت نشد." });

                // --- 5) نرمال‌سازی دوره ---
                var period = NormalizePeriod(request.SubscriptionPeriod);

                // --- 6) محاسبه مبلغ پایه (قبل از تخفیف کد) ---
                (decimal standardPrice, decimal baseAmount) = CalculatePlanAmount(plan, period);
                if (baseAmount <= 0)
                    return BadRequest(new { success = false, message = "مبلغ پلن معتبر نیست." });

                decimal finalAmount = baseAmount;
                decimal discountApplied = 0;
                int? couponId = null;
                var now = DateTime.Now;
                // ========== اعمال کد تخفیف (با استفاده از دیتابیس) ==========
                if (!string.IsNullOrWhiteSpace(request.DiscountCode))
                {
                    var code = request.DiscountCode.Trim().ToUpper();
                    var coupon = await _context.Coupons
                        .FirstOrDefaultAsync(c => c.Code == code && c.IsActive);

                    if (coupon == null)
                        return BadRequest(new { success = false, message = "کد تخفیف نامعتبر است." });

                    // اعتبارسنجی تاریخ

                    if (now < coupon.StartDate || now > coupon.EndDate)
                        return BadRequest(new { success = false, message = "کد تخفیف منقضی شده یا هنوز فعال نشده است." });

                    // بررسی محدودیت کلی تعداد استفاده
                    if (coupon.UsageLimit.HasValue && coupon.UsedCount >= coupon.UsageLimit)
                        return BadRequest(new { success = false, message = "تعداد استفاده از این کد به پایان رسیده است." });

                    // بررسی محدودیت برای مالک (LimitPerOwner)
                    if (coupon.LimitPerOwner > 0)
                    {
                        var ownerUsageCount = await _context.CouponUsages
                            .Where(u => u.CouponId == coupon.Id && u.OwnerId == ownerId && u.Status == "Success")
                            .CountAsync();

                        if (ownerUsageCount >= coupon.LimitPerOwner)
                            return BadRequest(new { success = false, message = "شما قبلاً از این کد تخفیف استفاده کرده‌اید." });
                    }

                    // بررسی مالک خاص
                    if (coupon.SpecificOwnerId.HasValue && coupon.SpecificOwnerId != ownerId)
                        return BadRequest(new { success = false, message = "این کد تخفیف مخصوص شما نیست." });

                    // بررسی رستوران خاص
                    if (coupon.SpecificRestaurantId.HasValue && coupon.SpecificRestaurantId != request.RestaurantId)
                        return BadRequest(new { success = false, message = "این کد تخفیف برای این رستوران معتبر نیست." });

                    // بررسی حداقل مبلغ خرید
                    if (coupon.MinPurchaseAmount.HasValue && baseAmount < coupon.MinPurchaseAmount)
                        return BadRequest(new { success = false, message = $"حداقل مبلغ برای استفاده از این کد {coupon.MinPurchaseAmount:N0} تومان است." });

                    // محاسبه تخفیف
                    decimal discountValue = 0;
                    if (coupon.DiscountType == "Percentage")
                    {
                        discountValue = baseAmount * (coupon.DiscountValue / 100);
                        if (coupon.MaxDiscountAmount.HasValue)
                            discountValue = Math.Min(discountValue, coupon.MaxDiscountAmount.Value);
                    }
                    else // FixedAmount
                    {
                        discountValue = Math.Min(coupon.DiscountValue, baseAmount);
                    }

                    discountApplied = Math.Round(discountValue, 0);
                    finalAmount = baseAmount - discountApplied;
                    if (finalAmount < 0) finalAmount = 0;

                    couponId = coupon.Id;
                }
                else
                {
                    // اگر تخفیف دوره‌ای وجود دارد (مثل تخفیف ۳ و ۶ ماهه)
                    discountApplied = Math.Max(0, standardPrice - baseAmount);
                }

                // (اختیاری) اگر کلاینت FinalPrice ارسال کرده، با مقدار محاسبه‌شده مقایسه کنید
                if (request.FinalPrice.HasValue && Math.Abs(request.FinalPrice.Value - finalAmount) > 1)
                {
                    // می‌توانید خطا دهید یا نادیده بگیرید - برای امنیت مقدار سرور را نگه دارید
                }

                // --- 7) حذف پرداخت‌های Pending قبلی ---
                var pendings = await _context.Subscriptions
                    .Where(s => s.RestaurantId == request.RestaurantId &&
                                s.OwnerId == ownerId &&
                                s.Status == "PendingPayment" &&
                                s.PaymentMethod == "Zarinpal")
                    .ToListAsync();
                if (pendings.Any())
                {
                    _context.Subscriptions.RemoveRange(pendings);
                    await _context.SaveChangesAsync();
                }

                // --- 8) ارسال درخواست به زرین‌پال ---
                var merchantId = _configuration["Zarinpal:MerchantId"];
                var callbackUrl = _configuration["Zarinpal:CallbackUrl"];

                if (string.IsNullOrEmpty(merchantId) || string.IsNullOrEmpty(callbackUrl))
                    return BadRequest(new { success = false, message = "پیکربندی درگاه پرداخت ناقص است." });

                var owner = await _context.Owners.FindAsync(ownerId);
                using var client = new HttpClient();

                var zarinReq = new
                {
                    merchant_id = merchantId,
                    amount = (long)finalAmount,
                    currency = "IRT",
                    description = $"خرید اشتراک {plan.Name} - {period}" +
                                  (discountApplied > 0 ? $" (تخفیف: {discountApplied} تومان)" : ""),
                    callback_url = callbackUrl,
                    metadata = new { mobile = owner?.Phone?.Trim() ?? "", auto_verify = false }
                };

                var zarinResponse = await client.PostAsJsonAsync("https://payment.zarinpal.com/pg/v4/payment/request.json", zarinReq);
                var rawResponse = await zarinResponse.Content.ReadAsStringAsync();
                dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(rawResponse);

                if (json?.data?.code != 100 || string.IsNullOrWhiteSpace(json?.data?.authority?.ToString()))
                    return BadRequest(new { success = false, message = "خطا در ارتباط با زرین‌پال." });

                string authority = json.data.authority.ToString();

                // --- 9) ثبت رکورد اشتراک Pending ---

                var endDate = CalculateEndDate(now, period);

                var subscription = new Subscription
                {
                    RestaurantId = request.RestaurantId,
                    OwnerId = ownerId,
                    SubscriptionPlanId = request.SubscriptionPlanId,
                    SubscriptionPeriod = period,
                    Status = "PendingPayment",
                    StartDate = now,
                    EndDate = endDate,
                    PurchaseDate = now,
                    PricePaid = finalAmount,
                    DiscountApplied = discountApplied,
                    CouponId = couponId,  // ذخیره آی‌دی کوپن (حتی اگر null باشد)
                    PaymentMethod = "Zarinpal",
                    TransactionId = authority,
                    IsPaid = false,
                    CafeBazarPurchaseToken = "Zarinpal" + authority,
                    CafeBazarOrderId = "Zarinpal" + authority,
                    AutoRenew = false,
                    NextRenewalDate = endDate,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.Subscriptions.Add(subscription);
                await _context.SaveChangesAsync();

                // --- 10) بازگرداندن لینک پرداخت ---
                return Ok(new
                {
                    success = true,
                    url = $"https://payment.zarinpal.com/pg/StartPay/{authority}",
                    subscriptionId = subscription.Id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در ایجاد پرداخت زرین‌پال.", detail = ex.Message });
            }
        }



        [HttpPost("/coupon/validate")]
        public async Task<IActionResult> ValidateCoupon([FromBody] ValidateCouponRequest request)
        {
            try
            {
                // اعتبارسنجی ورودی
                if (request == null || string.IsNullOrWhiteSpace(request.Code))
                    return Ok(new { success = false, message = "کد تخفیف وارد نشده است." });

                if (request.PlanId <= 0 || request.BaseAmount <= 0)
                    return Ok(new { success = false, message = "اطلاعات اشتراک معتبر نیست." });

                // دریافت OwnerId از Claims
                var ownerIdClaim = User.FindFirstValue("OwnerId");
                if (string.IsNullOrWhiteSpace(ownerIdClaim) || !int.TryParse(ownerIdClaim, out var ownerId))
                    return Ok(new { success = false, message = "احراز هویت نامعتبر است." });

                // بررسی وجود رستوران و دسترسی مالک
                var restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(r => r.restaurant_id == request.RestaurantId && r.owner_id == ownerId);
                if (restaurant == null)
                    return Ok(new { success = false, message = "شما به این رستوران دسترسی ندارید." });

                // پیدا کردن کوپن
                var coupon = await _context.Coupons
                    .FirstOrDefaultAsync(c => c.Code == request.Code.ToUpper() && c.IsActive);

                if (coupon == null)
                    return Ok(new { success = false, message = "کد تخفیف نامعتبر است." });

                // بررسی تاریخ اعتبار
                var now = DateTime.Now;
                if (now < coupon.StartDate || now > coupon.EndDate)
                    return Ok(new { success = false, message = "کد تخفیف منقضی شده یا هنوز فعال نشده است." });

                // بررسی محدودیت کلی تعداد استفاده
                if (coupon.UsageLimit.HasValue && coupon.UsedCount >= coupon.UsageLimit)
                    return Ok(new { success = false, message = "تعداد استفاده از این کد به پایان رسیده است." });

                // بررسی محدودیت برای مالک (LimitPerOwner)
                if (coupon.LimitPerOwner > 0)
                {
                    var ownerUsageCount = await _context.CouponUsages
                        .Where(u => u.CouponId == coupon.Id && u.OwnerId == ownerId && u.Status == "Success")
                        .CountAsync();

                    if (ownerUsageCount >= coupon.LimitPerOwner)
                        return Ok(new { success = false, message = "شما قبلاً از این کد تخفیف استفاده کرده‌اید." });
                }

                // بررسی مالک خاص
                if (coupon.SpecificOwnerId.HasValue && coupon.SpecificOwnerId != ownerId)
                    return Ok(new { success = false, message = "این کد تخفیف مخصوص شما نیست." });

                // بررسی رستوران خاص
                if (coupon.SpecificRestaurantId.HasValue && coupon.SpecificRestaurantId != request.RestaurantId)
                    return Ok(new { success = false, message = "این کد تخفیف برای این رستوران معتبر نیست." });

                // بررسی حداقل مبلغ خرید
                if (coupon.MinPurchaseAmount.HasValue && request.BaseAmount < coupon.MinPurchaseAmount)
                    return Ok(new { success = false, message = $"حداقل مبلغ برای استفاده از این کد {coupon.MinPurchaseAmount:N0} تومان است." });

                // محاسبه مبلغ تخفیف
                decimal discountAmount = 0;
                if (coupon.DiscountType == "Percentage")
                {
                    discountAmount = request.BaseAmount * (coupon.DiscountValue / 100);
                    if (coupon.MaxDiscountAmount.HasValue)
                        discountAmount = Math.Min(discountAmount, coupon.MaxDiscountAmount.Value);
                }
                else // FixedAmount
                {
                    discountAmount = Math.Min(coupon.DiscountValue, request.BaseAmount);
                }

                discountAmount = Math.Round(discountAmount, 0);

                if (discountAmount <= 0)
                    return Ok(new { success = false, message = "تخفیف قابل اعمال نیست (مبلغ صفر)." });

                // برگرداندن اطلاعات تخفیف
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        Id = coupon.Id,
                        Code = coupon.Code,
                        DiscountAmount = discountAmount,
                        FinalPrice = request.BaseAmount - discountAmount
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = "خطا در اعتبارسنجی کد تخفیف: " + ex.Message });
            }
        }

        [HttpPost("/coupon/consume")]
        public async Task<IActionResult> ConsumeCoupon([FromBody] ConsumeCouponRequest request)
        {
            try
            {
                // اعتبارسنجی ورودی
                if (request == null || request.SubscriptionId <= 0 || request.CouponId <= 0)
                    return BadRequest(new { success = false, message = "اطلاعات نامعتبر است." });

                // دریافت OwnerId از Claims
                var ownerIdClaim = User.FindFirstValue("OwnerId");
                if (string.IsNullOrWhiteSpace(ownerIdClaim) || !int.TryParse(ownerIdClaim, out var ownerId))
                    return Unauthorized(new { success = false, message = "احراز هویت نامعتبر است." });

                // دریافت اشتراک
                var subscription = await _context.Subscriptions
                    .Include(s => s.Restaurant)
                    .FirstOrDefaultAsync(s => s.Id == request.SubscriptionId);

                if (subscription == null)
                    return NotFound(new { success = false, message = "اشتراک یافت نشد." });

                // بررسی مالکیت اشتراک
                if (subscription.OwnerId != ownerId)
                    return BadRequest(new { success = false, message = "شما به این اشتراک دسترسی ندارید." });

                // دریافت کوپن
                var coupon = await _context.Coupons.FindAsync(request.CouponId);
                if (coupon == null)
                    return NotFound(new { success = false, message = "کوپن یافت نشد." });

                // بررسی اینکه آیا کوپن قبلاً برای این اشتراک ثبت شده است؟
                var existingUsage = await _context.CouponUsages
                    .FirstOrDefaultAsync(u => u.SubscriptionId == request.SubscriptionId);

                if (existingUsage != null)
                    return Ok(new { success = false, message = "این اشتراک قبلاً با کوپن ثبت شده است." });

                // بررسی محدودیت استفاده برای مالک
                if (coupon.LimitPerOwner > 0)
                {
                    var ownerUsageCount = await _context.CouponUsages
                        .Where(u => u.CouponId == coupon.Id && u.OwnerId == ownerId && u.Status == "Success")
                        .CountAsync();

                    if (ownerUsageCount >= coupon.LimitPerOwner)
                        return Ok(new { success = false, message = "شما قبلاً از این کد تخفیف استفاده کرده‌اید." });
                }

                // بررسی محدودیت کلی
                if (coupon.UsageLimit.HasValue && coupon.UsedCount >= coupon.UsageLimit)
                    return Ok(new { success = false, message = "تعداد استفاده از این کد به پایان رسیده است." });

                // ثبت استفاده
                var usage = new CouponUsage
                {
                    CouponId = coupon.Id,
                    SubscriptionId = subscription.Id,
                    OwnerId = ownerId,
                    RestaurantId = subscription.RestaurantId,
                    UsedAt = DateTime.Now,
                    DiscountAmount = subscription.DiscountApplied ?? 0,
                    AppliedPrice = subscription.PricePaid,
                    Status = "Success",
                    TransactionId = subscription.TransactionId
                };

                _context.CouponUsages.Add(usage);

                // افزایش شمارنده استفاده
                coupon.UsedCount++;
                coupon.UpdatedAt = DateTime.Now;

                // به‌روزرسانی اشتراک با CouponId
                subscription.CouponId = coupon.Id;
                subscription.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "کوپن با موفقیت مصرف شد.",
                    data = new
                    {
                        usage.Id,
                        coupon.Code,
                        coupon.UsedCount,
                        usage.DiscountAmount,
                        usage.AppliedPrice
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در مصرف کوپن.", detail = ex.Message });
            }
        }

        static string NormalizePeriod(string? p)
        {
            p = (p ?? "").Trim();
            return p switch
            {
                "1" or "Monthly" or "ماهانه" => "Monthly",
                "3" or "3Monthly" or "سه ماهه" => "3Monthly",
                "6" or "6Monthly" or "شش ماهه" => "6Monthly",
                _ => "Monthly"
            };
        }

        static (decimal standardPrice, decimal amount) CalculatePlanAmount(dynamic plan, string period)
        {
            return period switch
            {
                "Monthly" => (plan.PriceMonthly, (plan.DiscountPriceMonthly > 0 ? plan.DiscountPriceMonthly : plan.PriceMonthly)),
                "3Monthly" => (plan.Price3Monthly, (plan.DiscountPrice3Monthly > 0 ? plan.DiscountPrice3Monthly : plan.Price3Monthly)),
                "6Monthly" => (plan.Price6Monthly, (plan.DiscountPrice6Monthly > 0 ? plan.DiscountPrice6Monthly : plan.Price6Monthly)),
                _ => (plan.PriceMonthly, (plan.DiscountPriceMonthly > 0 ? plan.DiscountPriceMonthly : plan.PriceMonthly))
            };
        }


        [HttpGet("zarinpal/verify")]
        public async Task<IActionResult> ZarinpalVerify([FromQuery] string Authority, [FromQuery] string Status)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Authority))
                    return RedirectToAction("PaymentResult", new { success = false, message = "کد Authority معتبر نیست." });

                // 1) پیدا کردن پرداخت Pending مربوط به authority
                var payment = await _context.Subscriptions
                    .FirstOrDefaultAsync(x => x.TransactionId == Authority);

                if (payment == null)
                    return RedirectToAction("PaymentResult", new { success = false, message = "پرداخت یافت نشد." });

                // اگر قبلاً فعال شده
                if (payment.Status == "Active" && payment.IsPaid)
                    return RedirectToAction("PaymentResult", new { success = true, message = "اشتراک قبلاً فعال شده است." });

                // 2) اگر کاربر درگاه را cancel کرده باشد
                if (!string.Equals(Status, "OK", StringComparison.OrdinalIgnoreCase))
                {
                    payment.Status = "Canceled";
                    payment.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                    return RedirectToAction("PaymentResult", new { success = false, message = "پرداخت لغو شد." });
                }

                // 3) تایید پرداخت با زرین‌پال
                var merchantId = _configuration["Zarinpal:MerchantId"];
                long amountInToman = (long)payment.PricePaid;

                var verifyRequest = new
                {
                    merchant_id = merchantId,
                    amount = amountInToman,
                    authority = Authority
                };

                using var client = new HttpClient();
                var response = await client.PostAsJsonAsync("https://payment.zarinpal.com/pg/v4/payment/verify.json", verifyRequest);
                var rawResponse = await response.Content.ReadAsStringAsync();
                dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(rawResponse);

                if (json?.data == null)
                {
                    payment.Status = "Failed";
                    payment.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                    return RedirectToAction("PaymentResult", new { success = false, message = "پاسخ نامعتبر از درگاه." });
                }

                int code = Convert.ToInt32(json.data.code);
                long? refId = json.data.ref_id != null ? Convert.ToInt64(json.data.ref_id) : (long?)null;

                // 4) پرداخت موفق
                if (code == 100 || code == 101)
                {
                    // ===== شروع تراکنش =====
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var now = DateTime.Now;

                        // فعال‌سازی اشتراک
                        payment.Status = "Active";
                        payment.IsPaid = true;
                        payment.PurchaseDate = now;
                        payment.StartDate = now;
                        payment.EndDate = CalculateEndDate(now, payment.SubscriptionPeriod);
                        payment.UpdatedAt = now;

                        _logger.LogInformation($"✅ اشتراک {payment.Id} در حال فعال‌سازی. CouponId: {payment.CouponId}");

                        // ========== مصرف کوپن ==========
                        if (payment.CouponId.HasValue)
                        {
                            _logger.LogInformation($"🔍 شروع مصرف کوپن برای SubscriptionId: {payment.Id}, CouponId: {payment.CouponId}");

                            var coupon = await _context.Coupons.FindAsync(payment.CouponId.Value);
                            if (coupon != null)
                            {
                                _logger.LogInformation($"✅ کوپن پیدا شد: {coupon.Code}, UsedCount: {coupon.UsedCount}");

                                // بررسی اعتبار کوپن
                                var isValid = await CheckCouponValidityForConsume(coupon, payment.OwnerId, payment.RestaurantId);
                                _logger.LogInformation($"✅ اعتبار کوپن: {isValid}");

                                var usage = new CouponUsage
                                {
                                    CouponId = coupon.Id,
                                    SubscriptionId = payment.Id,
                                    OwnerId = payment.OwnerId,
                                    RestaurantId = payment.RestaurantId,
                                    UsedAt = DateTime.Now,
                                    DiscountAmount = payment.DiscountApplied ?? 0,
                                    AppliedPrice = payment.PricePaid,
                                    Status = isValid ? "Success" : "Failed",
                                    TransactionId = payment.TransactionId
                                };

                                _context.CouponUsages.Add(usage);
                                _logger.LogInformation($"✅ CouponUsage ساخته شد. Status: {usage.Status}");

                                if (isValid)
                                {
                                    coupon.UsedCount++;
                                    coupon.UpdatedAt = DateTime.Now;
                                    _logger.LogInformation($"✅ UsedCount افزایش یافت: {coupon.UsedCount}");
                                }
                                else
                                {
                                    _logger.LogWarning($"⚠️ کوپن نامعتبر است: {coupon.Code}");
                                }
                            }
                            else
                            {
                                _logger.LogWarning($"⚠️ کوپن با Id {payment.CouponId} یافت نشد.");
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"⚠️ CouponId برای SubscriptionId: {payment.Id} وجود ندارد.");
                        }

                        // ===== ذخیره همه تغییرات در تراکنش =====
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation($"✅ تراکنش با موفقیت انجام شد. SubscriptionId: {payment.Id}");

                        return RedirectToAction("PaymentResult", new
                        {
                            success = true,
                            message = $"پرداخت موفق. شماره پیگیری: {refId}"
                        });
                    }
                    catch (Exception ex)
                    {
                        // ===== برگرداندن تراکنش در صورت خطا =====
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, $"❌ خطا در تراکنش مصرف کوپن برای SubscriptionId: {payment.Id}");

                        // اشتراک را Failed کنید تا کاربر متوجه شود
                        payment.Status = "Failed";
                        payment.UpdatedAt = DateTime.Now;
                        await _context.SaveChangesAsync();

                        return RedirectToAction("PaymentResult", new
                        {
                            success = false,
                            message = $"خطا در ثبت کوپن: {ex.Message}"
                        });
                    }
                }

                // 5) پرداخت ناموفق از سمت زرین‌پال
                payment.Status = "Failed";
                payment.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                return RedirectToAction("PaymentResult", new
                {
                    success = false,
                    message = $"پرداخت ناموفق. کد خطا: {code}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطای کلی در ZarinpalVerify");
                return RedirectToAction("PaymentResult", new
                {
                    success = false,
                    message = $"خطا: {ex.Message}"
                });
            }
        }

        private async Task<bool> CheckCouponValidityForConsume(Coupon coupon, int ownerId, int restaurantId)
        {
            // بررسی تاریخ
            var now = DateTime.Now;
            if (now < coupon.StartDate || now > coupon.EndDate)
                return false;

            // بررسی محدودیت کلی
            if (coupon.UsageLimit.HasValue && coupon.UsedCount >= coupon.UsageLimit)
                return false;

            // بررسی محدودیت برای مالک
            if (coupon.LimitPerOwner > 0)
            {
                var ownerUsageCount = await _context.CouponUsages
                    .Where(u => u.CouponId == coupon.Id && u.OwnerId == ownerId && u.Status == "Success")
                    .CountAsync();

                if (ownerUsageCount >= coupon.LimitPerOwner)
                    return false;
            }

            // بررسی مالک خاص
            if (coupon.SpecificOwnerId.HasValue && coupon.SpecificOwnerId != ownerId)
                return false;

            // بررسی رستوران خاص
            if (coupon.SpecificRestaurantId.HasValue && coupon.SpecificRestaurantId != restaurantId)
                return false;

            return true;
        }




        private DateTime CalculateEndDate(DateTime startDate, string period)
        {
            return period switch
            {
                "Monthly" => startDate.AddMonths(1),
                "3Monthly" => startDate.AddMonths(3),
                "6Monthly" => startDate.AddMonths(6),
                "12Monthly" => startDate.AddMonths(12),
                _ => startDate.AddMonths(1)
            };
        }



        public IActionResult PaymentResult(bool success, string message)
        {
            ViewBag.Success = success;
            ViewBag.Message = message;
            return View();
        }



    }
}