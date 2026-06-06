namespace resturanyar.Models
{
    public class OrderItem
    {
        public int OrderItemId { get; set; }
        public int OrderId { get; set; }

        public int FoodItemId { get; set; }
        public Order Order { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? UnitPriceWithDiscount { get; set; }

        public string? FoodName { get; set; }          
        public string? FoodImageUrl { get; set; }      

        public decimal TotalPrice => Quantity * UnitPrice;
    }
}
