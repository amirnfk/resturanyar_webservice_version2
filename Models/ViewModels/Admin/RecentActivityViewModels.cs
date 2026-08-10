using System;
using System.Collections.Generic;

namespace resturanyar.Models.ViewModels.Admin
{
    public static class ActivityFeedTypes
    {
        public const string OrderCreated = "OrderCreated";
        public const string SubscriptionPurchased = "SubscriptionPurchased";
        public const string RestaurantCreated = "RestaurantCreated";
        public const string SupportActivity = "SupportActivity";
        public const string FoodCreated = "FoodCreated";
        public const string CustomerCreated = "CustomerCreated";
        public const string OwnerRegistered = "OwnerRegistered";
        public const string CouponUsed = "CouponUsed";
        public const string InventoryMovement = "InventoryMovement";
        public const string OwnerLogin = "OwnerLogin";
        public const string StaffLogin = "StaffLogin";
    }

    public class RecentActivityPageViewModel
    {
        public List<ActivityFeedItem> Orders { get; set; } = new();
        public List<ActivityFeedItem> Subscriptions { get; set; } = new();
        public List<ActivityFeedItem> Restaurants { get; set; } = new();
        public List<ActivityFeedItem> Support { get; set; } = new();
        public List<ActivityFeedItem> Foods { get; set; } = new();
        public List<ActivityFeedItem> Customers { get; set; } = new();
        public List<ActivityFeedItem> Owners { get; set; } = new();
        public List<ActivityFeedItem> CouponUsages { get; set; } = new();
        public List<ActivityFeedItem> InventoryMovements { get; set; } = new();
        public List<ActivityFeedItem> OwnerLogins { get; set; } = new();
        public List<ActivityFeedItem> StaffLogins { get; set; } = new();
    }

    public class ActivityFeedItem
    {
        public string Type { get; set; }
        public long EntityId { get; set; }
        public int? RestaurantId { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public DateTime OccurredAt { get; set; }
        public string BadgeLabel { get; set; }
        public string BadgeClass { get; set; }
        public string IconClass { get; set; }
    }

    public class ActivityOrderDetailDto
    {
        public int OrderId { get; set; }
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        public string TableNumber { get; set; }
        public string StatusName { get; set; }
        public string OrderTypeLabel { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CustomerName { get; set; }
        public string CustomerMobile { get; set; }
        public List<ActivityOrderItemDto> Items { get; set; } = new();
        public decimal ItemsTotal { get; set; }
    }

    public class ActivityOrderItemDto
    {
        public string FoodName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class ActivitySubscriptionDetailDto
    {
        public int SubscriptionId { get; set; }
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        public string OwnerName { get; set; }
        public string OwnerPhone { get; set; }
        public string PlanName { get; set; }
        public string Period { get; set; }
        public string Status { get; set; }
        public decimal PricePaid { get; set; }
        public decimal? DiscountApplied { get; set; }
        public string PaymentMethod { get; set; }
        public string TransactionId { get; set; }
        public DateTime PurchaseDate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class ActivityRestaurantDetailDto
    {
        public int RestaurantId { get; set; }
        public string Name { get; set; }
        public string RestaurantCode { get; set; }
        public string OwnerName { get; set; }
        public string OwnerPhone { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool ReceiptChargesEnabled { get; set; }
    }

    public class ActivitySupportDetailDto
    {
        public long ConversationId { get; set; }
        public int? RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        public string OwnerName { get; set; }
        public string OwnerPhone { get; set; }
        public int UnreadBySupport { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime LastMessageAtUtc { get; set; }
        public string LastMessagePreview { get; set; }
        public string LastPageUrl { get; set; }
    }

    public class ActivityFoodDetailDto
    {
        public int FoodItemId { get; set; }
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        public string Name { get; set; }
        public string CategoryName { get; set; }
        public decimal Price { get; set; }
        public decimal? DiscountPrice { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ImageUrl { get; set; }
    }

    public class ActivityCustomerDetailDto
    {
        public int CustomerId { get; set; }
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        public string FullName { get; set; }
        public string Mobile { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Description { get; set; }
    }

    public class ActivityOwnerDetailDto
    {
        public int OwnerId { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public int RestaurantCount { get; set; }
    }

    public class ActivityCouponUsageDetailDto
    {
        public int UsageId { get; set; }
        public string CouponCode { get; set; }
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        public string OwnerName { get; set; }
        public string OwnerPhone { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal AppliedPrice { get; set; }
        public string Status { get; set; }
        public DateTime UsedAt { get; set; }
        public int SubscriptionId { get; set; }
    }

    public class ActivityInventoryDetailDto
    {
        public int MovementId { get; set; }
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        public string ItemName { get; set; }
        public string Reason { get; set; }
        public string ReasonLabel { get; set; }
        public decimal DeltaQuantity { get; set; }
        public decimal QuantityAfter { get; set; }
        public decimal? UnitPrice { get; set; }
        public string Note { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ActivityOwnerLoginDetailDto
    {
        public int TokenId { get; set; }
        public int OwnerId { get; set; }
        public string OwnerName { get; set; }
        public string OwnerPhone { get; set; }
        public DateTime? EstimatedLoginAt { get; set; }
        public DateTime ExpiryTime { get; set; }
    }

    public class ActivityStaffLoginDetailDto
    {
        public int TokenId { get; set; }
        public int UserId { get; set; }
        public string StaffName { get; set; }
        public string RoleName { get; set; }
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiryTime { get; set; }
    }
}
