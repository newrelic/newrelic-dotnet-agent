using System;

namespace NewRelic.SystemInterfaces
{
    public interface IDateTimeStatic
    {
        DateTime Now { get; }

        DateTime UtcNow { get; }
    }

    public class DateTimeStatic : IDateTimeStatic
    {
        public DateTime Now { get { return DateTime.Now; } }

        public DateTime UtcNow { get { return DateTime.UtcNow; } }
    }
}
