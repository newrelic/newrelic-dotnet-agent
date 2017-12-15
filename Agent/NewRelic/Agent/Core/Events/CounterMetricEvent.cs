using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Events
{
	public class CounterMetricEvent
	{
		[NotNull] public readonly String Namespace;
		[NotNull] public readonly String Name;
		[NotNull] public readonly Int32 Count;

		public CounterMetricEvent([NotNull] String @namespace, [NotNull] String name, Int32 count = 1)
		{
			Namespace = @namespace;
			Name = name;
			Count = count;
		}
		public CounterMetricEvent([NotNull] String name, Int32 count = 1)
		{
			Namespace = "";
			Name = name;
			Count = count;
		}
	}
}
