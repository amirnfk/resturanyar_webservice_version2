namespace resturanyar.Utility
{
    /// <summary>Converts amounts to Persian words (e.g. for thermal slip totals).</summary>
    public static class PersianMoneyWords
    {
        private static readonly string[] Ones =
        {
            "", "یک", "دو", "سه", "چهار", "پنج", "شش", "هفت", "هشت", "نه",
            "ده", "یازده", "دوازده", "سیزده", "چهارده", "پانزده", "شانزده", "هفده", "هجده", "نوزده"
        };

        private static readonly string[] Tens =
        {
            "", "", "بیست", "سی", "چهل", "پنجاه", "شصت", "هفتاد", "هشتاد", "نود"
        };

        private static readonly string[] Hundreds =
        {
            "", "یکصد", "دویست", "سیصد", "چهارصد", "پانصد", "ششصد", "هفتصد", "هشتصد", "نهصد"
        };

        private static readonly string[] Scales =
        {
            "", "هزار", "میلیون", "میلیارد", "تریلیون"
        };

        public static string ToTomanWords(decimal amount)
        {
            var value = (long)Math.Abs(Math.Round(amount, MidpointRounding.AwayFromZero));
            if (value == 0)
                return "صفر تومان";

            var words = ToWords(value);
            if (string.IsNullOrWhiteSpace(words))
                return "صفر تومان";

            return amount < 0 ? $"منفی {words} تومان" : $"{words} تومان";
        }

        public static string ToWords(long number)
        {
            if (number == 0)
                return "صفر";

            if (number < 0)
                return "منفی " + ToWords(Math.Abs(number));

            var parts = new List<string>();
            var scale = 0;

            while (number > 0 && scale < Scales.Length)
            {
                var chunk = (int)(number % 1000);
                if (chunk != 0)
                {
                    var chunkWords = ThreeDigits(chunk);
                    if (!string.IsNullOrEmpty(Scales[scale]))
                        chunkWords = $"{chunkWords} {Scales[scale]}";
                    parts.Insert(0, chunkWords);
                }

                number /= 1000;
                scale++;
            }

            return string.Join(" و ", parts);
        }

        private static string ThreeDigits(int number)
        {
            var parts = new List<string>();

            var hundred = number / 100;
            var rest = number % 100;

            if (hundred > 0)
                parts.Add(Hundreds[hundred]);

            if (rest > 0)
            {
                if (rest < 20)
                {
                    parts.Add(Ones[rest]);
                }
                else
                {
                    var ten = rest / 10;
                    var one = rest % 10;
                    if (ten > 0)
                        parts.Add(Tens[ten]);
                    if (one > 0)
                        parts.Add(Ones[one]);
                }
            }

            return string.Join(" و ", parts);
        }
    }
}
