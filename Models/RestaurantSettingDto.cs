namespace resturanyar.Models
{
    public class RestaurantSettingDto
    {
        public string PrimaryColor { get; set; } = "#f97316";
        public string SecondaryColor { get; set; } = "#fff7ed";
        public string? BackgroundImageUrl { get; set; }
        public string? LogoUrl { get; set; }
        public string MenuHeroBadge { get; set; } = string.Empty;
        public string MenuTagline { get; set; } = string.Empty;
        public string? MenuHeroBadgeCustom { get; set; }
        public string? MenuTaglineCustom { get; set; }
        public string MenuHeroBadgeDefault { get; set; } = string.Empty;
        public string MenuTaglineDefault { get; set; } = string.Empty;
    }

    public class SaveRestaurantSettingFormRequest
    {
        public int RestaurantId { get; set; }
        public string? BackgroundImageUrl { get; set; }
        public string? MenuHeroBadge { get; set; }
        public string? MenuTagline { get; set; }
        public IFormFile? Logo { get; set; }
    }
}
