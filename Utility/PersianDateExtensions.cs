using System.Globalization;

namespace resturanyar.Utility
{
    public static class PersianDateExtensions
    {
        private static readonly char[] PersianDigits = new[] { '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹' };

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

        public static string ToPersianDate(this DateTime dt)
        {
            var pc = new PersianCalendar();
            string s = $"{pc.GetYear(dt):0000}/{pc.GetMonth(dt):00}/{pc.GetDayOfMonth(dt):00}";
            return s.ToPersianDigits();
        }
    }
}