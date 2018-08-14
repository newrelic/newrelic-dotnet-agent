using AgentLogHelper;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Web.Http;

namespace NetFrameworkBasicApplication.Controllers
{
	public class LogsController : ApiController
	{
		private AgentLog _agentLog;
		private const string TransactionSampleLogLineRegex = @"Invoking ""transaction_sample_data"" with : ";


		public LogsController()
		{
			var logPath = @"c:\Home\LogFiles\NewRelic";
			_agentLog = new AgentLog(logPath);
		}

		[HttpGet]
		//Get api/Logs/AgentLog
		public string AgentLog()
		{
			var logString = _agentLog.GetAgentLog();
			return logString;
		}

		[HttpGet]
		//Get api/Logs/AgentLogWithTransactionSample
		public string AgentLogWithTransactionSample()
		{
			var logString = _agentLog.GetAgentLog(TransactionSampleLogLineRegex);
			return logString;
		}

		[HttpGet]
		//Get api/Logs/ProfilerLog
		public string ProfilerLog()
		{
			var logString = _agentLog.GetProfilerLog();
			return logString;
		}
	}
}
