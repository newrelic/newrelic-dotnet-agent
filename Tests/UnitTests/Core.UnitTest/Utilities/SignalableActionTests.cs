using System.Threading;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilities
{
	[TestFixture]
	public class SignalableActionTests
	{
		[Test]
		public void SignalableAction_GetsSignaled()
		{
			var called = 0;
			void Work()
			{
				++called;
			}

			var action = new SignalableAction(Work, 0);
			action.Start();

			Thread.Sleep(20);
			action.Signal();
			Thread.Sleep(20);
			action.Signal();
			Thread.Sleep(20);
			Assert.AreEqual(2, called);
		}
	}
}
