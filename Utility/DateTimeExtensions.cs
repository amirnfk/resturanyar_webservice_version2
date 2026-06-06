namespace resturanyar.Utility
{
    public static class DateTimeExtensions
    {
        public static string ToRelativeTime(this DateTime dateTime)
        {
            var diff = DateTime.Now - dateTime;

            if (diff.TotalMinutes < 1)
                return "لحظاتی پیش";

            if (diff.TotalMinutes < 60)
            {
                int minutes = (int)diff.TotalMinutes;
                if (minutes == 30) return "نیم ساعت پیش";
                return $"{minutes} دقیقه پیش";
            }

            if (diff.TotalHours < 24)
            {
                int hours = (int)diff.TotalHours;
                if (hours == 1) return "یک ساعت پیش";
                if (hours == 2) return "دو ساعت پیش";
                return $"{hours} ساعت پیش";
            }

            if (diff.TotalDays < 7)
            {
                int days = (int)diff.TotalDays;
                if (days == 1) return "دیروز";
                return $"{days} روز پیش";
            }

            // اگر بیشتر از یک هفته باشه تاریخ کامل نشون بده
            return dateTime.ToString("yyyy/MM/dd HH:mm");
        }
    }
}
