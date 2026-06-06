namespace resturanyar.Models
{
    public class OrderItemDto
    {
        public int OrderItemId { get; set; }
        public int FoodItemId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? UnitPriceWithDiscount { get; set; }

        public string? FoodName { get; set; }         // جدید
        public string? FoodImageUrl { get; set; }     // جدید

        public decimal FinalUnitPrice =>
     UnitPriceWithDiscount.HasValue && UnitPriceWithDiscount.Value > 0
         ? UnitPriceWithDiscount.Value
         : UnitPrice;

        public decimal TotalPrice => FinalUnitPrice * Quantity;


    }
}
