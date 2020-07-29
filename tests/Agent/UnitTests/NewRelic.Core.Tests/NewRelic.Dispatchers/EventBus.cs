using System;
using System.Threading;
using NewRelic.WeakActions;
using NUnit.Framework;

namespace NewRelic.Dispatchers.UnitTests
{
    public class Class_EventBus
    {
        [Test]
        public void publishing_without_subscribing_does_not_throw_exception()
        {
            EventBus<object>.Publish(new object());
        }

        [Test]
        public void publishing_reaches_one_subscriber()
        {
            bool wasCalled = false;
            Action<object> callback = _ => wasCalled = true;
            EventBus<object>.Subscribe(callback);
            EventBus<object>.Publish(new object());
            EventBus<object>.Unsubscribe(callback);

            Assert.True(wasCalled);
        }

        [Test]
        public void publishing_reaches_multiple_subscribers()
        {
            bool firstWasCalled = false;
            bool secondWasCalled = false;
            Action<object> firstCallback = _ => firstWasCalled = true;
            Action<object> secondCallback = _ => secondWasCalled = true;
            EventBus<object>.Subscribe(firstCallback);
            EventBus<object>.Subscribe(secondCallback);
            EventBus<object>.Publish(new object());
            EventBus<object>.Unsubscribe(firstCallback);
            EventBus<object>.Unsubscribe(secondCallback);

            Assert.True(firstWasCalled);
            Assert.True(secondWasCalled);
        }

        [Test]
        public void publishing_after_unsubscribe_does_not_callback()
        {
            bool wasCalled = false;
            Action<object> callback = _ => wasCalled = true;
            EventBus<object>.Subscribe(callback);
            EventBus<object>.Unsubscribe(callback);
            EventBus<object>.Publish(new object());

            Assert.False(wasCalled);
        }

        [Test]
        public void publishing_after_publishing_and_unsubscribing_does_not_callback()
        {
            int callCount = 0;
            Action<object> callback = _ => ++callCount;
            EventBus<object>.Subscribe(callback);
            EventBus<object>.Publish(new object());
            EventBus<object>.Unsubscribe(callback);
            EventBus<object>.Publish(new object());

            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void publishing_wrong_event_does_not_callback()
        {
            bool wasCalled = false;
            Action<object> callback = _ => wasCalled = true;
            EventBus<object>.Subscribe(callback);
            EventBus<string>.Publish(string.Empty);
            EventBus<object>.Unsubscribe(callback);

            Assert.False(wasCalled);
        }

        [Test]
        public void subscribing_twice_results_in_one_callback()
        {
            int callCount = 0;
            Action<object> callback = _ => ++callCount;
            EventBus<object>.Subscribe(callback);
            EventBus<object>.Subscribe(callback);
            EventBus<object>.Publish(new object());
            EventBus<object>.Unsubscribe(callback);

            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void subscribing_twice_requires_one_unsubscribes()
        {
            bool wasCalled = false;
            Action<object> callback = _ => wasCalled = true;
            EventBus<object>.Subscribe(callback);
            EventBus<object>.Subscribe(callback);
            EventBus<object>.Unsubscribe(callback);
            EventBus<object>.Publish(new object());

            Assert.False(wasCalled);
        }

        [Test]
        public void unsubscribing_a_callback_that_is_not_subscribed_does_not_throw()
        {
            EventBus<object>.Unsubscribe(_ => { });
        }

        [Test]
        public void unsubscribing_twice_after_subscribing_once_does_not_throw()
        {
            Action<object> callback = _ => { };
            EventBus<object>.Subscribe(callback);
            EventBus<object>.Unsubscribe(callback);
            EventBus<object>.Unsubscribe(callback);
        }

        [Test]
        public void exception_thrown_from_one_subscriber_still_calls_other_subscribers()
        {
            var firstCalled = false;
            var secondCalled = false;
            using (new EventSubscription<object>(_ => firstCalled = true))
            using (new EventSubscription<object>(_ => { throw new Exception(); }))
            using (new EventSubscription<object>(_ => secondCalled = true))
            {
                EventBus<object>.Publish(new object());
            }

            Assert.True(firstCalled);
            Assert.True(secondCalled);
        }

        [Test]
        public void when_publishing_asynchronously_then_processing_occurs_on_a_different_thread()
        {
            int? threadId = null;
            using (new EventSubscription<object>(_ => threadId = Thread.CurrentThread.ManagedThreadId))
            {
                EventBus<object>.PublishAsync(new object());

                while (threadId == null) ;

                Assert.AreNotEqual(Thread.CurrentThread.ManagedThreadId, threadId);
            }
        }

        public class Method_WeakSubscribe
        {
            private class Foo
            {
                public delegate void Action();

                private readonly Action _action;

                public Foo(Action action)
                {
                    _action = action;
                }

                public void OnObject(object @object)
                {
                    _action();
                }
            }

            // a place to put foos that will help guarantee their lifetime, local variables have undefined lifetimes in optimized builds
            private Foo _foo;
            private bool _fooOnObjectWasCalled;

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
                EventBus<object>.WeakSubscribe(_foo.OnObject);
                _foo = null;

                GC.Collect();

                EventBus<object>.Publish(new object());

                Assert.False(_fooOnObjectWasCalled);
            }

            [Test]
            public void when_weak_action_is_strongly_referenced_then_callback_is_called()
            {
                EventBus<object>.WeakSubscribe(_foo.OnObject);

                GC.Collect();

                EventBus<object>.Publish(new object());

                Assert.True(_fooOnObjectWasCalled);
            }

            [Test]
            public void when_weak_action_is_subscribed_then_callback_is_called()
            {
                EventBus<object>.Subscribe(WeakActionUtilities.MakeWeak<object>(_foo.OnObject, null));

                GC.Collect();

                EventBus<object>.Publish(new object());

                Assert.True(_fooOnObjectWasCalled);
            }

            [Test]
            public void when_weak_action_is_subscribed_but_not_strongly_referenced_then_callback_is_not_called()
            {
                // NCrunch ignored due to NCrunch garbage collection bug, see http://stackoverflow.com/questions/16771249/how-to-force-full-garbage-collection-in-net-4-x
                EventBus<object>.Subscribe(WeakActionUtilities.MakeWeak<object>(_foo.OnObject, null));
                _foo = null;

                GC.Collect();

                EventBus<object>.Publish(new object());

                Assert.False(_fooOnObjectWasCalled);
            }

            [Test]
            public void when_weak_action_is_unsubscribed_then_callback_is_not_called()
            {
                var weakAction = WeakActionUtilities.MakeWeak<object>(_foo.OnObject, null);
                EventBus<object>.Subscribe(weakAction);
                EventBus<object>.Unsubscribe(weakAction);

                GC.Collect();

                EventBus<object>.Publish(new object());

                Assert.False(_fooOnObjectWasCalled);
            }

        }

    }
}
