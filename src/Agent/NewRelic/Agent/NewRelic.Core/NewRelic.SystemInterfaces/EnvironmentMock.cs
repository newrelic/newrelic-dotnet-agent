using System;
using JetBrains.Annotations;

namespace NewRelic.SystemInterfaces
{
    public class EnvironmentMock : IEnvironment
    {
        [NotNull]
        private readonly Func<String, String> _getEnvironmentVariable;

        public EnvironmentMock([CanBeNull] Func<String, String> getEnvironmentVariable = null)
        {
            _getEnvironmentVariable = getEnvironmentVariable ?? (variable => null);
        }

        public String GetEnvironmentVariable(String variable)
        {
            return _getEnvironmentVariable(variable);
        }
    }
}
