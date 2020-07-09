using System;

namespace NewRelic.SystemInterfaces
{
	public class Environment : IEnvironment
	{
		public String GetEnvironmentVariable(String variable)
		{
			return System.Environment.GetEnvironmentVariable(variable);
		}
	}
}
