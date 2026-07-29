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

    }
}
