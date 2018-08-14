using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Web;
using System.Web.Http;
using AgentLogHelper;

namespace OwinService.Controllers
{
	public class LogsController : ApiController
	{
		private AgentLog _agentLog;

		public LogsController()
		{
			var logPath = Path.Combine(Environment.GetEnvironmentVariable("NEWRELIC_HOME"), "logs");
			_agentLog = new AgentLog(logPath);
		}

		[HttpGet]
		[Route("Logs/AgentLog")]
		public string AgentLog()
		{
			var logString = _agentLog.GetAgentLog();
			return logString;
		}

		[HttpGet]
		[Route("Logs/ProfilerLog")]
		public string ProfilerLog()
		{
			var logString = _agentLog.GetProfilerLog();
			return logString;
		}
	}
}
