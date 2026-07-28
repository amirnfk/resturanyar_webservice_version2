using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace resturanyar.Models
{
    public class Restaurant
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int restaurant_id { get; set; }

        [Required]
        [MaxLength(100)]
        public string name { get; set; }

        [ForeignKey("Owner")]
        public int owner_id { get; set; }

        public string restaurant_code { get; set; }

        [MaxLength(36)]  
        public string PublicMenuToken { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public bool ReceiptChargesEnabled { get; set; }

        /// <summary>
        /// When charge defaults (estimates, auto-settle) apply to orders.
        /// Only orders created at or after this time get passive charge defaults.
        /// </summary>
        public DateTime? ReceiptChargesEnabledAt { get; set; }

        public ICollection<Category> Categories { get; set; }
        public virtual ICollection<Subscription> Subscriptions { get; set; }
        public virtual RestaurantSetting? Setting { get; set; }

    }
}
