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

        /// <summary>When true, Takeaway (OrderType=1) create is allowed for this restaurant.</summary>
        public bool EnableTakeaway { get; set; } = true;

        /// <summary>When true, Delivery (OrderType=2) create is allowed for this restaurant.</summary>
        public bool EnableDelivery { get; set; } = true;

        /// <summary>When true, delivery orders at status 5 are auto-assigned to DefaultDeliveryDriverUserId.</summary>
        public bool AutoAssignDeliveryDriver { get; set; } = true;

        /// <summary>Default courier (Users.user_id) for auto-assign when AutoAssignDeliveryDriver is on.</summary>
        public int? DefaultDeliveryDriverUserId { get; set; }

        public ICollection<Category> Categories { get; set; }
        public virtual ICollection<Subscription> Subscriptions { get; set; }
        public virtual RestaurantSetting? Setting { get; set; }

    }
}
