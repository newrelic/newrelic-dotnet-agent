using System;

namespace NewRelic.SystemInterfaces
{
	public class EnvironmentMock : IEnvironment
	{
		private readonly Func<string, string> _getEnvironmentVariable;

		public EnvironmentMock(Func<string, string> getEnvironmentVariable = null)
		{
			_getEnvironmentVariable = getEnvironmentVariable ?? (variable => null);
		}

		public string GetEnvironmentVariable(string variable)
		{
			return _getEnvironmentVariable(variable);
		}

		public string GetEnvironmentVariable(string variable, EnvironmentVariableTarget environmentVariableTarget)
		{
			return _getEnvironmentVariable(variable);
		}
	}
}
