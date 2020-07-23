using System;
using System.Threading;
using JetBrains.Annotations;
using NewRelic.WeakActions;
using NUnit.Framework;


// ReSharper disable InconsistentNaming
namespace NewRelic.Dispatchers.UnitTests
{
    public class Class_EventBus
    {
        [Test]
        public void publishing_without_subscribing_does_not_throw_exception()
        {
            EventBus<Object>.Publish(new Object());
        }

        [Test]
        public void publishing_reaches_one_subscriber()
        {
            bool wasCalled = false;
            Action<Object> callback = _ => wasCalled = true;
            EventBus<Object>.Subscribe(callback);
            EventBus<Object>.Publish(new Object());
            EventBus<Object>.Unsubscribe(callback);

            Assert.True(wasCalled);
        }

        [Test]
        public void publishing_reaches_multiple_subscribers()
        {
            bool firstWasCalled = false;
            bool secondWasCalled = false;
            Action<Object> firstCallback = _ => firstWasCalled = true;
            Action<Object> secondCallback = _ => secondWasCalled = true;
            EventBus<Object>.Subscribe(firstCallback);
            EventBus<Object>.Subscribe(secondCallback);
            EventBus<Object>.Publish(new Object());
            EventBus<Object>.Unsubscribe(firstCallback);
            EventBus<Object>.Unsubscribe(secondCallback);

            Assert.True(firstWasCalled);
            Assert.True(secondWasCalled);
        }

        [Test]
        public void publishing_after_unsubscribe_does_not_callback()
        {
            bool wasCalled = false;
            Action<Object> callback = _ => wasCalled = true;
            EventBus<Object>.Subscribe(callback);
            EventBus<Object>.Unsubscribe(callback);
            EventBus<Object>.Publish(new Object());

            Assert.False(wasCalled);
        }

