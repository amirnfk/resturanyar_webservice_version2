using resturanyar.Models;

public class UpdateOrderRequest
{
    public int OrderId { get; set; }

    public string TableNumber { get; set; }
    public int RestaurantId { get; set; }
    public int StatusId { get; set; }
    public string? Description { get; set; }   

    public List<OrderItemDto> Items { get; set; }
    public int? CustomerId { get; set; }

    /// <summary>Update fulfillment address link for open Takeaway/Delivery orders. OrderType cannot change after create.</summary>
    public int? CustomerAddressId { get; set; }

    /// <summary>Override / free-text address snapshot for open Takeaway/Delivery orders.</summary>
    public string? AddressText { get; set; }
}
