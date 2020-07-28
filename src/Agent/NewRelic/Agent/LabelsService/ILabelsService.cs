using System;
using System.Collections.Generic;

namespace NewRelic.Agent
{
    public interface ILabelsService : IDisposable
    {
        IEnumerable<Label> Labels { get; }
    }
}
