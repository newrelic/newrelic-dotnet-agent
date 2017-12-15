using System;
using System.Threading;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Fixtures
{
	public static class Assertions
	{
		public static void Eventually(Func<Boolean> predicate, TimeSpan timeout)
		{
			var giveUpTime = DateTime.Now + timeout;
			while (!predicate() && DateTime.Now < giveUpTime)
				Thread.Sleep(1);

			Assert.True(predicate());
		}
	}
}
