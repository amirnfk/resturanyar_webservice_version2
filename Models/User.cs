using resturanyar.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Resturanyar.Data
{
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int user_id { get; set; }

        [Required]
        public string name { get; set; }

        [Required]
        [MaxLength(255)]
        public string password { get; set; }

        [ForeignKey("Restaurant")]
        public int restaurant_id { get; set; }

        
        [Required]
        public int role_id { get; set; }

        public Role Role { get; set; }
        public Restaurant Restaurant { get; set; }
        [Required]
        public bool order_management_permission { get; set; } = false;

        [Required]
        public bool kitchen_management_permission { get; set; } = false;

        [Required]
        public bool payment_management_permission { get; set; } = false;

    }
}
