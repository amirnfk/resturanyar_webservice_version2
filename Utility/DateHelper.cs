using System.Globalization;

namespace resturanyar.Utility
{
    public static class DateHelper
    {
        // تبدیل تاریخ شمسی به میلادی
        public static DateTime ShamsiToDateTime(string shamsiDate)
        {
            try
            {
                var parts = shamsiDate.Split('/');
                if (parts.Length != 3)
                    throw new ArgumentException("فرمت تاریخ شمسی نامعتبر است");

                int year = int.Parse(parts[0]);
                int month = int.Parse(parts[1]);
                int day = int.Parse(parts[2]);

                PersianCalendar pc = new PersianCalendar();
                return pc.ToDateTime(year, month, day, 0, 0, 0, 0);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"خطا در تبدیل تاریخ شمسی: {shamsiDate}", ex);
            }
        }

        // متد قبلی تبدیل به شمسی
        public static string ToShamsi(DateTime date)
        {
            PersianCalendar pc = new PersianCalendar();
            return $"{pc.GetYear(date)}/{pc.GetMonth(date):00}/{pc.GetDayOfMonth(date):00}";
        }
    }
}
