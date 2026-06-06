using Microsoft.AspNetCore.Mvc;

namespace Resturanyar.Controllers
{
    public class AppDownloadController : Controller
    {
        public IActionResult AppDownload()
        {
            // اطلاعات ثابت و داخلی
            ViewBag.AppName = "رستورانیار";
            ViewBag.DownloadUrl = "https://cafebazaar.ir/app/";
            ViewBag.Description = "برای استفاده از آخرین امکانات اپلیکیشن، لطفاً نسخه جدید را دانلود کنید.";

            return View();
        }
    }
}
