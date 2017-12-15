using System;
using System.Security;
using JetBrains.Annotations;

namespace NewRelic.SystemInterfaces
{
	public interface IEnvironment
	{
		/// <summary>
		/// Retrieves the value of an environment variable from the current process.
		/// </summary>
		/// <param name="variable">The name of the environment variable.</param>
		/// <returns>The value of the environment variable specified by variable, or null if the environment variable is not found.</returns>
		/// <exception cref="ArgumentNullException">variable is null.</exception>
		/// <exception cref="SecurityException">The caller does not have the required permission to perform this operation.</exception>
		[CanBeNull]
		String GetEnvironmentVariable([NotNull] String variable);
	}
}
