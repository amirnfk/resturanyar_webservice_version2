using System.ComponentModel.DataAnnotations;

namespace resturanyar.Models
{
    public class Article
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Slug { get; set; } = string.Empty;

        [Required]
        [MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string MetaDescription { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Keywords { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime PublishedAt { get; set; }

        public bool IsPublished { get; set; } = true;

        [MaxLength(100)]
        public string Author { get; set; } = "رستورانیار";

        [MaxLength(500)]
        public string? FeaturedImageUrl { get; set; }
    }
}
