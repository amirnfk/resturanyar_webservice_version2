namespace resturanyar.Models.Copoun
{
    public class ValidateCouponRequest
    {
        public string Code { get; set; }
        public int PlanId { get; set; }
        public int RestaurantId { get; set; }
        public decimal BaseAmount { get; set; }
    }
}
