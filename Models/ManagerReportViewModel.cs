using System;
using System.Collections.Generic;

namespace resturanyar.Models
{
    public class ManagerReportViewModel
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Period { get; set; }
        public bool IsCustomRange { get; set; }
        public int FilterStatusId { get; set; } = -1;

        // KPI ها
        public int TotalOrders { get; set; }
        public int PaidOrders { get; set; }
        public int CancelledOrders { get; set; }

        /// <summary>جمع فاکتور: snapshot GrandTotal در صورت وجود، وگرنه جمع اقلام.</summary>
        public decimal TotalRevenue { get; set; }
        public decimal PaidRevenue { get; set; }

        /// <summary>فروش غذا/اقلام (از ItemsSubtotal فاکتور یا جمع آیتم‌ها).</summary>
        public decimal ItemsRevenue { get; set; }
        public decimal FeesTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal DiscountTotal { get; set; }

        public decimal AvgOrderValue { get; set; }
        public double AvgItemsPerOrder { get; set; }
        public double CancelRate { get; set; }
        public double PaidConversionRate { get; set; }

        public int IssuedReceiptCount { get; set; }
        public int OrdersWithChargesCount { get; set; }

        public List<ChargeBreakdownItemDto> ChargeBreakdown { get; set; } = new();

        // مقایسه با دوره قبل
        public bool HasPreviousPeriodComparison { get; set; }
        public int PrevTotalOrders { get; set; }
        public decimal PrevTotalRevenue { get; set; }
        public decimal PrevAvgOrderValue { get; set; }
        public double? OrdersChangePercent { get; set; }
        public double? RevenueChangePercent { get; set; }
        public double? AovChangePercent { get; set; }

        public Dictionary<int, int> StatusCounts { get; set; } = new();
        public List<SalesPointDto> SalesByDay { get; set; } = new();
        public List<HourlySalesPointDto> SalesByHour { get; set; } = new();

        public List<TopItemDto> TopItemsByQuantity { get; set; } = new();
        public List<TopItemDto> TopItemsByRevenue { get; set; } = new();

        public Dictionary<int, string> StatusMap { get; set; } = new();
        public Dictionary<int, string> StatusColors { get; set; } = new();

        public int TopN { get; set; } = 8;
    }

    public class ChargeBreakdownItemDto
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class SalesPointDto
    {
        public DateTime Day { get; set; }
        public decimal Revenue { get; set; }
        public int Orders { get; set; }
    }

    public class HourlySalesPointDto
    {
        public int Hour { get; set; }
        public int Orders { get; set; }
        public decimal Revenue { get; set; }
    }

    public class TopItemDto
    {
        public int FoodItemId { get; set; }
        public string? Name { get; set; }
        public string? ImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
    }
}
