using System;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class Utilities
    {
#if DEBUG
        public static String Configuration = "Debug";
#else
        public static String Configuration = "Release";
#endif

        public static T ThrowIfNull<T>(T value, String valueName)
        {
            if (value == null)
                throw new ArgumentNullException(valueName);

            return value;
        }
    }
}
