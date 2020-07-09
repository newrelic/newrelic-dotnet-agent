using System;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;

namespace NewRelic.Agent.TestUtilities
{
	public static class WaitForPredicateOrTimeout
	{
		/// <summary>
		/// Takes in a predicate that is watched for timeout duration.  If the predicate is set to true during that time then Wait returns true.  If the timeout is reached then it returns false.
		/// </summary>
		public static Boolean Wait(ref Boolean predicate, TimeSpan timeout, TimeSpan? timeBetween = null)
		{
			if (timeBetween == null)
				timeBetween = TimeSpan.FromMilliseconds(1);

			var timer = new Stopwatch();
			timer.Start();
			while (!predicate)
			{
				if (timer.Elapsed >= timeout)
					return false;

				Thread.Sleep(timeBetween.Value);
			}

			return true;
		}

		public static Boolean Wait<T>(ref T predicate, TimeSpan timeout, TimeSpan? timeBetween = null) where T : class
		{
			if (timeBetween == null)
				timeBetween = TimeSpan.FromMilliseconds(1);

			var timer = new Stopwatch();
			timer.Start();
			while (predicate == null)
			{
				if (timer.Elapsed >= timeout)
					return false;

				Thread.Sleep(timeBetween.Value);
			}

			return true;
		}

		public static Boolean Wait([NotNull] Func<Boolean> predicate, TimeSpan timeout, TimeSpan? timeBetween = null)
		{
			if (timeBetween == null)
				timeBetween = TimeSpan.FromMilliseconds(1);

			var timer = new Stopwatch();
			timer.Start();
			while (!predicate())
			{
				if (timer.Elapsed >= timeout)
					return false;

				Thread.Sleep(timeBetween.Value);
			}

			return true;
		}
	}
}
