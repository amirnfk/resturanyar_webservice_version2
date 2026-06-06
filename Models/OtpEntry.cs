using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models
{
    public class OtpEntry
    {
        public int Id { get; set; }
        public string PhoneNumber { get; set; }
        public string CodeHash { get; set; }
        public DateTime ExpireAt { get; set; }
        public bool Used { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

}
