namespace resturanyar.Models
{
    public class SeoMetadata
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Keywords { get; set; }
        public string CanonicalUrl { get; set; } = string.Empty;
        public string OgImage { get; set; } = "https://resturanyar.ir/images/og-resturanyar.jpg";
        public string OgType { get; set; } = "website";
        public bool NoIndex { get; set; }
    }
}
