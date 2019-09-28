using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using NewRelic.Api.Agent;

namespace AgentSmokeTest
{
	class Program
	{
		public const int TestLengthInMinutes = 5;

		static void Main(string[] args)
		{
			// Execute method that creates transactions in a loop for some period of time
			Console.WriteLine($"Beginning smoke test for {TestLengthInMinutes} minutes.");
			Stopwatch s = new Stopwatch();
			s.Start();
			while (s.Elapsed < TimeSpan.FromMinutes(5))
			{
				DoWork();
			}
			Console.WriteLine("Finished.");
		}

		[Transaction]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void DoWork()
		{
			//Console.WriteLine("No smoke.");
			System.Threading.Thread.Sleep(1000);
		}
	}
}
