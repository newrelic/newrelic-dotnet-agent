using System;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class Utilities
    {
#if DEBUG
        public static string Configuration = "Debug";
#else
        public static String Configuration = "Release";
#endif

        public static T ThrowIfNull<T>(T value, string valueName)
        {
            if (value == null)
                throw new ArgumentNullException(valueName);

            return value;
        }
    }
}
