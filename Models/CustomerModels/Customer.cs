using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.CustomerModels
{
    public class Customer
    {
        [Key]

        public int CustomerId { get; set; }

        [Required]
        [Column("RestaurantId")]
        public int RestaurantId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Mobile { get; set; }

        [MaxLength(200)]
        public string? FullName { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

         
        [ForeignKey(nameof(RestaurantId))]
        public virtual Restaurant? Restaurant { get; set; }
    }
}
