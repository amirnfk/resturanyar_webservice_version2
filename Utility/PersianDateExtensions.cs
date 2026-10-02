using System.Globalization;

namespace resturanyar.Utility
{
    public static class PersianDateExtensions
    {
        private static readonly char[] PersianDigits = new[] { '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹' };
        private static readonly TimeZoneInfo TehranTimeZone = ResolveTehranTimeZone();

        private static TimeZoneInfo ResolveTehranTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time"); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }

            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran"); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }

            // Iran is permanently UTC+03:30 (no DST).
            return TimeZoneInfo.CreateCustomTimeZone(
                "Iran Standard Time",
                TimeSpan.FromHours(3.5),
                "Iran Standard Time",
                "Iran Standard Time");
        }

        /// <summary>
        /// Converts a UTC (or DB Unspecified-as-UTC) timestamp to Tehran local time.
        /// </summary>
        public static DateTime ToTehranTime(this DateTime dt)
        {
            var utc = dt.Kind switch
            {
                DateTimeKind.Utc => dt,
                DateTimeKind.Local => dt.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            };
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TehranTimeZone);
        }

        public static string ToPersianDigits(this string? input)
        {
            if (string.IsNullOrEmpty(input)) return input ?? "";
            var ch = input.ToCharArray();
            for (int i = 0; i < ch.Length; i++)
            {
                if (ch[i] >= '0' && ch[i] <= '9')
                    ch[i] = PersianDigits[ch[i] - '0'];
            }
            return new string(ch);
        }

        public static string ToPersianDateTime(this DateTime dt)
        {
            var pc = new PersianCalendar();
            string s = $"{pc.GetYear(dt):0000}/{pc.GetMonth(dt):00}/{pc.GetDayOfMonth(dt):00} {dt:HH:mm}";
            return s.ToPersianDigits();
        }

        /// <summary>
        /// Formats a UTC issuance timestamp as Persian date/time in Tehran.
        /// </summary>
        public static string ToPersianDateTimeTehran(this DateTime utcDt)
            => utcDt.ToTehranTime().ToPersianDateTime();

        public static string ToPersianDate(this DateTime dt)
        {
            var pc = new PersianCalendar();
            string s = $"{pc.GetYear(dt):0000}/{pc.GetMonth(dt):00}/{pc.GetDayOfMonth(dt):00}";
            return s.ToPersianDigits();
        }

        private static readonly string[] PersianMonthNames =
        {
            "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
            "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
        };

        /// <summary>
        /// First day of the Persian month that contains <paramref name="dt"/>.
        /// </summary>
        public static DateTime GetPersianMonthStart(this DateTime dt)
        {
            var pc = new PersianCalendar();
            var year = pc.GetYear(dt);
            var month = pc.GetMonth(dt);
            return pc.ToDateTime(year, month, 1, 0, 0, 0, 0);
        }

        /// <summary>
        /// Adds Persian calendar months to a date (day clamped to month length).
        /// </summary>
        public static DateTime AddPersianMonths(this DateTime dt, int months)
        {
            var pc = new PersianCalendar();
            var year = pc.GetYear(dt);
            var month = pc.GetMonth(dt) + months;
            var day = pc.GetDayOfMonth(dt);

            while (month > 12) { month -= 12; year++; }
            while (month < 1) { month += 12; year--; }

            var maxDay = pc.GetDaysInMonth(year, month);
            if (day > maxDay) day = maxDay;

            return pc.ToDateTime(year, month, day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond);
        }

        /// <summary>
        /// e.g. "فروردین ۱۴۰۴"
        /// </summary>
        public static string ToPersianMonthLabel(this DateTime dt)
        {
            var pc = new PersianCalendar();
            var year = pc.GetYear(dt);
            var month = pc.GetMonth(dt);
            return $"{PersianMonthNames[month - 1]} {year}".ToPersianDigits();
        }
    }
}