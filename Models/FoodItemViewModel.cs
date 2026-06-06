namespace resturanyar.Models
{
    public class FoodItemViewModel
    {
        public int FoodItemId { get; set; }
        public int RestaurantId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public int CategoryId { get; set; }  // Foreign Key to Categories
        public string CategoryName { get; set; }  // Foreign Key to Categories

        public decimal Price { get; set; }
        public decimal DiscountPrice { get; set; }
        public decimal CostPrice { get; set; }
        public bool IsAvailable { get; set; }
        public string CreatedAt { get; set; }
    }
}
