namespace resturanyar.Models
{
    public class EditCategoryRequest
    {
        public int RestaurantId { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
    }
}
