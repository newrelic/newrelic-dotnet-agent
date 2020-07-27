using System;
using JetBrains.Annotations;
using NUnit.Framework;


namespace NewRelic.Dispatchers.UnitTests
{
    public class WeakEventSubscription
    {
        private class Foo
        {
            public delegate void Action();

            [NotNull] private readonly Action _onObjectCallback;

            public Foo([NotNull] Action onObjectCallback)
            {
                _onObjectCallback = onObjectCallback;
            }

            public void OnObject(Object @object)
            {
                _onObjectCallback();
            }
        }

        // a place to put foos that will help guarantee their lifetime, local variables have undefined lifetimes in optimized builds
        private Foo _foo;
        private UInt32 _fooOnObjectCallCount;

        public WeakEventSubscription()
        {
            _foo = new Foo(() => ++_fooOnObjectCallCount);
            _fooOnObjectCallCount = 0;
        }

        [SetUp]
        public void Setup()
        {
            _foo = new Foo(() => ++_fooOnObjectCallCount);
            _fooOnObjectCallCount = 0;
        }

        [Test]
        public void when_publishing_outside_of_using_statement_then_callback_is_not_called()
        {
            using (new WeakEventSubscription<Object>(_foo.OnObject)) { }

            GC.Collect();

            EventBus<Object>.Publish(new Object());

            Assert.AreEqual(0u, _fooOnObjectCallCount);
        }

        [Test]
        public void when_publishing_inside_of_using_statement_then_callback_is_called()
        {
            using (new WeakEventSubscription<Object>(_foo.OnObject))
            {
                GC.Collect();

                EventBus<Object>.Publish(new Object());
            }

            Assert.AreEqual(1u, _fooOnObjectCallCount);
        }

        [Test]
        public void when_subscriber_is_garbage_collected_before_publish_then_callback_is_not_called()
        {
            // NCrunch ignored due to NCrunch garbage collection bug, see http://stackoverflow.com/questions/16771249/how-to-force-full-garbage-collection-in-net-4-x
            new WeakEventSubscription<Object>(_foo.OnObject);
            _foo = null;
            GC.Collect();
            EventBus<Object>.Publish(new Object());

            Assert.AreEqual(0u, _fooOnObjectCallCount);
        }

        [Test]
        public void when_same_method_is_subscribed_twice_then_two_callbacks_are_made()
        {
            using (new WeakEventSubscription<Object>(_foo.OnObject))
            using (new WeakEventSubscription<Object>(_foo.OnObject))
            {
                GC.Collect();

                EventBus<Object>.Publish(new Object());
            }

            Assert.AreEqual(2u, _fooOnObjectCallCount);
        }

        [Test]
        public void when_same_method_is_subscribed_twice_and_then_reference_is_lost_then_callback_is_never_called()
        {
            // NCrunch ignored due to NCrunch garbage collection bug, see http://stackoverflow.com/questions/16771249/how-to-force-full-garbage-collection-in-net-4-x
            new WeakEventSubscription<Object>(_foo.OnObject);
            new WeakEventSubscription<Object>(_foo.OnObject);

            _foo = null;

            GC.Collect();

            EventBus<Object>.Publish(new Object());

            Assert.AreEqual(0u, _fooOnObjectCallCount);
        }
    }
}
