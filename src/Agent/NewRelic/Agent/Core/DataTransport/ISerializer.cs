using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.DataTransport
{
	public interface ISerializer
	{
		[NotNull]
		String Serialize([NotNull] Object[] parameters);

		[NotNull]
		T Deserialize<T>([NotNull] String responseBody);
	}
}
