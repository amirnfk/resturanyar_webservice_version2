using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models.CustomerAccounts
{
    [Table("CustomerAccountTransactions")]
    public class CustomerAccountTransaction
    {
        [Key]
        public int TransactionId { get; set; }

        public int AccountId { get; set; }

        public int RestaurantId { get; set; }

        public int CustomerId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SignedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceAfter { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? CreatedByOwnerId { get; set; }

        public int? CreatedByStaffUserId { get; set; }

        [ForeignKey(nameof(AccountId))]
        public CustomerAccount? Account { get; set; }
    }
}
