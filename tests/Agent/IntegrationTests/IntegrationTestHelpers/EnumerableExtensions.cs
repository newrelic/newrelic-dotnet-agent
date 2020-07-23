using System;
using System.Collections.Generic;

namespace NewRelic.Agent.IntegrationTestHelpers
{
	public static class Extensions
	{
		public static IEnumerable<T> Flatten<T>(this T root, Func<T, IEnumerable<T>> getChildren)
		{
			var stack = new Stack<T>();
			stack.Push(root);
			while (stack.Count > 0)
			{
				var current = stack.Pop();
				yield return current;
				foreach (var child in getChildren(current))
					stack.Push(child);
			}
		}
	}
}
