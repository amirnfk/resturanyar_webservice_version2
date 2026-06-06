namespace resturanyar.Models
{
    public class SubscriptionPlan
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string? CafeBazarCodeMonthly { get; set; }
        public string? CafeBazarCode3Monthly { get; set; }
        public string? CafeBazarCode6Monthly { get; set; }
        public string? CafeBazarCode12Monthly { get; set; }

        public string? Description { get; set; }

        public int EmployeeLimit { get; set; }
        public int FoodLimit { get; set; }
        public int CategoryLimit { get; set; }
        public int TableLimit { get; set; }

        public bool CanUseWeb { get; set; }
        public bool CanUsePrinter { get; set; }
        public bool CanShareMenu { get; set; }
        public bool CanUseGoftino { get; set; }
        public bool CanUseSocialChat { get; set; }
        public bool CanUseRealtime { get; set; }
        public bool CanManageUsers { get; set; }
        public bool CanAddImages { get; set; }
        public bool CanManageMultipleRestaurants { get; set; }
        public bool CanAccessReports { get; set; }
        public bool CanManageTables { get; set; }
        public bool CanManageCategories { get; set; }

        public decimal PriceMonthly { get; set; }
        public decimal Price3Monthly { get; set; }
        public decimal Price6Monthly { get; set; }
        public decimal Price12Monthly { get; set; }

        public decimal? DiscountPriceMonthly { get; set; }
        public decimal? DiscountPrice3Monthly { get; set; }
        public decimal? DiscountPrice6Monthly { get; set; }
        public decimal? DiscountPrice12Monthly { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public virtual ICollection<Subscription> Subscriptions { get; set; }

    }

}