        [Test]
        public void publishing_after_publishing_and_unsubscribing_does_not_callback()
        {
            int callCount = 0;
            Action<Object> callback = _ => ++callCount;
            EventBus<Object>.Subscribe(callback);
            EventBus<Object>.Publish(new Object());
            EventBus<Object>.Unsubscribe(callback);
            EventBus<Object>.Publish(new Object());

            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void publishing_wrong_event_does_not_callback()
        {
            bool wasCalled = false;
            Action<Object> callback = _ => wasCalled = true;
            EventBus<Object>.Subscribe(callback);
            EventBus<String>.Publish(String.Empty);
            EventBus<Object>.Unsubscribe(callback);

            Assert.False(wasCalled);
        }

        [Test]
        public void subscribing_twice_results_in_one_callback()
        {
            int callCount = 0;
            Action<Object> callback = _ => ++callCount;
            EventBus<Object>.Subscribe(callback);
            EventBus<Object>.Subscribe(callback);
            EventBus<Object>.Publish(new Object());
            EventBus<Object>.Unsubscribe(callback);

            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void subscribing_twice_requires_one_unsubscribes()
        {
            bool wasCalled = false;
            Action<Object> callback = _ => wasCalled = true;
            EventBus<Object>.Subscribe(callback);
            EventBus<Object>.Subscribe(callback);
            EventBus<Object>.Unsubscribe(callback);
            EventBus<Object>.Publish(new object());

            Assert.False(wasCalled);
        }

        [Test]
        public void unsubscribing_a_callback_that_is_not_subscribed_does_not_throw()
        {
            EventBus<Object>.Unsubscribe(_ => { });
        }

        [Test]
        public void unsubscribing_twice_after_subscribing_once_does_not_throw()
        {
            Action<Object> callback = _ => { };
            EventBus<Object>.Subscribe(callback);
            EventBus<Object>.Unsubscribe(callback);
            EventBus<Object>.Unsubscribe(callback);
        }

        [Test]
        public void exception_thrown_from_one_subscriber_still_calls_other_subscribers()
        {
            var firstCalled = false;
            var secondCalled = false;
            using (new EventSubscription<Object>(_ => firstCalled = true))
            using (new EventSubscription<Object>(_ => { throw new Exception(); }))
            using (new EventSubscription<Object>(_ => secondCalled = true))
            {
                EventBus<Object>.Publish(new Object());
            }

            Assert.True(firstCalled);
            Assert.True(secondCalled);
        }

        //[Test]
        //public void exception_thrown_from_subscriber_writes_error_log_message()
        //{
        //	using (var logger = new Core.UnitTest.Fixtures.Logging())
        //	using (new EventSubscription<Object>(_ => { throw new Exception(); }))
        //	{
        //		EventBus<Object>.Publish(new object());

        //		Assert.AreEqual(1, logger.ErrorCount);
        //	}
        //}

        [Test]
        public void when_publishing_asynchronously_then_processing_occurs_on_a_different_thread()
        {
            int? threadId = null;
            using (new EventSubscription<Object>(_ => threadId = Thread.CurrentThread.ManagedThreadId))
            {
                EventBus<Object>.PublishAsync(new Object());

                while (threadId == null) ;

                Assert.AreNotEqual(Thread.CurrentThread.ManagedThreadId, threadId);
            }
        }

        public class Method_WeakSubscribe
        {
            private class Foo
            {
                public delegate void Action();

                [NotNull] private readonly Action _action;

                public Foo(Action action)
                {
                    _action = action;
                }

                public void OnObject(Object @object)
                {
                    _action();
                }
            }

            // a place to put foos that will help guarantee their lifetime, local variables have undefined lifetimes in optimized builds
            private Foo _foo;
            private Boolean _fooOnObjectWasCalled;

            public Method_WeakSubscribe()
            {
                _foo = new Foo(() => _fooOnObjectWasCalled = true);
                _fooOnObjectWasCalled = false;
            }

            [SetUp]
            public void Setup()
            {
                _foo = new Foo(() => _fooOnObjectWasCalled = true);
                _fooOnObjectWasCalled = false;
            }

            [Test]
            public void when_weak_action_is_garbage_collected_then_callback_is_not_called()
            {
                // NCrunch ignored due to NCrunch garbage collection bug, see http://stackoverflow.com/questions/16771249/how-to-force-full-garbage-collection-in-net-4-x
                EventBus<Object>.WeakSubscribe(_foo.OnObject);
                _foo = null;

                GC.Collect();

                EventBus<Object>.Publish(new Object());

                Assert.False(_fooOnObjectWasCalled);
            }

            [Test]
            public void when_weak_action_is_strongly_referenced_then_callback_is_called()
            {
                EventBus<Object>.WeakSubscribe(_foo.OnObject);

                GC.Collect();

                EventBus<Object>.Publish(new Object());

                Assert.True(_fooOnObjectWasCalled);
            }

            [Test]
            public void when_weak_action_is_subscribed_then_callback_is_called()
            {
                EventBus<Object>.Subscribe(WeakActionUtilities.MakeWeak<Object>(_foo.OnObject, null));

                GC.Collect();

                EventBus<Object>.Publish(new Object());

                Assert.True(_fooOnObjectWasCalled);
            }

            [Test]
            public void when_weak_action_is_subscribed_but_not_strongly_referenced_then_callback_is_not_called()
            {
                // NCrunch ignored due to NCrunch garbage collection bug, see http://stackoverflow.com/questions/16771249/how-to-force-full-garbage-collection-in-net-4-x
                EventBus<Object>.Subscribe(WeakActionUtilities.MakeWeak<Object>(_foo.OnObject, null));
                _foo = null;

                GC.Collect();

                EventBus<Object>.Publish(new Object());

                Assert.False(_fooOnObjectWasCalled);
            }

            [Test]
            public void when_weak_action_is_unsubscribed_then_callback_is_not_called()
            {
                var weakAction = WeakActionUtilities.MakeWeak<Object>(_foo.OnObject, null);
                EventBus<Object>.Subscribe(weakAction);
                EventBus<Object>.Unsubscribe(weakAction);

                GC.Collect();

                EventBus<Object>.Publish(new Object());

                Assert.False(_fooOnObjectWasCalled);
            }

        }

    }
}
