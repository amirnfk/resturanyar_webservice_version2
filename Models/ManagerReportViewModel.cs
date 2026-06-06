using System;
using System.Collections.Generic;

namespace resturanyar.Models
{
public class ManagerReportViewModel
{
public DateTime? FromDate { get; set; }
public DateTime? ToDate { get; set; }
public string? Period { get; set; }

// KPI ها
public int TotalOrders { get; set; }
public int PaidOrders { get; set; }
public int CancelledOrders { get; set; }
public decimal TotalRevenue { get; set; } // مجموع مبلغ سفارش‌ها (همه وضعیت‌ها)
public decimal PaidRevenue { get; set; } // مجموع مبلغ سفارش‌های پرداخت شده
public decimal AvgOrderValue { get; set; } // میانگین ارزش سبد خرید (PaidRevenue / PaidOrders)
public double AvgItemsPerOrder { get; set; } // میانگین تعداد آیتم در هر سفارش
public double CancelRate { get; set; } // درصد لغو = CancelledOrders / TotalOrders
public double PaidConversionRate { get; set; } // درصد تبدیل به پرداخت = PaidOrders / TotalOrders

// تفکیک وضعیت‌ها
public Dictionary<int, int> StatusCounts { get; set; } = new();

// سری زمانی فروش
public List<SalesPointDto> SalesByDay { get; set; } = new();

// آیتم‌های برتر
public List<TopItemDto> TopItemsByQuantity { get; set; } = new();
public List<TopItemDto> TopItemsByRevenue { get; set; } = new();

// نگاشت وضعیت‌ها برای نمایش
public Dictionary<int, string> StatusMap { get; set; } = new();
public Dictionary<int, string> StatusColors { get; set; } = new();

// اندازه خروجی‌های نمودار/لیست
public int TopN { get; set; } = 8;
}

public class SalesPointDto
{
public DateTime Day { get; set; }
public decimal Revenue { get; set; }
public int Orders { get; set; }
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