using Microsoft.AspNetCore.Mvc;
using System;
using System.Text;

namespace resturanyar.Controllers
{
    public class SitemapController : Controller
    {
        [HttpGet]
        [Route("/sitemap.xml")]
        public IActionResult Index()
        {
            var urls = new[]
            {
                "https://resturanyar.ir/",
                "https://resturanyar.ir/restaurant-management",
                "https://resturanyar.ir/cafeshop-management",
                "https://resturanyar.ir/digital-menu",
                "https://resturanyar.ir/customer-club",
                "https://resturanyar.ir/public-support",
                "https://resturanyar.ir/resturanyar-pricelist",
                "https://resturanyar.ir/about-us"
            };

            var xml = new StringBuilder();

            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

            foreach (var url in urls)
            {
                xml.AppendLine("<url>");
                xml.AppendLine($"<loc>{url}</loc>");

             
                xml.AppendLine($"<lastmod>{DateTime.Now:yyyy-MM-dd}</lastmod>");

                xml.AppendLine("<changefreq>weekly</changefreq>");
                xml.AppendLine("<priority>0.8</priority>");
                xml.AppendLine("</url>");
            }

            xml.AppendLine("</urlset>");

            return Content(xml.ToString(), "application/xml");
        }
    }
}