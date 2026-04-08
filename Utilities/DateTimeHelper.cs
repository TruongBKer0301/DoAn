namespace LapTopBD.Utilities
{
    public static class DateTimeHelper
    {
        private const string VietnamTimeZoneId = "SE Asia Standard Time";

        public static DateTime Now
        {
            get
            {
                try
                {
                    var timeZone = TimeZoneInfo.FindSystemTimeZoneById(VietnamTimeZoneId);
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
                }
                catch
                {
                    return DateTime.UtcNow.AddHours(7);
                }
            }
        }
    }
}