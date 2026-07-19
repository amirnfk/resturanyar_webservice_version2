namespace resturanyar.Models.ViewModels.DashboardStat
{
    public class TopFoodDto
    {
        public int FoodItemId { get; set; }
        public string FoodName { get; set; }
        public string ImageUrl { get; set; }
        public int TotalQuantity { get; set; }
    }

    public class DashboardStatsViewModel
    {
        public string RestaurantName { get; set; }

        // بخش پرفروش‌ترین
        public List<TopFoodDto> TopFoods { get; set; }

        // بخش وضعیت سفارش‌ها
        public int TotalOrdersToday { get; set; }
        public int WaiterOrdersCount { get; set; }
        public int ChefOrdersCount { get; set; }
        public int CashierOrdersCount { get; set; }
        public int OrdersChangeCount { get; set; }
        public double OrdersChangePercent { get; set; }

        // بخش فروش
        public decimal TodayRevenue { get; set; }
        public decimal RevenueChange { get; set; }
        public double RevenueChangePercent { get; set; }

        // (اختیاری) اطلاعات قبلی
        public int UsersCount { get; set; }
        public int MenuItemsCount { get; set; }
        public int OrdersTodayCount { get; set; }
        public string PublicMenuToken { get; set; }

        public string PrimaryColor { get; set; } = "#f97316";
        public string SecondaryColor { get; set; } = "#fff7ed";
        public string LogoUrl { get; set; } = "/images/logo.png";
        public string BackgroundImageUrl { get; set; } = "/images/backgrounds/preset-warm.jpg";
    }
}
