using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace NewRelic.Agent.Core.Utilization
{
	public class VendorHttpApiRequestor
	{
		private const int WebReqeustTimeout = 1000;

		public virtual string CallVendorApi(Uri uri, IEnumerable<string> headers = null)
		{
			try
			{
				var request = WebRequest.Create(uri);
				request.Method = "GET";
				request.Timeout = WebReqeustTimeout;

				if (headers != null)
				{
					foreach (var header in headers)
					{
						request.Headers.Add(header);
					}
				}

				using (var response = request.GetResponse() as HttpWebResponse)
				{
					var stream = response?.GetResponseStream();
					if (stream == null)
						return null;

					var reader = new StreamReader(stream);

					return reader.ReadToEnd();
				}
			}
			catch
			{
				return null;
			}
		}
	}
}