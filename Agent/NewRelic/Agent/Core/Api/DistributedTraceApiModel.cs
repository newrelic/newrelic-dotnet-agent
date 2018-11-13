using System;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Utils;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Api
{
	public class DistributedTraceApiModel : IDistributedTraceApiModel
	{
		public static readonly DistributedTraceApiModel EmptyModel = new DistributedTraceApiModel(string.Empty);

		private readonly Lazy<string> _text;
		private bool _isEmpty = true;
		private string _httpSafe = string.Empty;

		public DistributedTraceApiModel(string encodedPayload)
		{
			_httpSafe = encodedPayload;
			_text = new Lazy<string>(DecodePayload);
			_isEmpty = string.IsNullOrEmpty(_httpSafe);

			string DecodePayload()
			{
				try
				{
					using (new IgnoreWork())
					{
						return Strings.Base64Decode(encodedPayload);
					}
				}
				catch (Exception ex)
				{
					try
					{
						Log.ErrorFormat("Failed to get DistributedTraceApiModel.Text: {0}", ex);
					}
					catch (Exception)
					{
						//Swallow the error
					}
					return string.Empty;
				}
			}
		}

		public string HttpSafe()
		{
			return _httpSafe;
		}

		public string Text()
		{
			return _text.Value;
		}

		public bool IsEmpty()
		{
			return _isEmpty;
		}
	}
}