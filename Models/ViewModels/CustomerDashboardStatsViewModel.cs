namespace resturanyar.Models.ViewModels
{
    
        public class CustomerDashboardStatsViewModel
        {
            // مشتریان جدید
            public int NewCustomersToday { get; set; }
            public int NewCustomersThisWeek { get; set; }
            public int NewCustomersThisMonth { get; set; }
            public int TotalActiveCustomers { get; set; }

            // آمار مالی
            public decimal TotalRevenue { get; set; }          // کل فروش از سفارشات بسته شده
            public decimal AverageRevenuePerCustomer { get; set; }
            public decimal AverageOrderValue { get; set; }     // میانگین ارزش هر سفارش
            public int TotalOrders { get; set; }               // تعداد کل سفارشات بسته شده

            // مشتری ویژه (بیشترین خرید)
            public string TopCustomerName { get; set; }
            public decimal TopCustomerTotalSpent { get; set; }
            public int TopCustomerOrders { get; set; }

            // روندهای اخیر (اختیاری برای نمودار)
            public List<DailyCustomerStat> Last7DaysStats { get; set; }
        }

        public class DailyCustomerStat
        {
            public DateTime Date { get; set; }
            public string PersianDate { get; set; }
            public int NewCustomers { get; set; }
            public decimal Revenue { get; set; }
        }
    
}
