using System;
using NewRelic.Agent.Core.AgentHealth;

namespace NewRelic.Agent.Core.Utilities
{
	public interface IAgentTimer : IDisposable
	{
		void Start();
		void StopAndRecordMetric();
	}

	public class AgentTimer : IAgentTimer
	{
		public AgentTimer(IAgentHealthReporter agentHealthReporter, params string[] nameParts)
		{
			_agentHealthReporter = agentHealthReporter;
			_nameParts = nameParts;
		}

		private readonly IAgentHealthReporter _agentHealthReporter;
		private readonly string[] _nameParts;
		private System.Diagnostics.Stopwatch _stopWatch;

		public void Start()
		{
			_stopWatch = System.Diagnostics.Stopwatch.StartNew();
		}

		public void StopAndRecordMetric()
		{
			_stopWatch.Stop();
			_agentHealthReporter.ReportAgentTimingMetric(string.Join("/", _nameParts), _stopWatch.Elapsed);
		}

		public void Dispose()
		{
			StopAndRecordMetric();
		}
	}
}