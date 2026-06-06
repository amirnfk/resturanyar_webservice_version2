namespace resturanyar.Models
{
    public class OrderFilterModel
    {
        public int? StatusId { get; set; }       // وضعیت فیلتر شده (مثلاً 6، 7، 8 یا 0 برای پیش‌فرض)
        public DateTime? From { get; set; }      // تاریخ شروع بازه فیلتر
        public DateTime? To { get; set; }        // تاریخ پایان بازه فیلتر
        public string? Search { get; set; }      // متن جستجو
        public int? Page { get; set; }           // شماره صفحه برای صفحه‌بندی
        public int? PageSize { get; set; }       // تعداد آیتم‌ها در هر صفحه
        public string? Period { get; set; }      // دوره زمانی کوتاه شده (today, week, month)
    }

}
