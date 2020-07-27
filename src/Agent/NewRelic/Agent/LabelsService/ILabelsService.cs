using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace NewRelic.Agent
{
    public interface ILabelsService : IDisposable
    {
        [NotNull]
        IEnumerable<Label> Labels { get; }
    }
}
