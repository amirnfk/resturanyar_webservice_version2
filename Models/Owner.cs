using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;

namespace resturanyar.Models
{
    public class Owner
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل معتبر نیست.")]
        [StringLength(11)]
        public string Phone { get; set; }

        [Required]
        public string Password { get; set; }

        public int role_id { get; set; }

        public Role Role { get; set; }
        public virtual ICollection<Subscription> Subscriptions { get; set; }

    }
}
