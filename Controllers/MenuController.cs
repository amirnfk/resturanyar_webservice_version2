using Microsoft.AspNetCore.Mvc;
using resturanyar.Models;
using Resturanyar.Data;
using Microsoft.EntityFrameworkCore;
using resturanyar.Utility;

public class MenuController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<MenuController> _logger;
    public MenuController(AppDbContext context, ILogger<MenuController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // 📌 منوی خصوصی (با سشن)
    //public async Task<IActionResult> RestaurantMenu()
    //{
    //    if (!User.Identity.IsAuthenticated)
    //        return RedirectToAction("ManagerLogin", "Home");

    //    var restaurantIdString = User.FindFirst("RestaurantId")?.Value;
    //    if (string.IsNullOrEmpty(restaurantIdString))
    //        return RedirectToAction("ChooseRestaurant", "Home"); // چون اول باید رستوران رو انتخاب کنه

    //    if (!int.TryParse(restaurantIdString, out int restaurantId))
    //        return RedirectToAction("ChooseRestaurant", "Home");

    //    var items = await _context.FoodItems
    //        .Where(f => f.RestaurantId == restaurantId)
    //        .Select(f => new FoodItemViewModel
    //        {
    //            FoodItemId = f.FoodItemId,
    //            RestaurantId = f.RestaurantId,
    //            Name = f.Name ?? "",
    //            Description = f.Description ?? "",
    //            ImageUrl = f.ImageUrl ?? "",
    //             CategoryId= f.CategoryId,

    //            Price = f.Price,
    //            DiscountPrice = f.DiscountPrice ?? 0,
    //            CostPrice = f.CostPrice ?? 0,
    //            IsAvailable = f.IsAvailable,
    //            CreatedAt = f.CreatedAt.HasValue
    //                ? f.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm")
    //                : ""
    //        })
    //        .ToListAsync();

    //    ViewBag.RestaurantId = restaurantId;
    //    return View(items);
    //}



    public IActionResult RestaurantMenu()
    {
        if (!User.Identity.IsAuthenticated)
            return RedirectToAction("ManagerLogin", "Home");

        return RedirectToAction("MenuSettings", "Home");
    }

    public IActionResult PublicMenuQRCode()
    {
        if (!User.Identity.IsAuthenticated)
            return RedirectToAction("ManagerLogin", "Home");

        return RedirectToAction("MenuSettings", "Home");
    }

    [HttpGet("PublicMenu")]
    public async Task<IActionResult> PublicMenu(string token)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
            {
                ViewBag.Error = "توکن ارسال نشده است.";
                return View("PublicMenuError");
            }

            var restaurant = await _context.Restaurants
                .FirstOrDefaultAsync(r => r.PublicMenuToken == token);

            if (restaurant == null)
            {
                ViewBag.Error = "رستوران با این توکن یافت نشد.";
                return View("PublicMenuError");
            }

            //var items = await _context.FoodItems
            //    .Where(f => f.RestaurantId == restaurant.restaurant_id)
            //    .Select(f => new FoodItemViewModel
            //    {
            //        FoodItemId = f.FoodItemId,
            //        RestaurantId = f.RestaurantId,
            //        Name = f.Name ?? "",
            //        Description = f.Description ?? "",
            //        ImageUrl = f.ImageUrl ?? "",
            //        CategoryId = f.CategoryId  ,
                    
            //        Price = f.Price,
            //        DiscountPrice = f.DiscountPrice ?? 0,
            //        CostPrice = f.CostPrice ?? 0,
            //        IsAvailable = f.IsAvailable,
            //        CreatedAt = f.CreatedAt.HasValue
            //            ? f.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm")
            //            : ""
            //    })
            //    .ToListAsync();


            var items = await _context.FoodItems
           .Where(f => f.RestaurantId == restaurant.restaurant_id && f.IsActive)
           .Join(_context.Categories,
                 f => f.CategoryId,
                 c => c.CategoryId,
                 (f, c) => new FoodItemViewModel
                 {
                     FoodItemId = f.FoodItemId,
                     RestaurantId = f.RestaurantId,
                     Name = f.Name ?? "",
                     Description = f.Description ?? "",
                     ImageUrl = f.ImageUrl ?? "",
                     CategoryId = f.CategoryId,
                     CategoryName = c.CategoryName ?? "",
                     CategoryDisplayOrder = c.DisplayOrder,
                     Price = f.Price,
                     DiscountPrice = f.DiscountPrice ?? 0,
                     CostPrice = f.CostPrice,
                     IsAvailable = f.IsAvailable,
                     CreatedAt = f.CreatedAt.HasValue
                         ? f.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm")
                         : ""
                 })
           .ToListAsync();

            ViewBag.RestaurantId = restaurant.restaurant_id;
            ViewBag.RestaurantName = restaurant.name;

            var settingsDto = await RestaurantSettingsHelper.GetSettingsDtoSafeAsync(_context, restaurant.restaurant_id);
            RestaurantSettingsHelper.PopulateMenuPresentation(ViewData, settingsDto, restaurant.name);

            return View(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطا در PublicMenu با توکن {Token}", token);
            ViewBag.Error = "خطایی در پردازش درخواست رخ داد.";
            return View("PublicMenuError");
        }
    }
}
