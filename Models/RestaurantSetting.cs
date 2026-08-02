using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models
{
    public class RestaurantSetting
    {
        [Key]
        [ForeignKey(nameof(Restaurant))]
        public int RestaurantId { get; set; }

        [Required]
        [MaxLength(9)]
        public string PrimaryColor { get; set; } = "#f97316";

        [Required]
        [MaxLength(9)]
        public string SecondaryColor { get; set; } = "#f97316";

        [MaxLength(500)]
        public string? BackgroundImageUrl { get; set; }

        [MaxLength(500)]
        public string? LogoUrl { get; set; }

        [MaxLength(80)]
        public string? MenuHeroBadge { get; set; }

        [MaxLength(160)]
        public string? MenuTagline { get; set; }

        public Restaurant Restaurant { get; set; } = null!;
    }
}
