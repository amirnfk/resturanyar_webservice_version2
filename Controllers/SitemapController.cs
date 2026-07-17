using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Resturanyar.Data;
using System.Text;

namespace resturanyar.Controllers
{
    public class SitemapController : Controller
    {
        private readonly AppDbContext _context;

        public SitemapController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("/sitemap.xml")]
        public async Task<IActionResult> Index()
        {
            var staticPages = new (string Url, string Priority)[]
            {
                ("https://resturanyar.ir/", "1.0"),
                ("https://resturanyar.ir/restaurant-management", "0.8"),
                ("https://resturanyar.ir/cafeshop-management", "0.8"),
                ("https://resturanyar.ir/digital-menu", "0.8"),
                ("https://resturanyar.ir/customer-club", "0.8"),
                ("https://resturanyar.ir/public-support", "0.8"),
                ("https://resturanyar.ir/resturanyar-pricelist", "0.8"),
                ("https://resturanyar.ir/about-us", "0.8"),
                ("https://resturanyar.ir/articles", "0.7")
            };

            var articles = await _context.Articles
                .AsNoTracking()
                .Where(a => a.IsPublished)
                .OrderByDescending(a => a.PublishedAt)
                .Select(a => new { a.Slug, a.PublishedAt })
                .ToListAsync();

            var lastMod = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var xml = new StringBuilder();

            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

            foreach (var page in staticPages)
            {
                AppendUrl(xml, page.Url, lastMod, "weekly", page.Priority);
            }

            foreach (var article in articles)
            {
                AppendUrl(
                    xml,
                    $"https://resturanyar.ir/articles/{article.Slug}",
                    article.PublishedAt.ToString("yyyy-MM-dd"),
                    "monthly",
                    "0.6");
            }

            xml.AppendLine("</urlset>");

            return Content(xml.ToString(), "application/xml");
        }

        private static void AppendUrl(StringBuilder xml, string url, string lastMod, string changeFreq, string priority)
        {
            xml.AppendLine("<url>");
            xml.AppendLine($"<loc>{url}</loc>");
            xml.AppendLine($"<lastmod>{lastMod}</lastmod>");
            xml.AppendLine($"<changefreq>{changeFreq}</changefreq>");
            xml.AppendLine($"<priority>{priority}</priority>");
            xml.AppendLine("</url>");
        }
    }
}
