using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Core.Errors;

namespace NewRelic.Agent.Core.Segments
{
    public interface IInternalSpan : ISegment, ISegmentExperimental, ISpan
    {
        ErrorData ErrorData { get; set; }
    }
}
