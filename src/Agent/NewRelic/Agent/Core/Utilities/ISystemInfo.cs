using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NewRelic.Agent.Core.Utilization;

namespace NewRelic.Agent.Core.Utilities
{
    public interface ISystemInfo
    {
        UInt64 GetTotalPhysicalMemoryBytes();
        Int32 GetTotalLogicalProcessors();
        BootIdResult GetBootId();
    }
}
