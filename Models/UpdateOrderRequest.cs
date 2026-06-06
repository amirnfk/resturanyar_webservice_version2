using resturanyar.Models;

public class UpdateOrderRequest
{
    public int OrderId { get; set; }

    public string TableNumber { get; set; }
    public int RestaurantId { get; set; }
    public int StatusId { get; set; }
    public string? Description { get; set; }   

    public List<OrderItemDto> Items { get; set; }
}
