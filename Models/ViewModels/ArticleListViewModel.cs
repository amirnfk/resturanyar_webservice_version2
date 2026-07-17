namespace resturanyar.Models.ViewModels
{
    public class ArticleListItemViewModel
    {
        public string Slug { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public string? FeaturedImageUrl { get; set; }
    }

    public class ArticleListViewModel
    {
        public IReadOnlyList<ArticleListItemViewModel> Articles { get; set; } = Array.Empty<ArticleListItemViewModel>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
    }
}
