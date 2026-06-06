namespace resturanyar.Models
{
  
        public class CategoryViewModel
        {
            public int CategoryId { get; set; }
            public int RestaurantId { get; set; }
            public string CategoryName { get; set; } = "";
            public string CreatedAt { get; set; } = "";
        }
  
}
