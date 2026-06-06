namespace resturanyar.Models
{
    public class DashboardStatsViewModel
    {
        public string RestaurantName { get; set; }
        public int UsersCount { get; set; }
        public int MenuItemsCount { get; set; }
        public int OrdersTodayCount { get; set; }
        public string PublicMenuToken { get; set; } // اگر نیاز دارید
    }
}
