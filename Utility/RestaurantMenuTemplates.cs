namespace resturanyar.Utility
{
    public record RestaurantMenuTemplate(
        string Id,
        string Label,
        string Tagline,
        string HeroBadge,
        string HeroEmoji,
        string PrimaryColor,
        string SecondaryColor);

    public static class RestaurantMenuTemplates
    {
        private static readonly Dictionary<string, RestaurantMenuTemplate> ById = new(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new("default", "پیشفرض", "طعم‌های خاص، لحظه‌های به‌یادماندنی", "منوی دیجیتال", "🍽️", "#e85d04", "#ff922b"),
            ["fastfood"] = new("fastfood", "فست فود", "سریع انتخاب کن، زود لذت ببر", "فست‌فود", "🍔", "#ef4444", "#f59e0b"),
            ["cafe"] = new("cafe", "کافه رستوران", "آرام بنشین، با طعم همراه شو", "کافه", "☕", "#8b5e3c", "#c99465"),
            ["kababi"] = new("kababi", "کبابی", "دود، آتش و طعم اصیل", "کبابی", "🥩", "#d97706", "#b45309"),
            ["sonati"] = new("sonati", "سنتی", "سفره‌ای به سبک خانه", "سنتی", "🍲", "#1e3a5f", "#3d6b9a"),
            ["modern"] = new("modern", "مدرن", "ساده، شیک، بی‌نقص", "مدرن", "✨", "#6366f1", "#a855f7"),
            ["organic"] = new("organic", "ارگانیک", "غذای سالم • از مزرعه تا سفره", "ارگانیک", "🌿", "#2d6a4f", "#52b788")
        };

        public static RestaurantMenuTemplate Default => ById["default"];

        public static RestaurantMenuTemplate FromBackgroundUrl(string? backgroundUrl)
        {
            var templateId = RestaurantBackgroundOptions.GetTemplateId(backgroundUrl);
            return ById.TryGetValue(templateId, out var template) ? template : Default;
        }

        public static RestaurantMenuTemplate FromTemplateId(string? templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId))
                return Default;

            return ById.TryGetValue(templateId.Trim(), out var template) ? template : Default;
        }
    }
}
