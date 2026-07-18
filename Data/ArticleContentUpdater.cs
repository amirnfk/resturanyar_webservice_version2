using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using Resturanyar.Data;
using Serilog;

namespace resturanyar.Data
{
    public static class ArticleContentUpdater
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static int SyncFromContentFolder(AppDbContext context, string contentRootPath)
        {
            var folder = Path.Combine(contentRootPath, "Data", "ArticleContent");
            if (!Directory.Exists(folder))
            {
                Log.Warning("Article content folder not found: {Folder}", folder);
                return 0;
            }

            var jsonFiles = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
            var updated = 0;

            foreach (var jsonPath in jsonFiles)
            {
                try
                {
                    var json = File.ReadAllText(jsonPath);
                    var definition = JsonSerializer.Deserialize<ArticleContentDefinition>(json, JsonOptions);
                    if (definition == null || string.IsNullOrWhiteSpace(definition.Slug))
                    {
                        Log.Warning("Skipping invalid article content file: {Path}", jsonPath);
                        continue;
                    }

                    var content = ResolveContentHtml(folder, definition);
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        Log.Warning("No content HTML for slug {Slug} in {Path}", definition.Slug, jsonPath);
                        continue;
                    }

                    var existing = context.Articles.FirstOrDefault(a => a.Slug == definition.Slug);
                    var now = DateTime.Now;

                    if (existing == null)
                    {
                        context.Articles.Add(new Article
                        {
                            Slug = definition.Slug.Trim(),
                            Title = definition.Title.Trim(),
                            MetaDescription = definition.MetaDescription.Trim(),
                            Keywords = definition.Keywords?.Trim(),
                            Content = content,
                            Author = string.IsNullOrWhiteSpace(definition.Author) ? "تیم محتوای رستورانیار" : definition.Author.Trim(),
                            FeaturedImageUrl = definition.FeaturedImageUrl?.Trim(),
                            PublishedAt = definition.PublishedAt == default ? now : definition.PublishedAt,
                            UpdatedAt = now,
                            IsPublished = true
                        });
                        updated++;
                        Log.Information("Created article from content file: {Slug}", definition.Slug);
                    }
                    else
                    {
                        existing.Title = definition.Title.Trim();
                        existing.MetaDescription = definition.MetaDescription.Trim();
                        existing.Keywords = definition.Keywords?.Trim();
                        existing.Content = content;
                        existing.Author = string.IsNullOrWhiteSpace(definition.Author) ? existing.Author : definition.Author.Trim();
                        existing.FeaturedImageUrl = definition.FeaturedImageUrl?.Trim() ?? existing.FeaturedImageUrl;
                        if (definition.PublishedAt != default)
                            existing.PublishedAt = definition.PublishedAt;
                        existing.UpdatedAt = now;
                        updated++;
                        Log.Information("Updated article from content file: {Slug}", definition.Slug);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to sync article content from {Path}", jsonPath);
                }
            }

            if (updated > 0)
                context.SaveChanges();

            return updated;
        }

        private static string? ResolveContentHtml(string folder, ArticleContentDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(definition.ContentFile))
            {
                var htmlPath = Path.Combine(folder, definition.ContentFile);
                if (File.Exists(htmlPath))
                    return File.ReadAllText(htmlPath);
            }

            var defaultHtmlPath = Path.Combine(folder, definition.Slug + ".html");
            if (File.Exists(defaultHtmlPath))
                return File.ReadAllText(defaultHtmlPath);

            return definition.ContentHtml;
        }
    }
}
