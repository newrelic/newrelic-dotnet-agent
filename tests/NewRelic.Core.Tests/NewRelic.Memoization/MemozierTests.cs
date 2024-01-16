// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Memoization.UnitTests
{
    public class MemozierTests
    {
        [Test]
        public void returns_previously_set_backing_nullable_value()
        {
            int? backingVariable = 5;
            Memoizer.Memoize(ref backingVariable, () => 7);
            ClassicAssert.AreEqual(5, backingVariable);
        }

        [Test]
        public void returns_new_nullable_value()
        {
            int? backingVariable = null;
            var result = Memoizer.Memoize(ref backingVariable, () => 5);
            ClassicAssert.AreEqual(5, result);
        }

        [Test]
        public void backing_nullable_value_is_set()
        {
            int? backingVariable = null;
            Memoizer.Memoize(ref backingVariable, () => 5);
            ClassicAssert.AreEqual(5, backingVariable);
        }

        [Test]
        public void returns_previously_set_backing_value()
        {
            string backingVariable = "foo";
            Memoizer.Memoize(ref backingVariable, () => "bar");
            ClassicAssert.AreEqual("foo", backingVariable);
        }

        [Test]
        public void returns_new_value()
        {
            string backingVariable = null;
            var result = Memoizer.Memoize(ref backingVariable, () => "foo");
            ClassicAssert.AreEqual("foo", result);
        }

        [Test]
        public void backing_value_is_set()
        {
            string backingVariable = null;
            Memoizer.Memoize(ref backingVariable, () => "foo");
            ClassicAssert.AreEqual("foo", backingVariable);
        }

        [Test]
        public void when_null_func_provided_then_throws_exception()
        {
            string backingVariable = null;
            Assert.Throws<ArgumentNullException>(() => Memoizer.Memoize(ref backingVariable, null));
        }

        [Test]
        public void when_null_func_provided_then_nullable_throws_exception()
        {
            int? backingVariable = null;
            Assert.Throws<ArgumentNullException>(() => Memoizer.Memoize<Int32>(ref backingVariable, null));
        }
    }
}
