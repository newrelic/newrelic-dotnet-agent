using NewRelic.Agent.Core.Utilization;

namespace NewRelic.Agent.Core.Utilities
{
	public interface ISystemInfo
	{
		ulong? GetTotalPhysicalMemoryBytes();
		int? GetTotalLogicalProcessors();
		BootIdResult GetBootId();
	}
}
