using System;

namespace NewRelic.Agent.Core.DataTransport
{
	public class CollectorRequestPayload
	{
		public Boolean IsCompressed { get; set; }
		public String CompressionType { get; }
		public Byte[] Data { get; }

		public CollectorRequestPayload(Boolean isCompressed, String compressionType, Byte[] data)
		{
			IsCompressed = isCompressed;
			CompressionType = compressionType;
			Data = data;
		}
	}
}
