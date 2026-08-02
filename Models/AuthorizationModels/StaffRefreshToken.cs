using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Resturanyar.Data;

namespace resturanyar.Models.AuthorizationModels
{
    [Table("StaffRefreshTokens")]
    public class StaffRefreshToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(512)]
        public string Token { get; set; }

        public DateTime ExpiryTime { get; set; }

        public int UserId { get; set; }

        public int RestaurantId { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(UserId))]
        public User User { get; set; }

        [ForeignKey(nameof(RestaurantId))]
        public Restaurant Restaurant { get; set; }
    }
}
