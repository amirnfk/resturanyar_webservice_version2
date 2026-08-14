using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using resturanyar.Models.CustomerModels;
using Resturanyar.Data;

namespace resturanyar.Models
{
    /// <summary>
    /// Optional 1:1 side table for Takeaway/Delivery orders.
    /// Snapshots customer/address at order time so later address edits do not rewrite history.
    /// </summary>
    public class OrderFulfillment
    {
        [Key]
        public int OrderId { get; set; }

        public int? CustomerAddressId { get; set; }

        [MaxLength(200)]
        public string? CustomerNameSnapshot { get; set; }

        [MaxLength(20)]
        public string? PhoneSnapshot { get; set; }

        [MaxLength(1000)]
        public string? AddressSnapshot { get; set; }

        /// <summary>Assigned پیک (Users.user_id) for Delivery orders.</summary>
        public int? AssignedDriverUserId { get; set; }

        public DateTime? AssignedAt { get; set; }

        [MaxLength(500)]
        public string? DeliveryFailureReason { get; set; }

        public DateTime? DeliveryFailedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(OrderId))]
        public Order? Order { get; set; }

        [ForeignKey(nameof(CustomerAddressId))]
        public CustomerAddress? CustomerAddress { get; set; }

        [ForeignKey(nameof(AssignedDriverUserId))]
        public User? AssignedDriver { get; set; }
    }
}
