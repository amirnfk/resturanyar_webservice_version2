using resturanyar.Models.CustomerModels;
using Resturanyar.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace resturanyar.Models
{
    public class Order
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int OrderId { get; set; }
        public int RestaurantId { get; set; }
        public string TableNumber { get; set; }
        public int StatusId { get; set; }
        public int? CustomerId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string CreatedAtShamsi { get; set; }
        public string UpdatedAtShamsi { get; set; }

        public string? Description { get; set; }  

        public List<OrderItem> OrderItems { get; set; }
        public OrderStatus Status { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; }
    }


}
