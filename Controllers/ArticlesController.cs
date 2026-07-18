using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using resturanyar.Data;
using resturanyar.Models;
using resturanyar.Models.ViewModels;
using resturanyar.Utility;
using Resturanyar.Data;

namespace resturanyar.Controllers
{
    public class ArticlesController : Controller
    {
        private const int PageSize = 9;
        private readonly AppDbContext _context;
        private readonly ILogger<ArticlesController> _logger;

        public ArticlesController(AppDbContext context, ILogger<ArticlesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [Route("/articles")]
        public async Task<IActionResult> Index(int page = 1)
        {
            if (page < 1)
            {
                page = 1;
            }

            await EnsureArticlesSeededAsync();

            var query = _context.Articles
                .AsNoTracking()
                .Where(a => a.IsPublished)
                .OrderByDescending(a => a.PublishedAt);

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));

            if (page > totalPages && totalCount > 0)
            {
                page = totalPages;
            }

            var articles = await query
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(a => new ArticleListItemViewModel
                {
                    Slug = a.Slug,
                    Title = a.Title,
                    MetaDescription = a.MetaDescription,
                    PublishedAt = a.PublishedAt,
                    FeaturedImageUrl = a.FeaturedImageUrl
                })
                .ToListAsync();

            ViewData["Seo"] = new SeoMetadata
            {
                Title = "مقالات رستورانیار | راهنمای مدیریت رستوران و کافه",
                Description = "مقالات آموزشی رستورانیار درباره نرم‌افزار مدیریت رستوران، منوی دیجیتال QR، POS، باشگاه مشتریان و گزارش‌گیری.",
                Keywords = "مقالات رستوران, آموزش مدیریت رستوران, منوی دیجیتال, نرم افزار رستوران, رستورانیار",
                CanonicalUrl = page == 1
                    ? SeoDefaults.SiteUrl + "/articles"
                    : SeoDefaults.SiteUrl + $"/articles?page={page}",
                OgType = "website"
            };

            return View(new ArticleListViewModel
            {
                Articles = articles,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalCount = totalCount
            });
        }

        [HttpGet]
        [Route("/articles/{slug}")]
        public async Task<IActionResult> Detail(string slug)
        {
            var article = await _context.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Slug == slug && a.IsPublished);

            if (article == null)
            {
                return NotFound();
            }

            ViewData["Seo"] = new SeoMetadata
            {
                Title = ArticleSchemaHelper.TrimSeoTitle(article.Title) + " | رستورانیار",
                Description = article.MetaDescription,
                Keywords = article.Keywords,
                CanonicalUrl = SeoDefaults.SiteUrl + "/articles/" + article.Slug,
                OgImage = string.IsNullOrWhiteSpace(article.FeaturedImageUrl)
                    ? SeoDefaults.DefaultOgImage
                    : SeoDefaults.SiteUrl + article.FeaturedImageUrl,
                OgType = "article"
            };

            ViewData["ArticleFaqs"] = ArticleSchemaHelper.ExtractFaqs(article.Content);

            return View(article);
        }

        private async Task EnsureArticlesSeededAsync()
        {
            if (await _context.Articles.AnyAsync())
            {
                return;
            }

            _logger.LogInformation("Articles table is empty. Running seed...");
            if (!ArticleDbSeeder.Seed(_context, Directory.GetCurrentDirectory()))
            {
                _logger.LogWarning("Article seed did not complete. Check Logs/log.txt for details.");
            }
        }
    }
}
