using System;
using NUnit.Framework;


// ReSharper disable InconsistentNaming
namespace NewRelic.Dispatchers.UnitTests
{
	public class Class_EventSubscription
	{
		[Test]
		public void publishing_outside_using_statement_results_in_no_callback()
		{
			var wasCalled = false;
			using (new EventSubscription<Object>(_ => wasCalled = true)) {}

			EventBus<Object>.Publish(new Object());

			Assert.False(wasCalled);
		}

		[Test]
		public void publishing_inside_using_statement_results_in_callback()
		{
			var wasCalled = false;
			using (new EventSubscription<Object>(_ => wasCalled = true))
				EventBus<Object>.Publish(new Object());

			Assert.True(wasCalled);
		}

		[Test]
		public void two_disposables_with_same_callback_are_called_once()
		{
			var callCount = 0;
			Action<Object> callback = _ => ++callCount;
			using (new EventSubscription<Object>(callback))
			using (new EventSubscription<Object>(callback))
			{
				EventBus<Object>.Publish(new Object());
			}

			Assert.AreEqual(1, callCount);
		}
	}
}
