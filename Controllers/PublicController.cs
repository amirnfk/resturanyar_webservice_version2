using Microsoft.AspNetCore.Mvc;
using resturanyar.Utility;

namespace resturanyar.Controllers
{
     

    public class PublicController : Controller
    {
        [HttpGet]
        [Route("/restaurant-management")]
        public IActionResult RestaurantManagement()
        {
            ViewData["Seo"] = SeoDefaults.RestaurantManagement();
            return View();
        }

        [HttpGet]
        [Route("/cafeshop-management")]
       public IActionResult CafeshopManagement()
        {
            ViewData["Seo"] = SeoDefaults.CafeShopManagement();
            return View();
        }

        [HttpGet]
        [Route("/digital-menu")]
        public IActionResult DigitalMenu()
        {
            ViewData["Seo"] = SeoDefaults.DigitalMenu();
            return View();
        }

        [HttpGet]
        [Route("/customer-club")]
       public IActionResult CustomerClub()
        {
            ViewData["Seo"] = SeoDefaults.CustomerClub();
            return View();
        }

        [HttpGet]
        [Route("/public-support")]
        public IActionResult PublicSupport()
        {
            ViewData["Seo"] = SeoDefaults.PublicSupport();
            return View();
        }

        [HttpGet]
        [Route("/support-chat")]
        public IActionResult SupportChatEmbed()
        {
            ViewData["Title"] = "پشتیبانی";
            return View();
        }

        [HttpGet]
        [Route("/resturanyar-pricelist")]
        public IActionResult ResturanyarPriceList()
        {
            ViewData["Seo"] = SeoDefaults.PriceList();
            return View();
        }

        [HttpGet]
        [Route("/about-us")]
        public IActionResult AboutUs()
        {
            ViewData["Seo"] = SeoDefaults.AboutUs();
            return View();
        }

 
    }
}
