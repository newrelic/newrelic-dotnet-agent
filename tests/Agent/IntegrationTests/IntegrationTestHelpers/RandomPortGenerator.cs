using System;
using System.Diagnostics;
using System.Globalization;
using JetBrains.Annotations;

namespace NewRelic.Agent.IntegrationTestHelpers
{
	public static class RandomPortGenerator
	{
		[NotNull]
		private static readonly Object LockObject = new Object();

		private static readonly Int32 RandomSeed = Process.GetCurrentProcess().Id + AppDomain.CurrentDomain.Id + Environment.TickCount;

		[NotNull]
		private static readonly Random Random = new Random(RandomSeed);

		public static Int32 NextPort()
		{
			lock (LockObject)
			{
				return Random.Next(60000) + 5000;
			}
		}

		[NotNull]
		public static String NextPortString()
		{
			return NextPort().ToString(CultureInfo.InvariantCulture);
		}
	}
}
