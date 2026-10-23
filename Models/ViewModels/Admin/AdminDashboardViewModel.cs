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

        /// <summary>Positive-change pulse for last 24h / 7 days (restaurants + purchases).</summary>
        public AdminRecentPulseViewModel RecentPulse { get; set; } = new();

        public List<RestaurantStatusViewModel> Restaurants { get; set; } = new();
        public List<OwnerSummaryViewModel> Owners { get; set; } = new();
        public List<ExpiringSubscriptionViewModel> ExpiringSubscriptions { get; set; } = new();
        public List<RecentSubscriptionViewModel> RecentSubscriptions { get; set; } = new();
        public List<MonthlyStatsViewModel> MonthlyStats { get; set; } = new();
    }

    public class AdminRecentPulseViewModel
    {
        public DateTime GeneratedAt { get; set; }

        public int NewRestaurants24h { get; set; }
        public int NewRestaurants7d { get; set; }

        public int PaidPurchases24h { get; set; }
        public int PaidPurchases7d { get; set; }
        public decimal PaidRevenue24h { get; set; }
        public decimal PaidRevenue7d { get; set; }

        public int FreeTrial24h { get; set; }
        public int FreeTrial7d { get; set; }

        public int TotalPurchases24h { get; set; }
        public int TotalPurchases7d { get; set; }

        public List<AdminPulseBreakdownItem> Plans24h { get; set; } = new();
        public List<AdminPulseBreakdownItem> Plans7d { get; set; } = new();
        public List<AdminPulseBreakdownItem> Methods24h { get; set; } = new();
        public List<AdminPulseBreakdownItem> Methods7d { get; set; } = new();
    }

    public class AdminPulseBreakdownItem
    {
        public string Label { get; set; } = "";
        public int Count { get; set; }
        public int PaidCount { get; set; }
        public decimal Revenue { get; set; }
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
