namespace resturanyar.Models
{
    // ایجاد یک DTO جدید
    public class FoodItemDto
    {
        public int FoodItemId { get; set; }
        public int RestaurantId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public string? CategoryName { get; set; }
        public int CategoryId { get; set; }
        public decimal Price { get; set; }
        public decimal DiscountPrice { get; set; }
        public decimal CostPrice { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsActive { get; set; }
        public string CreatedAt { get; set; }
    }
}
