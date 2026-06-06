namespace resturanyar.Models
{
    public class GetFoodItemResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public FoodItem? Data { get; set; }
        public int StatusCode { get; set; }
    }
}
