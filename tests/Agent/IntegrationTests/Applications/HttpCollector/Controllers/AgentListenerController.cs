using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using NewRelic.Agent.IntegrationTests.Shared.Models;

namespace HttpCollector.Controllers
{
	[RoutePrefix("agent_listener")]
	public class AgentListenerController : ApiController
	{
		private static readonly CollectorResponseEnvelope<string> EmptyResponse = new CollectorResponseEnvelope<string>(null, null);

		private static List<CollectedRequest> _collectedRequests = new List<CollectedRequest>();
		private static List<AgentCommand> _queuedCommands = new List<AgentCommand>();


		[HttpGet]
		[Route("WarmUpCollector")]
		public string WarmUpCollector()
		{
			return "All systems go.";
		}

		[HttpGet]
		[HttpPost]
		[HttpPut]
		[Route("invoke_raw_method")]
		public object InvokeRawMethod([FromBody] Stream body)
		{
			var capturedRequest = CaptureRequest(body);
			_collectedRequests.Add(capturedRequest);

			var collectorMethod = capturedRequest.Querystring
				.FirstOrDefault(qs => qs.Key == "method").Value;

			switch (collectorMethod)
			{
				case "get_redirect_host":
				{ 
					var host = new CollectorResponseEnvelope<string>(null, Request.RequestUri.Host);
					return host;
				}
				case "connect":
				{ 
					var serverConfig = new Dictionary<string, object>();

					serverConfig["agent_run_id"] = Guid.NewGuid();

					var config = new CollectorResponseEnvelope<Dictionary<string, object>>(null, serverConfig);
					return config;
				}
				case "get_agent_commands":
				{
					var commands = _queuedCommands;
					_queuedCommands = new List<AgentCommand>();
					
					var result = new CollectorResponseEnvelope<List<AgentCommand>>(null, commands);
					return result;
				}
			}

			
			return EmptyResponse;
		}

		private CollectedRequest CaptureRequest(Stream body)
		{
			var collectedRequest = new CollectedRequest();
			using (var br = new BinaryReader(body))
			{
				collectedRequest.RequestBody = br.ReadBytes((Int32)body.Length);
			}
			collectedRequest.Method = Request.Method.Method;
			collectedRequest.Querystring = Request.GetQueryNameValuePairs();
			collectedRequest.ContentEncoding = Request.Content.Headers.ContentEncoding;
			
			return collectedRequest;
		}

		[HttpGet]
		[Route("CollectedRequests")]
		public List<CollectedRequest> CollectedRequests()
		{
			return _collectedRequests;
		}

		[HttpGet]
		[Route("TriggerThreadProfile")]
		public void TriggerThreadProfile()
		{
			var threadProfileArguments = new Dictionary<string, object>();
			threadProfileArguments["profile_id"] = -1;
			threadProfileArguments["sample_period"] = 0.1F; //Agent enforces minimums
			threadProfileArguments["duration"] = 120; //Agent enforces minimums

			var threadProfileDetails = new CommandDetails("start_profiler", threadProfileArguments);
			var threadProfileCommand = new AgentCommand(-1, threadProfileDetails);

			_queuedCommands.Add(threadProfileCommand);
		}
	}
}
