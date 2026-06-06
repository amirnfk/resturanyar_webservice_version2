using Microsoft.AspNetCore.Mvc;
using resturanyar.Models;
using Resturanyar.Data;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using resturanyar.Controllers;

using System.Reflection;

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



    public async Task<IActionResult> RestaurantMenu()
    {
        if (!User.Identity.IsAuthenticated)
            return RedirectToAction("ManagerLogin", "Home");

        var restaurantIdString = User.FindFirst("RestaurantId")?.Value;
        if (string.IsNullOrEmpty(restaurantIdString))
            return RedirectToAction("ChooseRestaurant", "Home");

        if (!int.TryParse(restaurantIdString, out int restaurantId))
            return RedirectToAction("ChooseRestaurant", "Home");

        var items = await _context.FoodItems
            .Where(f => f.RestaurantId == restaurantId && f.IsActive)
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
                      Price = f.Price,
                      DiscountPrice = f.DiscountPrice ?? 0,
                      CostPrice = f.CostPrice ?? 0,
                      IsAvailable = f.IsAvailable,
                      CreatedAt = f.CreatedAt.HasValue
                          ? f.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm")
                          : ""
                  })
            .ToListAsync();

        ViewBag.RestaurantId = restaurantId;
        return View(items);
    }

    // 📌 QR Code
    public async Task<IActionResult> PublicMenuQRCode()
    {
        if (!User.Identity.IsAuthenticated)
            return RedirectToAction("ManagerLogin", "Home");

        var restaurantIdString = User.FindFirst("RestaurantId")?.Value;
        if (string.IsNullOrEmpty(restaurantIdString))
            return RedirectToAction("ChooseRestaurant", "Home");

        if (!int.TryParse(restaurantIdString, out int restaurantId))
            return RedirectToAction("ChooseRestaurant", "Home");

        var restaurant = await _context.Restaurants.FindAsync(restaurantId);
        if (restaurant == null)
            return NotFound("رستوران پیدا نشد.");

        var tokenProp = restaurant.GetType().GetProperty("PublicMenuToken")
                        ?? restaurant.GetType().GetProperty("public_menu_token")
                        ?? restaurant.GetType().GetProperty("PublicMenuToken".ToLower());
        var token = tokenProp?.GetValue(restaurant)?.ToString();

        if (string.IsNullOrWhiteSpace(token))
            return BadRequest("توکن منوی عمومی برای این رستوران تنظیم نشده است.");

        var menuUrl = Url.Action("PublicMenu", "Menu", new { token = token }, Request.Scheme)
                      ?? $"{Request.Scheme}://{Request.Host}/Menu/PublicMenu?token={Uri.EscapeDataString(token)}";

        using var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(menuUrl, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new Base64QRCode(qrData);
        var qrCodeImageAsBase64 = qrCode.GetGraphic(20);

        var nameProp = restaurant.GetType().GetProperty("Name") ?? restaurant.GetType().GetProperty("name");
        ViewBag.RestaurantName = nameProp?.GetValue(restaurant)?.ToString() ?? "";
        ViewBag.QRCodeImage = qrCodeImageAsBase64;
        ViewBag.MenuUrl = menuUrl;

        return View();
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
                     Price = f.Price,
                     DiscountPrice = f.DiscountPrice ?? 0,
                     CostPrice = f.CostPrice ?? 0,
                     IsAvailable = f.IsAvailable,
                     CreatedAt = f.CreatedAt.HasValue
                         ? f.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm")
                         : ""
                 })
           .ToListAsync();

            ViewBag.RestaurantId = restaurant.restaurant_id;
            ViewBag.RestaurantName = restaurant.name;

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
