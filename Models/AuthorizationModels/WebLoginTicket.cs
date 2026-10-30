using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.AuthorizationModels
{
    [Table("WebLoginTickets")]
    public class WebLoginTicket
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(88)]
        public string TokenHash { get; set; } = string.Empty;

        public int OwnerId { get; set; }

        public int RestaurantId { get; set; }

        [Required]
        [MaxLength(32)]
        public string Target { get; set; } = string.Empty;

        public DateTime ExpiresAtUtc { get; set; }

        public DateTime? UsedAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [MaxLength(64)]
        public string? CreatedIp { get; set; }

        [ForeignKey(nameof(OwnerId))]
        public Owner? Owner { get; set; }

        [ForeignKey(nameof(RestaurantId))]
        public Restaurant? Restaurant { get; set; }
    }
}
