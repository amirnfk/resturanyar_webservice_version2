namespace resturanyar.Models
{
    public class CreateOrderRequest
    {
        public int RestaurantId { get; set; }
        public int StatusId { get; set; }
        public string TableNumber { get; set; }
        public List<CreateOrderItemRequest> Items { get; set; }
        public string? Description { get; set; }
        public int? CustomerId { get; set; }

    }
}
