using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace NewRelic.Agent.IntegrationTests.Shared.Web
{
	public class StreamMediaTypeFormatter : MediaTypeFormatter
	{
		public StreamMediaTypeFormatter()
		{
			this.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/octet-stream"));
		}

		public override Boolean CanReadType(Type type)
		{
			return typeof(Stream) == type;
		}

		public override Boolean CanWriteType(Type type)
		{
			return false;
		}

		public override Task<Object> ReadFromStreamAsync(Type type,
			Stream readStream, HttpContent content, IFormatterLogger formatterLogger)
		{
			return Task.FromResult((Object)readStream);
		}

		public override Task<Object> ReadFromStreamAsync(Type type, Stream readStream,
			HttpContent content, IFormatterLogger formatterLogger, System.Threading.CancellationToken cancellationToken)
		{
			return ReadFromStreamAsync(type, readStream, content, formatterLogger);
		}
	}
}
