using System;
using JetBrains.Annotations;

namespace NewRelic.Agent
{
	public interface IAttribute
	{
		[NotNull]
		String Key { get; }

		[NotNull]
		Object Value { get; }

		AttributeDestinations DefaultDestinations { get; }
	}
}
