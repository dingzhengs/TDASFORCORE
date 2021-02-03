using System;

namespace TDASDataParser.Utils
{
    public static class DateHelper
    {
        public static DateTime GetDate(double timestamp, int length = 10)
        {
            DateTime startTime = TimeZoneInfo.ConvertTime(new DateTime(1970, 1, 1, 0, 0, 0, 0), TimeZoneInfo.Local);

            return length == 10 ? startTime.AddSeconds(timestamp) : startTime.AddMilliseconds(timestamp);
        }

        public static double GetTimestamp(DateTime date, int length = 10)
        {
            DateTime startTime = TimeZoneInfo.ConvertTime(new DateTime(1970, 1, 1, 0, 0, 0, 0), TimeZoneInfo.Local);

            return length == 10 ? (date.Ticks - startTime.Ticks) / 10000000 : (date.Ticks - startTime.Ticks) / 10000;
        }
    }
}
