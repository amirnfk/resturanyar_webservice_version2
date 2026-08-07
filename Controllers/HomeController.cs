using ClosedXML.Excel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using resturanyar.Helpers;
using resturanyar.Models;
using resturanyar.Models.Copoun;
using resturanyar.Models.Receipt;
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
        private readonly IWebHostEnvironment _env;
        private readonly resturanyar.Services.Receipt.IReceiptService _receiptService;
        private readonly resturanyar.Services.Inventory.IInventoryService _inventoryService;

        public HomeController(
            AppDbContext context,
            ILogger<HomeController> logger,
            IConfiguration configuration,
            IWebHostEnvironment env,
            resturanyar.Services.Receipt.IReceiptService receiptService,
            resturanyar.Services.Inventory.IInventoryService inventoryService)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _env = env;
            _receiptService = receiptService;
            _inventoryService = inventoryService;
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
            ViewData["Seo"] = SeoDefaults.HomePage();
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> PrepareUpgrade()
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return RedirectToAction(nameof(ChooseRestaurant));

            var ownerId = User.GetOwnerId();
            if (ownerId == null)
                return RedirectToAction(nameof(ManagerLogin));

            var restaurant = await _context.Restaurants
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId.Value && r.owner_id == ownerId.Value);

            if (restaurant == null)
                return Forbid();

            HttpContext.Session.SetInt32("UpgradeRestaurantId", restaurant.restaurant_id);
            HttpContext.Session.SetString("UpgradeRestaurantName", restaurant.name ?? string.Empty);

            return RedirectToAction(nameof(Upgrade));
        }

        [Authorize]
        public async Task<IActionResult> Upgrade()
        {
            var ownerId = User.GetOwnerId();
            if (ownerId == null)
                return RedirectToAction(nameof(ManagerLogin));

            var restaurantId = HttpContext.Session.GetInt32("UpgradeRestaurantId") ?? User.GetRestaurantId();
            if (restaurantId == null)
                return RedirectToAction(nameof(ChooseRestaurant));

            var restaurant = await _context.Restaurants
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId.Value && r.owner_id == ownerId.Value);

            if (restaurant == null)
            {
                HttpContext.Session.Remove("UpgradeRestaurantId");
                HttpContext.Session.Remove("UpgradeRestaurantName");
                return RedirectToAction(nameof(ChooseRestaurant));
            }

            HttpContext.Session.SetInt32("UpgradeRestaurantId", restaurant.restaurant_id);
            HttpContext.Session.SetString("UpgradeRestaurantName", restaurant.name ?? string.Empty);

            ViewBag.RestaurantId = restaurant.restaurant_id;
            ViewBag.RestaurantName = restaurant.name ?? string.Empty;

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

            if (User.Identity?.IsAuthenticated == true)
            {
                var ownerId = User.FindFirst("OwnerId")?.Value;
                if (!string.IsNullOrEmpty(ownerId))
                {
                    if (User.GetRestaurantId() != null)
                        return RedirectToAction(nameof(Dashboard));

                    return RedirectToAction(nameof(ChooseRestaurant));
                }
            }

            ViewBag.BackgroundImageUrl = RestaurantSettingsHelper.DefaultBackgroundPath;
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
            var settingsDto = await RestaurantSettingsHelper.GetSettingsDtoSafeAsync(_context, restaurantId.Value);

            bool inventoryEnabled = false;
            int inventoryLowStockCount = 0;
            int inventoryItemCount = 0;
            try
            {
                var invSettings = await _inventoryService.GetSettingsIfExistsAsync(restaurantId.Value);
                if (invSettings?.IsEnabled == true)
                {
                    inventoryEnabled = true;
                    var low = await _inventoryService.GetLowStockItemsAsync(restaurantId.Value);
                    inventoryLowStockCount = low.Count;
                    var items = await _inventoryService.ListItemsAsync(restaurantId.Value, activeOnly: true);
                    inventoryItemCount = items.Count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Inventory dashboard stats unavailable for restaurant {RestaurantId}", restaurantId);
            }

            // ========== ساخت ViewModel ==========
            var vm = new DashboardStatsViewModel
            {
                RestaurantName = restaurant.name,
                ReceiptChargesEnabled = restaurant.ReceiptChargesEnabled,

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
                PublicMenuToken = ViewBag.PublicMenuToken,
                PrimaryColor = settingsDto.PrimaryColor,
                SecondaryColor = settingsDto.SecondaryColor,
                LogoUrl = RestaurantSettingsHelper.ResolveAssetUrl(settingsDto.LogoUrl, RestaurantSettingsHelper.DefaultLogoPath),
                BackgroundImageUrl = settingsDto.BackgroundImageUrl,
                InventoryEnabled = inventoryEnabled,
                InventoryLowStockCount = inventoryLowStockCount,
                InventoryItemCount = inventoryItemCount,
            };

            ViewBag.RestaurantId = restaurantId;
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


        public IActionResult ManageStaff(int? restaurantId = null)
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

            var restaurantRows = _context.Restaurants
                .Where(r => r.owner_id == int.Parse(ownerId))
                .AsNoTracking()
                .Select(r => new
                {
                    Restaurant = r,
                    BackgroundImageUrl = r.Setting != null
                        ? r.Setting.BackgroundImageUrl
                        : null
                })
                .ToList();

            var restaurants = restaurantRows.Select(row => row.Restaurant).ToList();
            var restaurantBackgrounds = restaurantRows.ToDictionary(
                row => row.Restaurant.restaurant_id,
                row => RestaurantBackgroundOptions.ResolveUrl(row.BackgroundImageUrl));

            ViewBag.Restaurants = restaurants;
            ViewBag.RestaurantBackgrounds = restaurantBackgrounds;
            ViewBag.BackgroundImageUrl = restaurants.Count > 0
                ? restaurantBackgrounds[restaurants[0].restaurant_id]
                : RestaurantSettingsHelper.DefaultBackgroundPath;

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
                    ReceiptChargesEnabled = true,
                    ReceiptChargesEnabledAt = DateTime.Now,
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
                .Where(s =>
                    s.RestaurantId == restaurantId &&
                    s.Status == "Active" &&
                    s.EndDate >= DateTime.Now)
                .OrderByDescending(s => s.EndDate)
                .ThenByDescending(s => s.Id)
                .FirstOrDefaultAsync();

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
                return RedirectToAction("ManageStaff");
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

            var statusColors = OrderStatusColors.HeaderHexDictionary();

            var activeStatuses = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 12 };

            bool hasCustomRange = from.HasValue || to.HasValue;

            if (hasCustomRange)
            {
                period = null;
                if (from.HasValue) from = from.Value.Date;
                if (to.HasValue) to = to.Value.Date.AddDays(1).AddTicks(-1);
                if (from.HasValue && !to.HasValue) to = from.Value.Date.AddDays(1).AddTicks(-1);
                if (to.HasValue && !from.HasValue) from = to.Value.Date;
            }
            else
            {
                if (string.IsNullOrEmpty(period))
                    period = "month";

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
                    from = today.AddMonths(-1);
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

            var ordersQuery = _context.Orders.AsNoTracking().Where(o => o.RestaurantId == restaurantId);

            if (statusId > 0)
                ordersQuery = ordersQuery.Where(o => o.StatusId == statusId);
            else if (statusId == 0)
                ordersQuery = ordersQuery.Where(o => activeStatuses.Contains(o.StatusId));

            if (from.HasValue) ordersQuery = ordersQuery.Where(o => o.CreatedAt >= from.Value);
            if (to.HasValue) ordersQuery = ordersQuery.Where(o => o.CreatedAt <= to.Value);

            var ordersInRange = await ordersQuery
                .Select(o => new { o.OrderId, o.CreatedAt, o.StatusId })
                .ToListAsync();

            var orderIdsInRange = ordersInRange.Select(o => o.OrderId).ToList();

            // Issued receipt snapshots: food / fees / tax / grand come from here when present.
            var snapshotMap = orderIdsInRange.Count == 0
                ? new Dictionary<int, OrderReceiptSnapshot>()
                : await _context.OrderReceiptSnapshots
                    .AsNoTracking()
                    .Where(s => s.RestaurantId == restaurantId && orderIdsInRange.Contains(s.OrderId))
                    .ToDictionaryAsync(s => s.OrderId);

            var statusGroups = await ordersQuery
                .GroupBy(o => o.StatusId)
                .Select(g => new { StatusId = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalOrders = statusGroups.Sum(g => g.Count);
            var paidOrders = statusGroups.Where(g => g.StatusId == 11).Sum(g => g.Count);
            var cancelledOrders = statusGroups.Where(g => g.StatusId is 9 or 10).Sum(g => g.Count);

            var orderItemsQuery = _context.OrderItems.AsNoTracking()
                .Where(oi => oi.Order.RestaurantId == restaurantId);

            if (statusId > 0)
                orderItemsQuery = orderItemsQuery.Where(oi => oi.Order.StatusId == statusId);
            else if (statusId == 0)
                orderItemsQuery = orderItemsQuery.Where(oi => activeStatuses.Contains(oi.Order.StatusId));

            if (from.HasValue) orderItemsQuery = orderItemsQuery.Where(oi => oi.Order.CreatedAt >= from.Value);
            if (to.HasValue) orderItemsQuery = orderItemsQuery.Where(oi => oi.Order.CreatedAt <= to.Value);

            var orderSubtotals = orderIdsInRange.Count == 0
                ? new Dictionary<int, decimal>()
                : await orderItemsQuery
                    .GroupBy(oi => oi.OrderId)
                    .Select(g => new
                    {
                        OrderId = g.Key,
                        Subtotal = g.Sum(oi =>
                            (decimal)oi.Quantity *
                            (
                                oi.UnitPriceWithDiscount.HasValue &&
                                oi.UnitPriceWithDiscount.Value > 0
                                    ? oi.UnitPriceWithDiscount.Value
                                    : oi.UnitPrice
                            ))
                    })
                    .ToDictionaryAsync(x => x.OrderId, x => x.Subtotal);

            decimal itemsRevenue = 0;
            decimal feesTotal = 0;
            decimal taxTotal = 0;
            decimal discountTotal = 0;
            decimal totalRevenue = 0;
            decimal paidRevenue = 0;
            var breakdownMap = new Dictionary<string, ChargeBreakdownItemDto>(StringComparer.OrdinalIgnoreCase);
            int ordersWithChargesCount = 0;

            foreach (var order in ordersInRange)
            {
                var finance = ResolveOrderFinance(
                    order.OrderId,
                    snapshotMap,
                    orderSubtotals);

                itemsRevenue += finance.ItemsSubtotal;
                feesTotal += finance.FeesTotal;
                taxTotal += finance.TaxTotal;
                discountTotal += finance.DiscountTotal;
                totalRevenue += finance.GrandTotal;

                if (order.StatusId == 11)
                    paidRevenue += finance.GrandTotal;

                if (finance.HasCharges)
                    ordersWithChargesCount++;

                foreach (var line in finance.ChargeLines)
                {
                    if (line.CalculatedAmount == 0)
                        continue;

                    var key = !string.IsNullOrWhiteSpace(line.Code)
                        ? line.Code.Trim()
                        : (line.Title ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(key))
                        key = $"cat-{(int)line.Category}";

                    if (!breakdownMap.TryGetValue(key, out var item))
                    {
                        item = new ChargeBreakdownItemDto
                        {
                            Code = line.Code ?? string.Empty,
                            Title = string.IsNullOrWhiteSpace(line.Title) ? key : line.Title.Trim(),
                            Amount = 0
                        };
                        breakdownMap[key] = item;
                    }

                    item.Amount += line.CalculatedAmount;
                    if (string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(line.Title))
                        item.Title = line.Title.Trim();
                }
            }

            var totalItemsCount = await orderItemsQuery.SumAsync(oi => (int?)oi.Quantity) ?? 0;

            decimal GetFinancialTotal(int orderId) =>
                ResolveOrderFinance(orderId, snapshotMap, orderSubtotals).GrandTotal;

            var salesByDay = ordersInRange
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new SalesPointDto
                {
                    Day = g.Key,
                    Revenue = g.Sum(o => GetFinancialTotal(o.OrderId)),
                    Orders = g.Count()
                })
                .OrderBy(x => x.Day)
                .ToList();

            var salesByHour = Enumerable.Range(0, 24)
                .Select(hour =>
                {
                    var hourOrders = ordersInRange.Where(o => o.CreatedAt.Hour == hour).ToList();
                    return new HourlySalesPointDto
                    {
                        Hour = hour,
                        Orders = hourOrders.Count,
                        Revenue = hourOrders.Sum(o => GetFinancialTotal(o.OrderId))
                    };
                })
                .ToList();

            // Previous equal-length window for owner comparison
            int prevTotalOrders = 0;
            decimal prevTotalRevenue = 0;
            decimal prevPaidRevenue = 0;
            decimal prevAvgOrderValue = 0;
            bool hasPrevComparison = false;
            double? ordersChangePercent = null;
            double? revenueChangePercent = null;
            double? aovChangePercent = null;

            if (from.HasValue && to.HasValue)
            {
                var span = to.Value - from.Value;
                if (span >= TimeSpan.Zero)
                {
                    var prevTo = from.Value.AddTicks(-1);
                    var prevFrom = prevTo - span;

                    var prevOrdersQuery = _context.Orders.AsNoTracking()
                        .Where(o => o.RestaurantId == restaurantId);

                    if (statusId > 0)
                        prevOrdersQuery = prevOrdersQuery.Where(o => o.StatusId == statusId);
                    else if (statusId == 0)
                        prevOrdersQuery = prevOrdersQuery.Where(o => activeStatuses.Contains(o.StatusId));

                    prevOrdersQuery = prevOrdersQuery
                        .Where(o => o.CreatedAt >= prevFrom && o.CreatedAt <= prevTo);

                    var prevOrdersInRange = await prevOrdersQuery
                        .Select(o => new { o.OrderId, o.StatusId })
                        .ToListAsync();

                    if (prevOrdersInRange.Count > 0)
                    {
                        hasPrevComparison = true;
                        prevTotalOrders = prevOrdersInRange.Count;
                        var prevOrderIds = prevOrdersInRange.Select(o => o.OrderId).ToList();

                        var prevSnapshotMap = await _context.OrderReceiptSnapshots
                            .AsNoTracking()
                            .Where(s => s.RestaurantId == restaurantId && prevOrderIds.Contains(s.OrderId))
                            .ToDictionaryAsync(s => s.OrderId);

                        var prevSubtotals = await _context.OrderItems.AsNoTracking()
                            .Where(oi => prevOrderIds.Contains(oi.OrderId))
                            .GroupBy(oi => oi.OrderId)
                            .Select(g => new
                            {
                                OrderId = g.Key,
                                Subtotal = g.Sum(oi =>
                                    (decimal)oi.Quantity *
                                    (
                                        oi.UnitPriceWithDiscount.HasValue &&
                                        oi.UnitPriceWithDiscount.Value > 0
                                            ? oi.UnitPriceWithDiscount.Value
                                            : oi.UnitPrice
                                    ))
                            })
                            .ToDictionaryAsync(x => x.OrderId, x => x.Subtotal);

                        foreach (var order in prevOrdersInRange)
                        {
                            var finance = ResolveOrderFinance(order.OrderId, prevSnapshotMap, prevSubtotals);
                            prevTotalRevenue += finance.GrandTotal;
                            if (order.StatusId == 11)
                                prevPaidRevenue += finance.GrandTotal;
                        }

                        var prevPaidOrders = prevOrdersInRange.Count(o => o.StatusId == 11);
                        prevAvgOrderValue = prevPaidOrders > 0
                            ? Math.Round(prevPaidRevenue / prevPaidOrders, 0)
                            : 0;

                        var avgOrderValue = paidOrders > 0 ? Math.Round(paidRevenue / paidOrders, 0) : 0m;
                        ordersChangePercent = CalcChangePercent(totalOrders, prevTotalOrders);
                        revenueChangePercent = CalcChangePercent(totalRevenue, prevTotalRevenue);
                        aovChangePercent = CalcChangePercent(avgOrderValue, prevAvgOrderValue);
                    }
                }
            }

            var allTopItems = await orderItemsQuery
                .GroupBy(oi => oi.FoodItemId)
                .Select(g => new TopItemDto
                {
                    FoodItemId = g.Key,
                    Name = g.FirstOrDefault()!.FoodName ?? "بدون نام",
                    ImageUrl = g.FirstOrDefault()!.FoodImageUrl ?? "/uploads/food_default.jpg",
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
                .ToListAsync();

            var topByQty = allTopItems.OrderByDescending(x => x.Quantity).Take(topN).ToList();
            var topByRev = allTopItems.OrderByDescending(x => x.Revenue).Take(topN).ToList();

            var vm = new ManagerReportViewModel
            {
                FromDate = from,
                ToDate = to,
                Period = period,
                IsCustomRange = hasCustomRange,
                FilterStatusId = statusId,
                TotalOrders = totalOrders,
                PaidOrders = paidOrders,
                CancelledOrders = cancelledOrders,
                TotalRevenue = totalRevenue,
                PaidRevenue = paidRevenue,
                ItemsRevenue = itemsRevenue,
                FeesTotal = feesTotal,
                TaxTotal = taxTotal,
                DiscountTotal = discountTotal,
                IssuedReceiptCount = snapshotMap.Count,
                OrdersWithChargesCount = ordersWithChargesCount,
                ChargeBreakdown = breakdownMap.Values
                    .OrderByDescending(x => x.Amount)
                    .ToList(),
                HasPreviousPeriodComparison = hasPrevComparison,
                PrevTotalOrders = prevTotalOrders,
                PrevTotalRevenue = prevTotalRevenue,
                PrevAvgOrderValue = prevAvgOrderValue,
                OrdersChangePercent = ordersChangePercent,
                RevenueChangePercent = revenueChangePercent,
                AovChangePercent = aovChangePercent,
                AvgOrderValue = paidOrders > 0 ? Math.Round(paidRevenue / paidOrders, 0) : 0,
                AvgItemsPerOrder = totalOrders > 0 ? Math.Round((double)totalItemsCount / totalOrders, 2) : 0,
                CancelRate = totalOrders > 0 ? Math.Round((double)cancelledOrders * 100 / totalOrders, 2) : 0,
                PaidConversionRate = totalOrders > 0 ? Math.Round((double)paidOrders * 100 / totalOrders, 2) : 0,
                StatusMap = statusMap,
                StatusColors = statusColors,
                SalesByDay = salesByDay,
                SalesByHour = salesByHour,
                TopItemsByQuantity = topByQty,
                TopItemsByRevenue = topByRev,
                TopN = topN
            };

            foreach (var sg in statusGroups)
                vm.StatusCounts[sg.StatusId] = sg.Count;

            ViewData["FilterStatusId"] = statusId;
            ViewData["TopN"] = topN;
            ViewData["CurrentPeriod"] = period?.ToLower();

            if (hasCustomRange)
            {
                if (from.HasValue) ViewData["FromFaDate"] = from.Value.ToPersianDate();
                if (to.HasValue) ViewData["ToFaDate"] = to.Value.Date.ToPersianDate();
            }
            else if (from.HasValue)
            {
                ViewData["FromFaDate"] = from.Value.ToPersianDate();
                if (to.HasValue) ViewData["ToFaDate"] = to.Value.Date.ToPersianDate();
            }

            if (Request.Headers["X-Reports-Partial"] == "true")
                return PartialView("_ManagerReportsPartial", vm);

            return View("ManagerReports", vm);
        }



        [HttpGet("ExportOrdersToExcel")]
        public async Task<IActionResult> ExportOrdersToExcel(
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
                        from = today.AddMonths(-1);
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

                var snapshotMap = await _context.OrderReceiptSnapshots
                    .AsNoTracking()
                    .Where(s => s.RestaurantId == restaurantId)
                    .Where(s => orders.Select(o => o.OrderId).Contains(s.OrderId))
                    .ToDictionaryAsync(s => s.OrderId);

                var fromLabel = DateHelper.ToShamsi(fromDate);
                var toLabel = DateHelper.ToShamsi(toDate);
                var content = resturanyar.Services.OrdersExcelExportService.BuildWorkbook(
                    orders,
                    snapshotMap,
                    fromLabel,
                    toLabel);

                string fileName = $"OrdersReport_{restaurantId}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest($"خطا در تولید گزارش: {ex.Message}");
            }
        }

        [HttpGet("ExportOrdersToPdf")]
        public async Task<IActionResult> ExportOrdersToPdf(
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
                        from = today.AddMonths(-1);
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

                var ordersQuery = _context.Orders
                    .Include(o => o.OrderItems)
                    .Include(o => o.Customer)
                    .Where(o => o.RestaurantId == restaurantId)
                    .Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate);

                if (statusId > 0)
                    ordersQuery = ordersQuery.Where(o => o.StatusId == statusId);

                var orders = ordersQuery.OrderByDescending(o => o.CreatedAt).ToList();

                if (!orders.Any())
                    return BadRequest("هیچ سفارشی در این بازه زمانی یافت نشد.");

                var snapshotMap = await _context.OrderReceiptSnapshots
                    .AsNoTracking()
                    .Where(s => s.RestaurantId == restaurantId)
                    .Where(s => orders.Select(o => o.OrderId).Contains(s.OrderId))
                    .ToDictionaryAsync(s => s.OrderId);

                var fromLabel = DateHelper.ToShamsi(fromDate);
                var toLabel = DateHelper.ToShamsi(toDate);
                var content = resturanyar.Services.OrdersPdfExportService.BuildPdf(
                    orders,
                    snapshotMap,
                    fromLabel,
                    toLabel,
                    _env.WebRootPath);

                string fileName = $"OrdersReport_{restaurantId}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.pdf";
                return File(content, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest($"خطا در تولید گزارش PDF: {ex.Message}");
            }
        }

        private static double? CalcChangePercent(decimal current, decimal previous)
        {
            if (previous == 0)
                return current == 0 ? 0 : null;
            return Math.Round((double)((current - previous) / previous * 100m), 1);
        }

        private static double? CalcChangePercent(int current, int previous) =>
            CalcChangePercent((decimal)current, (decimal)previous);

        private static readonly System.Text.Json.JsonSerializerOptions ReceiptReportJsonOptions = new()
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private sealed class OrderFinanceTotals
        {
            public decimal ItemsSubtotal { get; init; }
            public decimal FeesTotal { get; init; }
            public decimal TaxTotal { get; init; }
            public decimal DiscountTotal { get; init; }
            public decimal GrandTotal { get; init; }
            public bool HasSnapshot { get; init; }
            public bool HasCharges { get; init; }
            public List<ReceiptChargeLineDto> ChargeLines { get; init; } = new();
        }

        /// <summary>
        /// Food/top-items: order items. Fees/tax/discount/breakdown: issued snapshots only.
        /// Grand/sales/AOV: snapshot GrandTotal else items subtotal.
        /// </summary>
        private static OrderFinanceTotals ResolveOrderFinance(
            int orderId,
            IReadOnlyDictionary<int, OrderReceiptSnapshot> snapshotMap,
            IReadOnlyDictionary<int, decimal> orderSubtotals)
        {
            if (snapshotMap.TryGetValue(orderId, out var snapshot))
            {
                var lines = ParseChargeLines(snapshot.ChargeLinesJson);
                var fees = lines.Where(c => c.Category == ChargeCategory.Fee).Sum(c => c.CalculatedAmount);
                var tax = lines.Where(c => c.Category == ChargeCategory.Tax).Sum(c => c.CalculatedAmount);
                var discount = lines.Where(c => c.Category == ChargeCategory.Discount).Sum(c => c.CalculatedAmount);

                return new OrderFinanceTotals
                {
                    ItemsSubtotal = snapshot.ItemsSubtotal,
                    FeesTotal = fees,
                    TaxTotal = tax,
                    DiscountTotal = discount,
                    GrandTotal = snapshot.GrandTotal,
                    HasSnapshot = true,
                    HasCharges = lines.Any(c => c.CalculatedAmount != 0),
                    ChargeLines = lines
                };
            }

            var items = orderSubtotals.GetValueOrDefault(orderId, 0m);
            return new OrderFinanceTotals
            {
                ItemsSubtotal = items,
                GrandTotal = items,
                HasSnapshot = false,
                HasCharges = false
            };
        }

        private static List<ReceiptChargeLineDto> ParseChargeLines(string? chargeLinesJson)
        {
            if (string.IsNullOrWhiteSpace(chargeLinesJson))
                return new List<ReceiptChargeLineDto>();

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<ReceiptChargeLineDto>>(
                           chargeLinesJson,
                           ReceiptReportJsonOptions)
                       ?? new List<ReceiptChargeLineDto>();
            }
            catch
            {
                return new List<ReceiptChargeLineDto>();
            }
        }

        private static void SetChargeCodeCell(
            ClosedXML.Excel.IXLCell cell,
            IEnumerable<ReceiptChargeLineDto> lines,
            string code)
        {
            var amount = lines
                .Where(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
                .Sum(c => c.CalculatedAmount);
            if (amount == 0)
                cell.Value = "-";
            else
                cell.Value = amount;
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
                    CostPrice = f.CostPrice,
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

        [Authorize]
        public async Task<IActionResult> Inventory()
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return RedirectToAction("ChooseRestaurant");

            var restaurant = await _context.Restaurants
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId.Value);

            ViewBag.RestaurantId = restaurantId.Value;
            ViewBag.RestaurantName = restaurant?.name;
            ViewBag.InventoryEnabled = false;
            ViewBag.LowStockCount = 0;
            ViewBag.InventorySchemaReady = true;

            try
            {
                var settings = await _inventoryService.GetSettingsAsync(restaurantId.Value);
                ViewBag.InventoryEnabled = settings.IsEnabled;

                if (settings.IsEnabled)
                {
                    var low = await _inventoryService.GetLowStockItemsAsync(restaurantId.Value);
                    ViewBag.LowStockCount = low.Count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Inventory schema not ready for restaurant {RestaurantId}", restaurantId);
                ViewBag.InventorySchemaReady = false;
            }

            return View();
        }

        [Authorize]
        public async Task<IActionResult> InventoryLowStock()
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return RedirectToAction("ChooseRestaurant");

            try
            {
                var settings = await _inventoryService.GetSettingsAsync(restaurantId.Value);
                if (!settings.IsEnabled)
                    return RedirectToAction("Inventory");

                var restaurant = await _context.Restaurants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId.Value);

                var items = await _inventoryService.GetLowStockItemsAsync(restaurantId.Value);
                ViewBag.RestaurantId = restaurantId.Value;
                ViewBag.RestaurantName = restaurant?.name;
                return View(items);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Inventory low-stock unavailable for restaurant {RestaurantId}", restaurantId);
                return RedirectToAction("Inventory");
            }
        }

        [Authorize]
        public async Task<IActionResult> InventoryMovements(int? itemId = null)
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return RedirectToAction("ChooseRestaurant");

            try
            {
                var settings = await _inventoryService.GetSettingsAsync(restaurantId.Value);
                if (!settings.IsEnabled)
                    return RedirectToAction("Inventory");

                var restaurant = await _context.Restaurants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId.Value);

                ViewBag.RestaurantId = restaurantId.Value;
                ViewBag.RestaurantName = restaurant?.name;
                ViewBag.PreselectedItemId = itemId;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Inventory movements unavailable for restaurant {RestaurantId}", restaurantId);
                return RedirectToAction("Inventory");
            }
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
                    CostPrice = f.CostPrice,
                    IsAvailable = f.IsAvailable,
                    CreatedAt = f.CreatedAt.HasValue
                        ? f.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm")
                        : ""
                })
                .ToListAsync();

            ViewBag.RestaurantId = restaurantId.Value;

            var restaurant = await _context.Restaurants
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId.Value);

            ViewBag.ReceiptChargesEnabled = restaurant?.ReceiptChargesEnabled == true;

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
            ViewBag.ReceiptChargesEnabled = restaurant.ReceiptChargesEnabled;

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

            var statusColors = OrderStatusColors.HeaderHexDictionary();

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
                })
                .ToListAsync();

            if (orders.Count > 0)
            {
                var orderIds = orders.Select(o => o.OrderId).ToList();
                var snapshots = await _context.OrderReceiptSnapshots
                    .AsNoTracking()
                    .Where(s => orderIds.Contains(s.OrderId))
                    .ToDictionaryAsync(s => s.OrderId);

                foreach (var order in orders)
                {
                    if (snapshots.TryGetValue(order.OrderId, out var snapshot))
                    {
                        order.ReceiptGrandTotal = snapshot.GrandTotal;
                        order.ReceiptIssuedAt = snapshot.IssuedAt;
                    }
                }

                if (restaurant.ReceiptChargesEnabled)
                {
                    foreach (var order in orders.Where(o =>
                        !o.ReceiptGrandTotal.HasValue &&
                        _receiptService.IsOrderEligibleForChargeDefaults(restaurant, o.CreatedAt)))
                    {
                        var preview = await _receiptService.PreviewAsync(
                            order.OrderId,
                            restaurantId.Value,
                            new ReceiptPreviewRequest
                            {
                                OrderType = (OrderTypeKind)order.OrderType
                            });

                        if (preview.Success && preview.Receipt != null)
                        {
                            var itemSubtotal = order.OrderItems?.Sum(i => i.TotalPrice) ?? 0;
                            if (preview.Receipt.GrandTotal != itemSubtotal)
                                order.EstimatedReceiptGrandTotal = preview.Receipt.GrandTotal;
                        }
                    }
                }
            }

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

            if (Request.Headers["X-Orders-Partial"] == "true")
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

            var previousStatusId = order.StatusId;
            order.StatusId = newStatusId;
            order.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            object? receiptData = null;
            try
            {
                var issueResult = await _receiptService.TryAutoIssueOnSettlementAsync(
                    orderId,
                    restaurantId.Value,
                    User.GetOwnerId(),
                    previousStatusId,
                    newStatusId);

                if (issueResult.Success && issueResult.Receipt?.IsIssued == true)
                {
                    var issuedAt = issueResult.Receipt.IssuedAt;
                    if (issuedAt.HasValue && issuedAt.Value.Kind == DateTimeKind.Unspecified)
                        issuedAt = DateTime.SpecifyKind(issuedAt.Value, DateTimeKind.Utc);

                    receiptData = new
                    {
                        isIssued = true,
                        grandTotal = issueResult.Receipt.GrandTotal,
                        issuedAt
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto-issue receipt failed for order {OrderId}", orderId);
            }

            try
            {
                var inventoryConsumption = HttpContext.RequestServices
                    .GetRequiredService<resturanyar.Services.Inventory.IOrderInventoryConsumptionService>();
                await inventoryConsumption.HandleStatusChangeAsync(
                    orderId, restaurantId.Value, previousStatusId, newStatusId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Inventory auto-deduct failed for order {OrderId}", orderId);
            }

            return Json(new { success = true, message = "وضعیت با موفقیت به‌روز شد.", newStatusId, receipt = receiptData });
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

            var statusColors = OrderStatusColors.HeaderHexDictionary();

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
        public IActionResult Messages()
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

        public IActionResult Settings()
        {
            return RedirectToAction(nameof(MenuSettings));
        }

        public async Task<IActionResult> RestaurantSetting()
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return RedirectToAction("ChooseRestaurant");

            var restaurant = await _context.Restaurants
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId.Value);

            if (restaurant == null)
                return RedirectToAction("ChooseRestaurant");

            ViewBag.RestaurantId = restaurantId.Value;
            ViewBag.RestaurantName = restaurant.name ?? string.Empty;
            ViewBag.RestaurantCode = restaurant.restaurant_code ?? string.Empty;

            var menuUrl = !string.IsNullOrWhiteSpace(restaurant.PublicMenuToken)
                ? PublicMenuQrHelper.BuildMenuUrl(Url, Request, restaurant.PublicMenuToken)
                : null;
            ViewBag.MenuUrl = menuUrl;
            ViewBag.HasPublicMenuToken = menuUrl != null;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateRestaurantName([FromBody] UpdateRestaurantNameRequest request)
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return Unauthorized(new { success = false, message = "رستوران انتخاب نشده است." });

            if (request == null || string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { success = false, message = "نام رستوران الزامی است." });

            var name = request.Name.Trim();
            if (name.Length > 100)
                return BadRequest(new { success = false, message = "نام رستوران نباید بیش از ۱۰۰ کاراکتر باشد." });

            var restaurant = await _context.Restaurants
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId.Value);

            if (restaurant == null)
                return NotFound(new { success = false, message = "رستوران یافت نشد." });

            restaurant.name = name;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "نام رستوران با موفقیت ذخیره شد.",
                name
            });
        }

        public async Task<IActionResult> MenuSettings()
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return RedirectToAction("ChooseRestaurant");

            var restaurant = await _context.Restaurants
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId.Value);

            ViewBag.RestaurantId = restaurantId.Value;
            ViewBag.RestaurantName = restaurant?.name ?? string.Empty;

            var qrResult = PublicMenuQrHelper.Build(Url, Request, restaurant?.PublicMenuToken);
            ViewBag.MenuUrl = qrResult?.MenuUrl;
            ViewBag.QRCodeImage = qrResult?.QrCodeImageBase64;
            ViewBag.HasPublicMenuToken = qrResult != null;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetRestaurantSettings()
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return Unauthorized(new { success = false, message = "رستوران انتخاب نشده است." });

            var settingsDto = await RestaurantSettingsHelper.GetSettingsDtoSafeAsync(_context, restaurantId.Value);
            return Json(new
            {
                success = true,
                data = settingsDto,
                backgroundOptions = RestaurantBackgroundOptions.ToApiList()
            });
        }

        [HttpPost]
        [RequestSizeLimit(RestaurantLogoUploadHelper.MaxFileSizeBytes + 4096)]
        public async Task<IActionResult> SaveRestaurantSettings([FromForm] SaveRestaurantSettingFormRequest request)
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return Unauthorized(new { success = false, message = "رستوران انتخاب نشده است." });

            if (request == null)
                return BadRequest(new { success = false, message = "داده‌ای ارسال نشده است." });

            var result = await RestaurantSettingsHelper.SaveSettingsAsync(
                _context,
                _env,
                restaurantId.Value,
                request.BackgroundImageUrl,
                request.MenuHeroBadge,
                request.MenuTagline,
                request.Logo);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.ErrorMessage });

            return Json(new
            {
                success = true,
                message = "تنظیمات با موفقیت ذخیره شد.",
                data = result.Data,
                backgroundOptions = RestaurantBackgroundOptions.ToApiList()
            });
        }

        [HttpPost]
        public async Task<IActionResult> ResetRestaurantSettings()
        {
            int? restaurantId = User.GetRestaurantId();
            if (restaurantId == null)
                return Unauthorized(new { success = false, message = "رستوران انتخاب نشده است." });

            var data = await RestaurantSettingsHelper.ResetToDefaultsAsync(_context, _env, restaurantId.Value);

            return Json(new
            {
                success = true,
                message = "تنظیمات به حالت پیش‌فرض بازگردانده شد.",
                data,
                backgroundOptions = RestaurantBackgroundOptions.ToApiList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("ManagerLogin", "Home");
        }

        [HttpPost]
        [ActionName("Logout")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutPost()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("ManagerLogin", "Home");
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
                if (payment.IsPaid && (payment.Status == "Active" || payment.Status == "Renewed"))
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

                        var existingActive = await _context.Subscriptions
                            .Where(s => s.RestaurantId == payment.RestaurantId
                                     && s.Status == "Active"
                                     && s.EndDate > now
                                     && s.Id != payment.Id)
                            .OrderByDescending(s => s.EndDate)
                            .FirstOrDefaultAsync();

                        if (existingActive != null)
                        {
                            existingActive.EndDate = CalculateEndDate(existingActive.EndDate, payment.SubscriptionPeriod);
                            existingActive.SubscriptionPlanId = payment.SubscriptionPlanId;
                            existingActive.SubscriptionPeriod = payment.SubscriptionPeriod;
                            existingActive.UpdatedAt = now;

                            payment.Status = "Renewed";
                            payment.IsPaid = true;
                            payment.PurchaseDate = now;
                            payment.StartDate = existingActive.StartDate;
                            payment.EndDate = existingActive.EndDate;
                            payment.UpdatedAt = now;

                            _logger.LogInformation($"✅ اشتراک {existingActive.Id} تمدید شد. SubscriptionId پرداخت: {payment.Id}");
                        }
                        else
                        {
                            payment.Status = "Active";
                            payment.IsPaid = true;
                            payment.PurchaseDate = now;
                            payment.StartDate = now;
                            payment.EndDate = CalculateEndDate(now, payment.SubscriptionPeriod);
                            payment.UpdatedAt = now;

                            _logger.LogInformation($"✅ اشتراک {payment.Id} در حال فعال‌سازی. CouponId: {payment.CouponId}");
                        }

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
