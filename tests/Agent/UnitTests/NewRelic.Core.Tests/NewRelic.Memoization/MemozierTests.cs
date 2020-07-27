using System;
using NUnit.Framework;


namespace NewRelic.Memoization.UnitTests
{
    public class MemozierTests
    {
        [Test]
        public void returns_previously_set_backing_nullable_value()
        {
            Int32? backingVariable = 5;
            Memoizer.Memoize(ref backingVariable, () => 7);
            Assert.AreEqual(5, backingVariable);
        }

        [Test]
        public void returns_new_nullable_value()
        {
            Int32? backingVariable = null;
            var result = Memoizer.Memoize(ref backingVariable, () => 5);
            Assert.AreEqual(5, result);
        }

        [Test]
        public void backing_nullable_value_is_set()
        {
            Int32? backingVariable = null;
            Memoizer.Memoize(ref backingVariable, () => 5);
            Assert.AreEqual(5, backingVariable);
        }

        [Test]
        public void returns_previously_set_backing_value()
        {
            String backingVariable = "foo";
            Memoizer.Memoize(ref backingVariable, () => "bar");
            Assert.AreEqual("foo", backingVariable);
        }

        [Test]
        public void returns_new_value()
        {
            String backingVariable = null;
            var result = Memoizer.Memoize(ref backingVariable, () => "foo");
            Assert.AreEqual("foo", result);
        }

        [Test]
        public void backing_value_is_set()
        {
            String backingVariable = null;
            Memoizer.Memoize(ref backingVariable, () => "foo");
            Assert.AreEqual("foo", backingVariable);
        }

        [Test]
        public void when_null_func_provided_then_throws_exception()
        {
            String backingVariable = null;
            Assert.Throws<ArgumentNullException>(() => Memoizer.Memoize(ref backingVariable, null));
        }

        [Test]
        public void when_null_func_provided_then_nullable_throws_exception()
        {
            Int32? backingVariable = null;
            Assert.Throws<ArgumentNullException>(() => Memoizer.Memoize<Int32>(ref backingVariable, null));
        }
    }
}
