namespace resturanyar.Models
{
    public class FoodItem
    {
        public int FoodItemId { get; set; }
        public int RestaurantId { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public string ImageUrl { get; set; }
        public int CategoryId { get; set; }  // Foreign Key to Categories
        public string? CategoryName { get; set; }  
        
         
        public decimal Price { get; set; }
        public decimal? DiscountPrice { get; set; }
        public decimal? CostPrice { get; set; }
        public bool IsAvailable { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public Category Category { get; set; }

        public DateTime? CreatedAt { get; set; } = DateTime.Now;

      
    }

}
