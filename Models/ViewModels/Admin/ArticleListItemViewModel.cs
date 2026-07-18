namespace resturanyar.Models.ViewModels.Admin
{
    public class ArticleListItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public bool IsPublished { get; set; }
        public string Author { get; set; } = string.Empty;
    }
}
