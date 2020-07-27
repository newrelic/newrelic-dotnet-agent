using System;
using NUnit.Framework;

namespace NewRelic.WeakActions.UnitTests
{
    public class WeakActionTests
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


        public WeakActionTests()
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
        public void when_reference_is_not_garbage_collected_then_weak_action_is_called()
        {
            var action = WeakActionUtilities.MakeWeak<object>(_foo.OnObject, null);

            GC.Collect();

            action.Action(new object());

            Assert.True(_fooOnObjectWasCalled);
        }

        [Test]
        public void when_garbage_collection_occurs_then_weak_action_is_not_called()
        {
            // NCrunch ignored due to NCrunch garbage collection bug, see http://stackoverflow.com/questions/16771249/how-to-force-full-garbage-collection-in-net-4-x
            var action = WeakActionUtilities.MakeWeak<object>(_foo.OnObject, null);
            _foo = null;

            GC.Collect();

            action.Action(new object());

            Assert.False(_fooOnObjectWasCalled);
        }

        [Test]
        public void when_weak_action_is_called_on_strongly_referenced_object_then_actionGarbageCollectedCallback_is_not_called()
        {
            var actionGarbageCollectedCallbackWasCalled = false;

            var action = WeakActionUtilities.MakeWeak<object>(_foo.OnObject, _ => actionGarbageCollectedCallbackWasCalled = true);

            GC.Collect();

            action.Action(new object());

            Assert.False(actionGarbageCollectedCallbackWasCalled);
        }

        [Test]
        public void when_weak_action_is_called_on_garbage_collected_object_then_actionGarbageCollectedCallback_is_called()
        {
            // NCrunch ignored due to NCrunch garbage collection bug, see http://stackoverflow.com/questions/16771249/how-to-force-full-garbage-collection-in-net-4-x
            var actionGarbageCollectedCallbackWasCalled = false;

            var action = WeakActionUtilities.MakeWeak<object>(_foo.OnObject, _ => actionGarbageCollectedCallbackWasCalled = true);
            _foo = null;

            GC.Collect();

            action.Action(new object());

            Assert.True(actionGarbageCollectedCallbackWasCalled);
        }

        // this test is a simplified test to make sure that the test runner is not doing something wierd with object lifetimes that will cause other tests to break. Currently, there is a bug in NCrunch that causes this test to fail, which means other failures in this library will also fail. If this test is green for a given test runner then other failures are legitimate.
        [Test]
        public void weak_reference_test()
        {
            // NCrunch ignored due to NCrunch garbage collection bug, see http://stackoverflow.com/questions/16771249/how-to-force-full-garbage-collection-in-net-4-x
            var weakReference = new WeakReference(new object());

            GC.Collect();

            Assert.False(weakReference.IsAlive);
        }
    }
}
