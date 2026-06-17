namespace resturanyar.Models.ViewModels
{
     
        public class CustomerStatsViewModel
        {
            public int CustomerId { get; set; }
            public string FullName { get; set; }
            public string Mobile { get; set; }
            public string Description { get; set; }
            public bool IsActive { get; set; }
            public DateTime CreatedAt { get; set; }
            public string CreatedAtShamsi { get; set; }

        public decimal? LastOrderAmount { get; set; }   // مبلغ آخرین سفارش

        public int TotalOrders { get; set; }           // تعداد کل سفارش‌ها
            public int TotalDistinctDays { get; set; }    // تعداد روزهای متفاوت حضور (بر اساس تاریخ سفارش)
            public decimal TotalSpent { get; set; }       // مجموع مبلغ خرید (با در نظر گرفتن تخفیف)
            public decimal AverageOrderValue { get; set; }// میانگین مبلغ هر سفارش
            public DateTime? LastOrderDate { get; set; }  // آخرین تاریخ سفارش
            public string LastOrderDateShamsi { get; set; }
        }
    }
 
