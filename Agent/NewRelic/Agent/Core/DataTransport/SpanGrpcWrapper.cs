using NewRelic.Agent.Core.Segments;
using Grpc.Core;
using System.Threading;

namespace NewRelic.Agent.Core.DataTransport
{
	public class SpanGrpcWrapper : GrpcWrapper<Span, RecordStatus>, IGrpcWrapper<Span, RecordStatus>
	{
		protected override AsyncDuplexStreamingCall<Span, RecordStatus> CreateStreamsImpl(Metadata headers, CancellationToken cancellationToken)
		{
			if (_channel == null)
			{
				throw new GrpcWrapperChannelNotAvailableException();
			}
			
			var client = new IngestService.IngestServiceClient(_channel);
			var streams = client.RecordSpan(headers: headers, cancellationToken: cancellationToken);

			return streams;
		}
	}

}
