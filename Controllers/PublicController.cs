using Microsoft.AspNetCore.Mvc;

namespace resturanyar.Controllers
{
     

    public class PublicController : Controller
    {
        [HttpGet]
        [Route("/restaurant-management")]
        public IActionResult RestaurantManagement()
        {
            return View();
        }

        [HttpGet]
        [Route("/cafeshop-management")]
       public IActionResult CafeshopManagement()
        {
            return View();
        }

        [HttpGet]
        [Route("/digital-menu")]
        public IActionResult DigitalMenu()
        {
            return View();
        }

        [HttpGet]
        [Route("/customer-club")]
       public IActionResult CustomerClub()
        {
            return View();
        }

        [HttpGet]
        [Route("/public-support")]
        public IActionResult PublicSupport()
        {
            return View();
        }


        [HttpGet]
        [Route("/resturanyar-pricelist")]
        public IActionResult ResturanyarPriceList()
        {
            return View();
        }

        [HttpGet]
        [Route("/about-us")]
        public IActionResult AboutUs()
        {
            return View();
        }

 
    }
}
