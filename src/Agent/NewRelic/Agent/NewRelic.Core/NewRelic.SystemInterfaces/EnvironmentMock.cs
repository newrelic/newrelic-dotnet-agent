using System;

namespace NewRelic.SystemInterfaces
{
    public class EnvironmentMock : IEnvironment
    {
        private readonly Func<String, String> _getEnvironmentVariable;

        public EnvironmentMock(Func<String, String> getEnvironmentVariable = null)
        {
            _getEnvironmentVariable = getEnvironmentVariable ?? (variable => null);
        }

        public String GetEnvironmentVariable(String variable)
        {
            return _getEnvironmentVariable(variable);
        }
    }
}
