using System;
using NUnit.Framework;


namespace NewRelic.SystemExtensions.UnitTests
{
    public class ObjectArrayExtensionsTests
    {
        public class Foo
        {
        }

        public class FooBar : Foo
        {
        }

        #region ExtractAs

        [Test]
        public void ExtractAs_Throws_IfSourceIsNull()
        {
            Assert.Throws<NullReferenceException>(() => ((Object[])null).ExtractAs<String>(1));
        }

        [Test]
        public void ExtractAs_Throws_IfSourceIsEmpty()
        {
            var objects = new Object[0];

            Assert.Throws<IndexOutOfRangeException>(() => objects.ExtractAs<String>(0));
        }

        [Test]
        public void ExtractAs_Throws_IfExpectedIndexIsOutOfRange()
        {
            var objects = new object[] { "banana" };

            Assert.Throws<IndexOutOfRangeException>(() => objects.ExtractAs<String>(5));
        }

        [Test]
        public void ExtractAs_Throws_IfExtractedValueIsOfWrongType()
        {
            var objects = new object[] { 1 };

            Assert.Throws<InvalidCastException>(() => objects.ExtractAs<String>(0));
        }

        [Test]
        public void ExtractAs_ReturnsValue_IfValueOfExpectedTypeExists()
        {
            var objects = new Object[] { 1, "banana" };

            var result = objects.ExtractAs<String>(1);

            Assert.AreEqual("banana", result);
        }

        [Test]
        public void ExtractAs_ReturnsValue_IfValueCanBeCastToExpectedType()
        {
            var fooBar = new FooBar();
            var objects = new Object[] { fooBar };

            var result = objects.ExtractAs<Foo>(0);

            Assert.AreEqual(fooBar, result);
        }

        [Test]
        public void ExtractAs_ReturnsNull_IfValueWasNull()
        {
            var objects = new Object[] { null };

            var result = objects.ExtractAs<String>(0);

            Assert.Null(result);
        }

        #endregion ExtractAs

        #region ExtractNotNullAs

        [Test]
        public void ExtractNotNullAs_Throws_IfSourceIsNull()
        {
            Assert.Throws<NullReferenceException>(() => ((Object[])null).ExtractNotNullAs<String>(1));
        }

        [Test]
        public void ExtractNotNullAs_Throws_IfSourceIsEmpty()
        {
            var objects = new Object[0];

            Assert.Throws<IndexOutOfRangeException>(() => objects.ExtractNotNullAs<String>(0));
        }

        [Test]
        public void ExtractNotNullAs_Throws_IfExpectedIndexIsOutOfRange()
        {
            var objects = new object[] { "banana" };

            Assert.Throws<IndexOutOfRangeException>(() => objects.ExtractNotNullAs<String>(5));
        }

        [Test]
        public void ExtractNotNullAs_Throws_IfExtractedValueIsOfWrongType()
        {
            var objects = new object[] { 1 };

            Assert.Throws<InvalidCastException>(() => objects.ExtractNotNullAs<String>(0));
        }

        [Test]
        public void ExtractNotNullAs_ReturnsValue_IfValueOfExpectedTypeExists()
        {
            var objects = new Object[] { 1, "banana" };

            var result = objects.ExtractNotNullAs<String>(1);

            Assert.AreEqual("banana", result);
        }

        [Test]
        public void ExtractNotNullAs_ReturnsValue_IfValueCanBeCastToExpectedType()
        {
            var fooBar = new FooBar();
            var objects = new Object[] { fooBar };

            var result = objects.ExtractNotNullAs<Foo>(0);

            Assert.AreEqual(fooBar, result);
        }

        [Test]
        public void ExtractNotNullAs_Throws_IfValueWasNull()
        {
            var objects = new Object[] { null };

            Assert.Throws<NullReferenceException>(() => objects.ExtractNotNullAs<String>(0));
        }

        #endregion ExtractNotNullAs
    }
}
