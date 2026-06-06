namespace resturanyar.Models
{
    public class GenericResponse
    {
        public bool Success { get; set; }   // وضعیت موفقیت یا شکست عملیات
        public string Message { get; set; } // پیام خطا یا موفقیت
    }
}
