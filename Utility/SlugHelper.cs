using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Resturanyar.Data;

namespace resturanyar.Utility
{
    public static class SlugHelper
    {
        public static string GenerateSlug(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            var slug = title.Trim().ToLowerInvariant();
            slug = Regex.Replace(slug, @"[\s_]+", "-");
            slug = Regex.Replace(slug, @"[^a-z0-9\-]", string.Empty);
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');

            return slug;
        }

        public static async Task<string> EnsureUniqueSlugAsync(AppDbContext context, string slug, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(slug))
                slug = "article";

            var baseSlug = slug;
            var candidate = baseSlug;
            var counter = 2;

            while (await context.Articles.AnyAsync(a =>
                       a.Slug == candidate && (!excludeId.HasValue || a.Id != excludeId.Value)))
            {
                candidate = $"{baseSlug}-{counter}";
                counter++;
            }

            return candidate;
        }
    }
}
