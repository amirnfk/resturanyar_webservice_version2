namespace resturanyar.Utility
{
    // PersianDateTime.cs
    public class PersianDateTime
    {
        private readonly System.Globalization.PersianCalendar _persianCalendar = new();
        private readonly DateTime _dateTime;

        public PersianDateTime(DateTime dateTime)
        {
            _dateTime = dateTime;
        }

        public string ToString(string format)
        {
            return format.Replace("yyyy", _persianCalendar.GetYear(_dateTime).ToString("0000"))
                        .Replace("MM", _persianCalendar.GetMonth(_dateTime).ToString("00"))
                        .Replace("dd", _persianCalendar.GetDayOfMonth(_dateTime).ToString("00"))
                        .Replace("HH", _dateTime.Hour.ToString("00"))
                        .Replace("mm", _dateTime.Minute.ToString("00"))
                        .Replace("ss", _dateTime.Second.ToString("00"));
        }
    }
}
