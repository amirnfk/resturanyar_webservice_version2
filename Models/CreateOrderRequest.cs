namespace resturanyar.Models
{
    public class CreateOrderRequest
    {
        public int RestaurantId { get; set; }
        public int StatusId { get; set; }
        public string TableNumber { get; set; }
        // Optional: lets clients persist order type early so server can compute default-charge totals.
        // If omitted, server keeps default `DineIn` (0) for backward compatibility.
        public byte? OrderType { get; set; }
        public List<CreateOrderItemRequest> Items { get; set; }
        public string? Description { get; set; }
        public int? CustomerId { get; set; }

        /// <summary>Optional saved customer address for Delivery (and optional Takeaway).</summary>
        public int? CustomerAddressId { get; set; }

        /// <summary>Free-text / override address; required for Delivery when no CustomerAddressId.</summary>
        public string? AddressText { get; set; }

        /// <summary>Optional restaurant-owned order discount code. Omitted/null = no code.</summary>
        public string? DiscountCode { get; set; }
    }
}
