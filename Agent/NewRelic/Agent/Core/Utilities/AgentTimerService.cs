using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Events;

namespace NewRelic.Agent.Core.Utilities
{
	public interface IAgentTimerService
	{
		IAgentTimer StartNew(params string[] nameParts);
	}

	public class AgentTimerService : ConfigurationBasedService, IAgentTimerService
	{
		public AgentTimerService(IAgentHealthReporter agentHealthReporter)
		{
			_agentHealthReporter = agentHealthReporter;
		}

		private bool _enabled = false;
		private readonly IAgentHealthReporter _agentHealthReporter;

		public IAgentTimer StartNew(params string[] nameParts)
		{
			if (!_enabled) return null;
			var timer = new AgentTimer(_agentHealthReporter, nameParts);
			timer.Start();
			return timer;
		}

		protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
		{
			_enabled = _configuration.DiagnosticsCaptureAgentTiming;
		}
	}
}