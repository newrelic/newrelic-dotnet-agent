using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
	public class BasicMvcApplication : RemoteApplicationFixture
	{
		public const String ExpectedTransactionName = @"WebTransaction/MVC/DefaultController/Index";

		[CanBeNull]
		public String ResponseBody { get; private set; }

		public BasicMvcApplication() : base(new RemoteWebApplication("BasicMvcApplication", ApplicationType.Bounded))
		{
		}

		public void GetHttpClient()
		{
			var address = $"http://{DestinationServerName}:{Port}/Default/HttpClient";
			var result = new WebClient().DownloadString(address);

			Assert.NotNull(result);
			Assert.Equal("Great success", result);
		}

		public void GetHttpClientTaskCancelled()
		{
			var address = $"http://{DestinationServerName}:{Port}/Default/HttpClientTaskCancelled";
			var result = new WebClient().DownloadString(address);

			Assert.NotNull(result);
			Assert.Equal("Great success", result);
		}
		public void GetRestSharpSyncClient(String method, Boolean generic)
		{
			var address = $"http://{DestinationServerName}:{Port}/RestSharp/SyncClient?method={method}&generic={generic}";
			var result = new WebClient().DownloadString(address);

			Assert.NotNull(result);
			Assert.Equal("Huge Success", result);
		}
		public void GetRestSharpAsyncAwaitClient(String method, Boolean generic, Boolean cancelable)
		{
			var address = $"http://{DestinationServerName}:{Port}/RestSharp/AsyncAwaitClient?method={method}&generic={generic}&cancelable={cancelable}";
			var result = new WebClient().DownloadString(address);

			Assert.NotNull(result);
			Assert.Equal("Huge Success", result);
		}

		public HttpResponseHeaders GetRestSharpAsyncAwaitClientWithHeaders(String method, Boolean generic, Boolean cancelable)
		{
			var address = $"http://{DestinationServerName}:{Port}/RestSharp/AsyncAwaitClient?method={method}&generic={generic}&cancelable={cancelable}";

			using (var httpClient = new HttpClient())
			{
				var requestData = new CrossApplicationRequestData("guid", false, "tripId", "pathHash");

				var headers = new List<KeyValuePair<String, String>>
				{
					new KeyValuePair<String, String>("X-NewRelic-ID", GetXNewRelicId()),
					new KeyValuePair<String, String>("X-NewRelic-Transaction", GetXNewRelicRequestData(requestData))
				};

				var httpRequestMessage = new HttpRequestMessage { RequestUri = new Uri(address), Method = HttpMethod.Get };
				foreach (var header in headers)
					httpRequestMessage.Headers.Add(header.Key, header.Value);

				return Task.Run(() => httpClient.SendAsync(httpRequestMessage)).Result.Headers;
			}

		}

		public void GetRestSharpTaskResultClient(String method, Boolean generic, Boolean cancelable)
		{
			var address = $"http://{DestinationServerName}:{Port}/RestSharp/TaskResultClient?method={method}&generic={generic}&cancelable={cancelable}";

			var result = new WebClient().DownloadString(address);

			Assert.NotNull(result);
			Assert.Equal("Huge Success", result);
		}

		public HttpResponseHeaders GetRestSharpTaskResultClientWithHeaders(String method, Boolean generic, Boolean cancelable)
		{
			var address = $"http://{DestinationServerName}:{Port}/RestSharp/TaskResultClient?method={method}&generic={generic}&cancelable={cancelable}";

			using (var httpClient = new HttpClient())
			{
				var requestData = new CrossApplicationRequestData("guid", false, "tripId", "pathHash");

				var headers = new List<KeyValuePair<String, String>>
				{
					new KeyValuePair<String, String>("X-NewRelic-ID", GetXNewRelicId()),
					new KeyValuePair<String, String>("X-NewRelic-Transaction", GetXNewRelicRequestData(requestData))
				};

				var httpRequestMessage = new HttpRequestMessage { RequestUri = new Uri(address), Method = HttpMethod.Get };
				foreach (var header in headers)
					httpRequestMessage.Headers.Add(header.Key, header.Value);

				return Task.Run(() => httpClient.SendAsync(httpRequestMessage)).Result.Headers;
			}

		}


		public void GetRestSharpClientTaskCancelled()
		{
			var address = $"http://{DestinationServerName}:{Port}/RestSharp/RestSharpClientTaskCancelled";
			var result = new WebClient().DownloadString(address);

			Assert.NotNull(result);
			Assert.Equal("Huge success", result);
		}

		public void Get404()
		{
			var address = $"http://{DestinationServerName}:{Port}/DoesNotExist";
			var webClient = new WebClient();

			try
			{
				webClient.DownloadString(address);
			}
			catch (WebException)
			{
				// swallow
			}
		}

		public void GetIgnored()
		{
			var guid = Guid.NewGuid().ToString();
			var address = $"http://{DestinationServerName}:{Port}/Default/Ignored?data={guid}";
			var webClient = new WebClient();
			var result = webClient.DownloadString(address);
			Assert.Equal(guid, result);
		}

		public void GetRouteWithAttribute()
		{
			var address = $"http://{DestinationServerName}:{Port}/foo/bar";
			var webClient = new WebClient();
			webClient.DownloadString(address);
		}

		public void WaitForStartup()
		{
			AgentLog.WaitForConnect(Timing.TimeToConnect);

			var stopwatch = Stopwatch.StartNew();
			while (stopwatch.Elapsed < TimeSpan.FromMinutes(5))
			{
				try
				{
					using (var httpClient = new HttpClient())
					{
						var guid = Guid.NewGuid().ToString();
						var address = $"http://{DestinationServerName}:{Port}/Default/Ignored?data={guid}";
						httpClient.Timeout = TimeSpan.FromSeconds(1);
						Task.Run(async () => Assert.Equal(guid, await httpClient.GetStringAsync(address))).Wait();
						return;
					}
				}
				catch (Exception)
				{
					// try again in a bit, until we get a response in under 1 second (meaning server is up and stable) or 5 minutes passes
					Thread.Sleep(TimeSpan.FromSeconds(5));
				}
			}

			Assert.True(false, @"Did not receive a stable response (less than 1 second) after 5 minutes of attempts every 6 seconds.");
		}

		[NotNull]
		public String Get()
		{
			var address = $"http://{DestinationServerName}:{Port}/Default";
			var webClient = new WebClient();

			ResponseBody = webClient.DownloadString(address);

			Assert.NotNull(ResponseBody);
			Assert.Contains("<html>", ResponseBody);

			return ResponseBody;
		}

		[NotNull]
		public String GetWithAsyncDisabled()
		{
			var address = $"http://{DestinationServerName}:{Port}/DisableAsyncSupport";
			var webClient = new WebClient();

			ResponseBody = webClient.DownloadString(address);

			Assert.NotNull(ResponseBody);
			Assert.Contains("<html>", ResponseBody);

			return ResponseBody;
		}

		public String GetWithData(String requestParameter)
		{
			var address = $"http://{DestinationServerName}:{Port}/Default/Query?data={requestParameter}";
			var webClient = new WebClient();

			var result = webClient.DownloadString(address);

			Assert.NotNull(result);
			Assert.Contains("<html>", result);

			return result;
		}

		[NotNull]
		public String GetNotHtmlContentType()
		{
			var address = $"http://{DestinationServerName}:{Port}/Default/NotHtmlContentType";
			var webClient = new WebClient();

			var result = webClient.DownloadString(address);

			Assert.NotNull(result);
			Assert.Contains("<html>", result);

			return result;
		}

		[NotNull]
		public HttpResponseHeaders GetWithHeaders([NotNull] IEnumerable<KeyValuePair<String, String>> headers, [CanBeNull] String action = null, String queryString = null)
		{
			var address = $"http://{DestinationServerName}:{Port}/Default/{action}{queryString}";

			using (var httpClient = new HttpClient())
			{
				var httpRequestMessage = new HttpRequestMessage { RequestUri = new Uri(address), Method = HttpMethod.Get };
				foreach (var header in headers)
					httpRequestMessage.Headers.Add(header.Key, header.Value);

				return Task.Run(() => httpClient.SendAsync(httpRequestMessage)).Result.Headers;
			}
		}

		[NotNull]
		public HttpResponseHeaders GetWithCatHeader(Boolean includeCrossProcessIdHeader = true, CrossApplicationRequestData requestData = null)
		{
			var headers = new List<KeyValuePair<String, String>>();

			if (includeCrossProcessIdHeader)
				headers.Add(new KeyValuePair<String, String>("X-NewRelic-ID", GetXNewRelicId()));
			if (requestData != null)
				headers.Add(new KeyValuePair<String, String>("X-NewRelic-Transaction", GetXNewRelicRequestData(requestData)));

			return GetWithHeaders(headers, "Index");
		}

		[NotNull]
		public HttpResponseHeaders GetWithCatHeaderWithRedirect()
		{
			var headers = new List<KeyValuePair<String, String>>
			{
				new KeyValuePair<String, String>("X-NewRelic-ID", GetXNewRelicId())
			};

			return GetWithHeaders(headers, "DoRedirect");
		}

		/// <summary>
		/// Makes a request, optionally including CAT headers, to the "Chained" endpoint (which will itself make a request).
		/// </summary>
		[NotNull]
		public HttpResponseHeaders GetWithCatHeaderChained([NotNull] CrossApplicationRequestData requestData)
		{
			var headers = new List<KeyValuePair<String, String>>
			{
				new KeyValuePair<String, String>("X-NewRelic-ID", GetXNewRelicId()),
				new KeyValuePair<String, String>("X-NewRelic-Transaction", GetXNewRelicRequestData(requestData))
			};

			const String action = "Index";
			var queryString = $"?chainedServerName={DestinationServerName}&chainedPortNumber={Port}&chainedAction={action}";
			return GetWithHeaders(headers, "Chained", queryString);
		}

		/// <summary>
		/// Makes a request, optionally including CAT headers, to the "Chained" endpoint (which will itself make a request).
		/// </summary>
		[NotNull]
		public HttpResponseHeaders GetWithCatHeaderChainedHttpClient([NotNull] CrossApplicationRequestData requestData)
		{
			var headers = new List<KeyValuePair<String, String>>
			{
				new KeyValuePair<String, String>("X-NewRelic-ID", GetXNewRelicId()),
				new KeyValuePair<String, String>("X-NewRelic-Transaction", GetXNewRelicRequestData(requestData))
			};

			const String action = "Index";
			var queryString = $"?chainedServerName={DestinationServerName}&chainedPortNumber={Port}&chainedAction={action}";
			return GetWithHeaders(headers, "ChainedHttpClient", queryString);
		}

		[NotNull]
		public HttpResponseHeaders GetWithUntrustedCatHeader()
		{
			var headers = new List<KeyValuePair<String, String>>();
			headers.Add(new KeyValuePair<String, String>("X-NewRelic-ID", GetUntrustedXNewRelicId()));
			return GetWithHeaders(headers, "Index");
		}

		public void GetCustomAttributes(String key1, String value1, String key2, String value2)
		{
			var address = $"http://{DestinationServerName}:{Port}/Default/CustomParameters?key1={key1}&value1={value1}&key2={key2}&value2={value2}";
			var webClient = new WebClient();

			ResponseBody = webClient.DownloadString(address);

			Assert.NotNull(ResponseBody);
			Assert.Contains("<html>", ResponseBody);
		}

		public void StartAgent()
		{
			var address = $"http://{DestinationServerName}:{Port}/Default/StartAgent";
			var webClient = new WebClient();

			ResponseBody = webClient.DownloadString(address);

			Assert.NotNull(ResponseBody);
			Assert.Contains("<html>", ResponseBody);
		}

		private String GetXNewRelicId()
		{
			var accountId = AgentLog.GetAccountId();
			var applicationId = AgentLog.GetApplicationId();
			var accountAndApp = $@"{accountId}#{applicationId}";

			return HeaderEncoder.Base64Encode(accountAndApp, HeaderEncoder.IntegrationTestEncodingKey);
		}

		private String GetUntrustedXNewRelicId()
		{
			var invalidAccountId = "999999";
			var applicationId = AgentLog.GetApplicationId();
			var accountAndApp = $@"{invalidAccountId}#{applicationId}";

			return HeaderEncoder.Base64Encode(accountAndApp, HeaderEncoder.IntegrationTestEncodingKey);
		}

		private String GetXNewRelicRequestData([NotNull] CrossApplicationRequestData requestData)
		{
			return HeaderEncoder.SerializeAndEncode(requestData, HeaderEncoder.IntegrationTestEncodingKey);
		}

		public void GetStaticResource()
		{
			var address = $"http://{DestinationServerName}:{Port}/bundles/modernizr?v=wBEWDufH_8Md-Pbioxomt90vm6tJN2Pyy9u9zHtWsPo1";
			var webClient = new WebClient();
			webClient.DownloadString(address);
		}

		[NotNull]
		private static Byte[] EncodeWithKey([NotNull] String value, [NotNull] String key)
		{
			var bytes = Encoding.UTF8.GetBytes(value);
			var keyBytes = Encoding.UTF8.GetBytes(key);

			for (var i = 0; i < bytes.Length; i++)
			{
				bytes[i] = (byte)(bytes[i] ^ keyBytes[i % keyBytes.Length]);
			}

			return bytes;
		}

		public void ThrowException()
		{
			var address = $"http://{DestinationServerName}:{Port}/Default/ThrowException";
			var webClient = new WebClient();
			Assert.Throws<System.Net.WebException>(() => webClient.DownloadString(address));
        }

		public void SimulateLostTransaction()
		{
			var address = $"http://{DestinationServerName}:{Port}/Default/SimulateLostTransaction";
			var webClient = new WebClient();

			ResponseBody = webClient.DownloadString(address);

			Assert.NotNull(ResponseBody);
			Assert.Contains("<html>", ResponseBody);
		}

		public void GetRedis()
		{
			var address = $"http://{DestinationServerName}:{Port}/Redis/Get";
			var result = new WebClient().DownloadString(address);

			Assert.NotNull(result);
			Assert.Equal("Great success", result);
		}

		public void GetCustomInstrumentation()
		{
			var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentation/Get";
			var result = new WebClient().DownloadString(address);

			Assert.NotNull(result);
			Assert.Equal("It am working", result);
		}

        public void GetIgnoredByIgnoreTransactionWrapper()
		{
			var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentation/GetIgnoredByIgnoreTransactionWrapper";
			var result = new WebClient().DownloadString(address);

			Assert.NotNull(result);
			Assert.Equal("It am working", result);
		}

		public void GetIgnoredByIgnoreTransactionTracerFactory()
		{
			var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentation/GetIgnoredByIgnoreTransactionTracerFactory";
			var result = new WebClient().DownloadString(address);

			Assert.NotNull(result);
			Assert.Equal("It am working", result);
		}

		public void GetIgnoredByIgnoreTransactionWrapperAsync()
		{
			var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentation/GetIgnoredByIgnoreTransactionWrapperAsync";
			var result = new WebClient().DownloadString(address);

			Assert.NotNull(result);
			Assert.Equal("It am working", result);
		}

		public void GetCustomInstrumentationAsync()
		{
			var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentationAsync/AsyncGet";
			var result = new WebClient().DownloadString(address);

			Assert.NotNull(result);
			Assert.Equal("Async is working", result);
		}

	    public void GetCustomInstrumentationAsyncCustomSegment()
	    {
            var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentationAsync/AsyncGetCustomSegment";
            var result = new WebClient().DownloadString(address);

            Assert.NotNull(result);
            Assert.Equal("AsyncCustomSegmentName", result);
        }

        public void GetCustomInstrumentationAsyncCustomSegmentAlternateParameterNaming()
        {
            var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentationAsync/AsyncGetCustomSegmentAlternateParameterNaming";
            var result = new WebClient().DownloadString(address);

            Assert.NotNull(result);
            Assert.Equal("AsyncCustomSegmentNameAlternate", result);
        }

        public void GetBackgroundThread()
		{
			var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentationAsync/GetBackgroundThread";
			var result = new WebClient().DownloadString(address);

			Assert.NotNull(result);
			Assert.Equal("Async is working", result);
		}

		public void GetBackgroundThreadWithError()
		{
			var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentationAsync/GetBackgroundThreadWithError";
			var result = new WebClient().DownloadString(address);

			Assert.NotNull(result);
			Assert.Equal("Async is working", result);
		}

		public String GetBrowserTimingHeader()
		{
			var address = $"http://{DestinationServerName}:{Port}/Default/GetBrowserTimingHeader";
			var webClient = new WebClient();

			var response = webClient.DownloadString(address);
			Assert.NotNull(response);

			return response;
		}

		[NotNull]
		public String GetHtmlWithCallToGetBrowserTimingHeader()
		{
			var address = $"http://{DestinationServerName}:{Port}/Default/GetHtmlWithCallToGetBrowserTimingHeader";
			var webClient = new WebClient();

			ResponseBody = webClient.DownloadString(address);

			Assert.NotNull(ResponseBody);
			Assert.Contains("<html>", ResponseBody);

			return ResponseBody;
		}
	}

	public class HSMBasicMvcApplicationTestFixture : BasicMvcApplication
	{
		public override string TestSettingCategory { get { return "HSM"; } }
	}
}
