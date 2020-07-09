using System.Collections.Generic;
using JetBrains.Annotations;

namespace NewRelic.Agent
{
	public interface IAttributeFilter<T> where T : IAttribute
	{
		[NotNull]
		IEnumerable<T> FilterAttributes([NotNull] IEnumerable<T> attributes, AttributeDestinations destination);
	}
}
