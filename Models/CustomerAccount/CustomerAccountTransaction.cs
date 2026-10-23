using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using resturanyar.Models;

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

        /// <summary>Linked order when debt was created from POS settlement (نسیه).</summary>
        public int? OrderId { get; set; }

        /// <summary>Cash collected alongside this order debt (partial pay); null for manual debts.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? PaidAmount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? CreatedByOwnerId { get; set; }

        public int? CreatedByStaffUserId { get; set; }

        [ForeignKey(nameof(AccountId))]
        public CustomerAccount? Account { get; set; }

        [ForeignKey(nameof(OrderId))]
        public Order? Order { get; set; }
    }
}
