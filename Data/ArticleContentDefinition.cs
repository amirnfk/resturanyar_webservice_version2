namespace resturanyar.Data
{
    public class ArticleContentDefinition
    {
        public string Slug { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public string? Keywords { get; set; }
        public string Author { get; set; } = "تیم محتوای رستورانیار";
        public string? FeaturedImageUrl { get; set; }
        public DateTime PublishedAt { get; set; }
        public string? ContentHtml { get; set; }
        public string? ContentFile { get; set; }
    }
}
