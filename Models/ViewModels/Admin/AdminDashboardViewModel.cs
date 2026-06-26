namespace resturanyar.Models.ViewModels.Admin
{
    using System;
    using System.Collections.Generic;

    
        public class AdminDashboardViewModel
        {
            // آمار کلی
            public int TotalRestaurants { get; set; }
            public int TotalOwners { get; set; }
            public int TotalOrders { get; set; }
            public int TotalSubscriptions { get; set; }
            public int ActiveSubscriptions { get; set; }
            public decimal TotalRevenue { get; set; }

            // لیست رستوران‌ها با وضعیت اشتراک
            public List<RestaurantStatusViewModel> Restaurants { get; set; }

            // لیست مالک‌ها
            public List<OwnerSummaryViewModel> Owners { get; set; }

            // اشتراک‌های در حال انقضا (کمتر از 7 روز)
            public List<ExpiringSubscriptionViewModel> ExpiringSubscriptions { get; set; }

            // آخرین اشتراک‌های خریداری شده
            public List<RecentSubscriptionViewModel> RecentSubscriptions { get; set; }
        public List<MonthlyStatsViewModel> MonthlyStats { get; set; }



    }
    // در فایل AdminDashboardViewModel.cs اضافه کنید

    // در فایل AdminDashboardViewModel.cs اضافه کنید

    public class SubscriptionStatsViewModel
    {
        public string PlanName { get; set; }
        public string PaymentMethod { get; set; }
        public int Count { get; set; }
        public decimal TotalRevenue { get; set; }
        public string DisplayName { get; set; }
        public string Color { get; set; } // برای رنگ‌های نمودار
    }
    public class MonthlyStatsViewModel
    {
        public string Label { get; set; }  // مثلاً "1404/01"
        public decimal Revenue { get; set; }
        public int NewSubscriptions { get; set; }
    }

    public class RestaurantStatusViewModel
        {
            public int RestaurantId { get; set; }
            public string Name { get; set; }
            public string OwnerName { get; set; }
            public string OwnerPhone { get; set; }
            public string SubscriptionStatus { get; set; } // Active, Expired, None
            public DateTime? SubscriptionEndDate { get; set; }
            public string PlanName { get; set; }
            public int TotalOrders { get; set; }
        public int TotalSubscriptions { get; set; }  // تعداد کل اشتراک‌های خریداری شده
    }

        public class OwnerSummaryViewModel
        {
            public int OwnerId { get; set; }
            public string Name { get; set; }
            public string Phone { get; set; }
            public int RestaurantCount { get; set; }
            public int ActiveSubscriptionCount { get; set; }
            public decimal TotalSpent { get; set; }
            public DateTime? LastPurchaseDate { get; set; }
        }

        public class ExpiringSubscriptionViewModel
        {
            public int SubscriptionId { get; set; }
            public string RestaurantName { get; set; }
            public string OwnerName { get; set; }
            public string PlanName { get; set; }
            public DateTime EndDate { get; set; }
            public int DaysLeft { get; set; }
        public string PaymentMethod { get; set; } // اضافه شود

    }

    public class RecentSubscriptionViewModel
        {
            public int SubscriptionId { get; set; }
            public string RestaurantName { get; set; }
            public string OwnerName { get; set; }
            public string PlanName { get; set; }
            public decimal PricePaid { get; set; }
            public DateTime PurchaseDate { get; set; }
            public string Status { get; set; }
        public string PaymentMethod { get; set; } // اضافه شود



    }


}
