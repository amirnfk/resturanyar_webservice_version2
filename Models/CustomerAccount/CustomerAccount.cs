using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using resturanyar.Models.CustomerModels;

namespace resturanyar.Models.CustomerAccounts
{
    [Table("CustomerAccounts")]
    public class CustomerAccount
    {
        [Key]
        public int AccountId { get; set; }

        public int RestaurantId { get; set; }

        public int CustomerId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentBalance { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; }
    }
}
