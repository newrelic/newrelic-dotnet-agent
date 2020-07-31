// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;


namespace NewRelic.Dispatchers.UnitTests
{
    public class WeakEventSubscription
    {
        private class Foo
        {
            public delegate void Action();

            private readonly Action _onObjectCallback;

            public Foo(Action onObjectCallback)
            {
                _onObjectCallback = onObjectCallback;
            }

            public void OnObject(object @object)
            {
                _onObjectCallback();
            }
        }

        // a place to put foos that will help guarantee their lifetime, local variables have undefined lifetimes in optimized builds
        private Foo _foo;
        private uint _fooOnObjectCallCount;

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
            using (new WeakEventSubscription<object>(_foo.OnObject)) { }

            GC.Collect();

            EventBus<object>.Publish(new object());

            Assert.AreEqual(0u, _fooOnObjectCallCount);
        }

        [Test]
        public void when_publishing_inside_of_using_statement_then_callback_is_called()
        {
            using (new WeakEventSubscription<object>(_foo.OnObject))
            {
                GC.Collect();

                EventBus<object>.Publish(new object());
            }

            Assert.AreEqual(1u, _fooOnObjectCallCount);
        }

        [Test]
        public void when_subscriber_is_garbage_collected_before_publish_then_callback_is_not_called()
        {
            // NCrunch ignored due to NCrunch garbage collection bug, see http://stackoverflow.com/questions/16771249/how-to-force-full-garbage-collection-in-net-4-x
            new WeakEventSubscription<object>(_foo.OnObject);
            _foo = null;
            GC.Collect();
            EventBus<object>.Publish(new object());

            Assert.AreEqual(0u, _fooOnObjectCallCount);
        }

        [Test]
        public void when_same_method_is_subscribed_twice_then_two_callbacks_are_made()
        {
            using (new WeakEventSubscription<object>(_foo.OnObject))
            using (new WeakEventSubscription<object>(_foo.OnObject))
            {
                GC.Collect();

                EventBus<object>.Publish(new object());
            }

            Assert.AreEqual(2u, _fooOnObjectCallCount);
        }

        [Test]
        public void when_same_method_is_subscribed_twice_and_then_reference_is_lost_then_callback_is_never_called()
        {
            // NCrunch ignored due to NCrunch garbage collection bug, see http://stackoverflow.com/questions/16771249/how-to-force-full-garbage-collection-in-net-4-x
            new WeakEventSubscription<object>(_foo.OnObject);
            new WeakEventSubscription<object>(_foo.OnObject);

            _foo = null;

            GC.Collect();

            EventBus<object>.Publish(new object());

            Assert.AreEqual(0u, _fooOnObjectCallCount);
        }
    }
}
