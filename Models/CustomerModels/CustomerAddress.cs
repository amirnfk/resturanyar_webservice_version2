using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.CustomerModels
{
    public class CustomerAddress
    {
        [Key]
        public int AddressId { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [MaxLength(100)]
        public string? Title { get; set; }

        [Required]
        [MaxLength(1000)]
        public string AddressText { get; set; }

        [MaxLength(10)]
        public string? Unit { get; set; }

        [MaxLength(10)]
        public string? Floor { get; set; }

        [MaxLength(10)]
        public string? PlateNumber { get; set; }

        [Column(TypeName = "decimal(10,7)")]
        public decimal? Latitude { get; set; }

        [Column(TypeName = "decimal(10,7)")]
        public decimal? Longitude { get; set; }

        public bool IsDefault { get; set; } = false;

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation property (اختیاری)
        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }
    }
}
