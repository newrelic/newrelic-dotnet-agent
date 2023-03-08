using System.IO;
using Serilog.Events;
using Serilog.Formatting;

namespace NewRelic.Agent.Core
{
    class CustomAuditLogTextFormatter : ITextFormatter
    {
        public void Format(LogEvent logEvent, TextWriter output)
        {
            logEvent.Properties.TryGetValue("Message", out var message);

            output.Write($"{logEvent.Timestamp.ToUniversalTime():yyyy-MM-dd HH:mm:ss,fff} NewRelic  AUDIT: {message}{output.NewLine}");
        }
    }
}