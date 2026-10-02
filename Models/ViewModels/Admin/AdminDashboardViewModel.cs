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

        // KPIهای تصمیم‌گیری
        public int PaidSubscriptionsCount { get; set; }
        public decimal ThisMonthRevenue { get; set; }
        public decimal LastMonthRevenue { get; set; }
        public decimal? RevenueGrowthPercent { get; set; }
        public int ThisMonthNewPaid { get; set; }
        public int FreeTrialActiveCount { get; set; }
        public int ExpiredRestaurantsCount { get; set; }
        public int NoSubscriptionCount { get; set; }
        public int ActiveRestaurantsCount { get; set; }

        public List<RestaurantStatusViewModel> Restaurants { get; set; } = new();
        public List<OwnerSummaryViewModel> Owners { get; set; } = new();
        public List<ExpiringSubscriptionViewModel> ExpiringSubscriptions { get; set; } = new();
        public List<RecentSubscriptionViewModel> RecentSubscriptions { get; set; } = new();
        public List<MonthlyStatsViewModel> MonthlyStats { get; set; } = new();
    }

    public class SubscriptionStatsViewModel
    {
        public string PlanName { get; set; }
        public string PaymentMethod { get; set; }
        public int Count { get; set; }
        public decimal TotalRevenue { get; set; }
        public string DisplayName { get; set; }
        public string Color { get; set; }
    }

    public class MonthlyStatsViewModel
    {
        public string Label { get; set; }
        public decimal Revenue { get; set; }
        public int NewSubscriptions { get; set; }
    }

    public class RestaurantStatusViewModel
    {
        public int RestaurantId { get; set; }
        public string Name { get; set; }
        public string OwnerName { get; set; }
        public string OwnerPhone { get; set; }
        public string SubscriptionStatus { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }
        public string PlanName { get; set; }
        public int TotalOrders { get; set; }
        public int TotalSubscriptions { get; set; }
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
        public string PaymentMethod { get; set; }
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
        public string PaymentMethod { get; set; }
    }
}
