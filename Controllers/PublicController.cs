using Microsoft.AspNetCore.Mvc;

namespace resturanyar.Controllers
{
     

    public class PublicController : Controller
    {
        [Route("/restaurant-management")]
        public IActionResult RestaurantManagement()
        {
            return View();
        }
        [Route("/cafeshop-management")]
        public IActionResult CafeshopManagement()
        {
            return View();
        }

        [Route("/digital-menu")]
        public IActionResult DigitalMenu()
        {
            return View();
        }
        [Route("/customer-club")]
        public IActionResult CustomerClub()
        {
            return View();
        }
        [Route("/public-support")]
        public IActionResult PublicSupport()
        {
            return View();
        }

        [Route("/about-us")]
        public IActionResult AboutUs()
        {
            return View();
        }

 
    }
}
