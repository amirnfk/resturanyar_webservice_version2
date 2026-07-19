namespace resturanyar.Models
{
    public class RestaurantSettingDto
    {
        public string PrimaryColor { get; set; } = "#f97316";
        public string SecondaryColor { get; set; } = "#fff7ed";
        public string? BackgroundImageUrl { get; set; }
        public string? LogoUrl { get; set; }
    }

    public class SaveRestaurantSettingFormRequest
    {
        public int RestaurantId { get; set; }
        public string PrimaryColor { get; set; } = "#f97316";
        public string SecondaryColor { get; set; } = "#fff7ed";
        public string? BackgroundImageUrl { get; set; }
        public IFormFile? Logo { get; set; }
    }
}
