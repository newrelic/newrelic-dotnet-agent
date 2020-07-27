using System;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;

namespace NewRelic.Agent.Core.Utilities
{
    public static class ActionExtensions
    {
        public static void CatchAndLog([NotNull] this Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Error($"An exception occurred while doing some background work: {ex}");
                }
                catch
                {
                }
            }
        }
    }
}
