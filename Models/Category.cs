using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models
{
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required]
        [MaxLength(100)]
        public string CategoryName { get; set; }

        // 📌 ارتباط با رستوران
        [Required]
        public int RestaurantId { get; set; }
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(RestaurantId))]
        public Restaurant Restaurant { get; set; }

        [JsonIgnore]
        public ICollection<FoodItem> FoodItems { get; set; }

    }
}
