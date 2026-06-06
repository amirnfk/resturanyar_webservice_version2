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
        public ICollection<Category> Categories { get; set; }
        public virtual ICollection<Subscription> Subscriptions { get; set; }

    }
}
