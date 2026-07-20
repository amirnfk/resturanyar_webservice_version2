namespace resturanyar.Utility
{
    public record RestaurantBackgroundOption(string Id, string Label, string Url);

    public static class RestaurantBackgroundOptions
    {
        public static IReadOnlyList<RestaurantBackgroundOption> All { get; } = new[]
        {
            new RestaurantBackgroundOption("default", "پیشفرض", "/images/backgrounds/default.jpg"),
            new RestaurantBackgroundOption("fastfood", "فست فود", "/images/backgrounds/fastfoodbackground.jpg"),
            new RestaurantBackgroundOption("cafe", "کافه رستوران", "/images/backgrounds/caferestaurantbackground.jpg"),
            new RestaurantBackgroundOption("kababi", "کبابی", "/images/backgrounds/kababibackground.jpg"),
            new RestaurantBackgroundOption("sonati", "سنتی", "/images/backgrounds/sonatibackground.jpg"),
            new RestaurantBackgroundOption("modern", "مدرن", "/images/backgrounds/modernbackground.jpg"),
            new RestaurantBackgroundOption("organic", "ارگانیک", "/images/backgrounds/organicbackground.jpg")
        };

        public static string DefaultUrl => All[0].Url;

        public static string GetTemplateId(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return All[0].Id;

            var resolved = ResolveUrl(url);
            var match = All.FirstOrDefault(o => string.Equals(o.Url, resolved, StringComparison.OrdinalIgnoreCase));
            return match?.Id ?? All[0].Id;
        }

        public static RestaurantBackgroundOption? GetByUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return All[0];

            var resolved = ResolveUrl(url);
            return All.FirstOrDefault(o => string.Equals(o.Url, resolved, StringComparison.OrdinalIgnoreCase)) ?? All[0];
        }

        private static readonly Dictionary<string, string> LegacyUrlMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["/images/backgrounds/back1.jpg"] = "/images/backgrounds/default.jpg",
            ["/images/backgrounds/preset-warm.jpg"] = "/images/backgrounds/default.jpg",
            ["/images/backgrounds/preset-dark.jpg"] = "/images/backgrounds/fastfoodbackground.jpg",
            ["/images/backgrounds/preset-fresh.jpg"] = "/images/backgrounds/caferestaurantbackground.jpg",
            ["/images/backgrounds/preset-elegant.jpg"] = "/images/backgrounds/kababibackground.jpg",
            ["/images/backgrounds/preset-cozy.jpg"] = "/images/backgrounds/sonatibackground.jpg",
            ["/images/backgrounds/preset-minimal.jpg"] = "/images/backgrounds/modernbackground.jpg",
            ["/images/modernbackground.jpg"] = "/images/backgrounds/modernbackground.jpg"
        };

        public static string ResolveUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return DefaultUrl;

            var normalized = url.Trim();
            if (LegacyUrlMap.TryGetValue(normalized, out var mapped))
                normalized = mapped;

            return IsAllowed(normalized) ? normalized : DefaultUrl;
        }

        public static bool IsAllowed(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return true;

            var normalized = url.Trim();
            return All.Any(o => string.Equals(o.Url, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public static object ToApiList()
        {
            return All.Select(o =>
            {
                var template = RestaurantMenuTemplates.FromTemplateId(o.Id);
                return new
                {
                    id = o.Id,
                    label = o.Label,
                    url = o.Url,
                    heroBadge = template.HeroBadge,
                    tagline = template.Tagline,
                    primaryColor = template.PrimaryColor,
                    secondaryColor = template.SecondaryColor
                };
            }).ToList();
        }
    }
}
