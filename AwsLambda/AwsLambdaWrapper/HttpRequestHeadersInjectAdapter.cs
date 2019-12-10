using OpenTracing;
using OpenTracing.Propagation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http.Headers;

namespace NewRelic.OpenTracing.AmazonLambda
{
	internal class HttpRequestHeadersInjectAdapter : ITextMap
	{
		private readonly HttpRequestHeaders _requestHeaders;

		public HttpRequestHeadersInjectAdapter(HttpRequestHeaders requestHeaders)
		{
			_requestHeaders = requestHeaders;
		}

		public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
		{
			throw new NotSupportedException(
				$"{nameof(HttpRequestHeadersInjectAdapter)} should only be used with {nameof(ITracer)}.{nameof(ITracer.Inject)}");
		}

		public void Set(string key, string value)
		{
			if (!_requestHeaders.Contains(key))
			{
				_requestHeaders.Add(key, value);
			}
			else
			{
				Logger.Log(message: "New Relic key already exists in HttpRequestHeaders collection.", rawLogging: false, level: "DEBUG");
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
