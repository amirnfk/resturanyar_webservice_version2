namespace resturanyar.Models
{
    public class CreateOrderItemRequest
    {
        public int FoodItemId { get; set; }
        public string? FoodName { get; set; }
        public int Quantity { get; set; }
     
        public decimal UnitPrice { get; set; }

           
        public string? FoodImageUrl { get; set; }     
    }
}
