using JetBrains.Annotations;

namespace NewRelic.Agent
{
	public interface ILazyMap<in TKey, TValue>
	{
		/// <summary>
		/// Get the value at <paramref name="key"/>, lazily evaluating it based on the backing resolution strategy.
		/// </summary>
		[CanBeNull]
		TValue Get([CanBeNull] TKey key);

		/// <summary>
		/// Use this if you don't want the map to use its backing resolution strategy.
		/// </summary>
		void Override([NotNull] TKey key, [CanBeNull] TValue value);
	}
}
