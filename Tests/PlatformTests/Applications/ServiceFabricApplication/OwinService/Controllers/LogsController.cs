using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Web;
using System.Web.Http;

namespace OwinService.Controllers
{
	public class LogsController : ApiController
	{
		private const string AgentLogPrefix = "newrelic_agent_";
		private const string ProfilerLogNamePrefix = "NewRelic.Profiler.";
		private const string LogFilePostfix = ".log";
		private const string ConnectString = @"Invoking ""connect"" with : [";

		private readonly int _processId;
		private readonly string _agentLogPath;
		private readonly string _profilerLogPath;

		public LogsController()
		{
			var logPath = Path.Combine(Environment.GetEnvironmentVariable("NEWRELIC_HOME"), "logs");
			var assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
			_agentLogPath = Path.Combine(logPath, AgentLogPrefix + assemblyName + LogFilePostfix);
			_processId = Process.GetCurrentProcess().Id;
			_profilerLogPath = Path.Combine(logPath, ProfilerLogNamePrefix + _processId + LogFilePostfix);
		}

		[HttpGet]
		[Route("Logs/AgentLog")]
		public string AgentLog()
		{
			var logString = String.Empty;
			var stopWatch = new Stopwatch();
			stopWatch.Start();

			while (stopWatch.Elapsed < TimeSpan.FromMinutes(3))
			{
				try
				{
					logString = Readfile(_agentLogPath);
					var logLines = logString.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

					foreach (var line in logLines)
					{
						if (line.Contains(ConnectString))
						{
							return logString;
						}
					}
				}
				catch (FileNotFoundException ex)
				{

				}

				Thread.Sleep(40000);
			}

			stopWatch.Stop();
			return logString;
		}

		[HttpGet]
		[Route("Logs/ProfilerLog")]
		public string ProfilerLog()
		{
			return Readfile(_profilerLogPath);
		}


		private string Readfile(string filePath)
		{
			using (var filestream = new StreamReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
			{
				return filestream.ReadToEnd();
			}
		}
	}
}
