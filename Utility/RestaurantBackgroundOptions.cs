namespace resturanyar.Utility
{
    public record RestaurantBackgroundOption(string Id, string Label, string Url);

    public static class RestaurantBackgroundOptions
    {
        public static IReadOnlyList<RestaurantBackgroundOption> All { get; } = new[]
        {
            new RestaurantBackgroundOption("warm", "گرم و کلاسیک", "/images/backgrounds/preset-warm.jpg"),
            new RestaurantBackgroundOption("dark", "تیره و مدرن", "/images/backgrounds/preset-dark.jpg"),
            new RestaurantBackgroundOption("fresh", "سبز و تازه", "/images/backgrounds/preset-fresh.jpg"),
            new RestaurantBackgroundOption("elegant", "بنفش و شیک", "/images/backgrounds/preset-elegant.jpg"),
            new RestaurantBackgroundOption("cozy", "قهوه‌ای و دنج", "/images/backgrounds/preset-cozy.jpg"),
            new RestaurantBackgroundOption("minimal", "مینیمال روشن", "/images/backgrounds/preset-minimal.jpg")
        };

        public static string DefaultUrl => All[0].Url;

        public static bool IsAllowed(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return true;

            var normalized = url.Trim();
            return All.Any(o => string.Equals(o.Url, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public static object ToApiList()
        {
            return All.Select(o => new { id = o.Id, label = o.Label, url = o.Url }).ToList();
        }
    }
}
