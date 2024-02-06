// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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
            Assert.Throws<NullReferenceException>(() => ((object[])null).ExtractAs<string>(1));
        }

        [Test]
        public void ExtractAs_Throws_IfSourceIsEmpty()
        {
            var objects = new object[0];

            Assert.Throws<IndexOutOfRangeException>(() => objects.ExtractAs<string>(0));
        }

        [Test]
        public void ExtractAs_Throws_IfExpectedIndexIsOutOfRange()
        {
            var objects = new object[] { "banana" };

            Assert.Throws<IndexOutOfRangeException>(() => objects.ExtractAs<string>(5));
        }

        [Test]
        public void ExtractAs_Throws_IfExtractedValueIsOfWrongType()
        {
            var objects = new object[] { 1 };

            Assert.Throws<InvalidCastException>(() => objects.ExtractAs<string>(0));
        }

        [Test]
        public void ExtractAs_ReturnsValue_IfValueOfExpectedTypeExists()
        {
            var objects = new object[] { 1, "banana" };

            var result = objects.ExtractAs<string>(1);

            Assert.That(result, Is.EqualTo("banana"));
        }

        [Test]
        public void ExtractAs_ReturnsValue_IfValueCanBeCastToExpectedType()
        {
            var fooBar = new FooBar();
            var objects = new object[] { fooBar };

            var result = objects.ExtractAs<Foo>(0);

            Assert.That(result, Is.EqualTo(fooBar));
        }

        [Test]
        public void ExtractAs_ReturnsNull_IfValueWasNull()
        {
            var objects = new object[] { null };

            var result = objects.ExtractAs<string>(0);

            Assert.That(result, Is.Null);
        }

        #endregion ExtractAs

        #region ExtractNotNullAs

        [Test]
        public void ExtractNotNullAs_Throws_IfSourceIsNull()
        {
            Assert.Throws<NullReferenceException>(() => ((object[])null).ExtractNotNullAs<string>(1));
        }

        [Test]
        public void ExtractNotNullAs_Throws_IfSourceIsEmpty()
        {
            var objects = new object[0];

            Assert.Throws<IndexOutOfRangeException>(() => objects.ExtractNotNullAs<string>(0));
        }

        [Test]
        public void ExtractNotNullAs_Throws_IfExpectedIndexIsOutOfRange()
        {
            var objects = new object[] { "banana" };

            Assert.Throws<IndexOutOfRangeException>(() => objects.ExtractNotNullAs<string>(5));
        }

        [Test]
        public void ExtractNotNullAs_Throws_IfExtractedValueIsOfWrongType()
        {
            var objects = new object[] { 1 };

            Assert.Throws<InvalidCastException>(() => objects.ExtractNotNullAs<string>(0));
        }

        [Test]
        public void ExtractNotNullAs_ReturnsValue_IfValueOfExpectedTypeExists()
        {
            var objects = new object[] { 1, "banana" };

            var result = objects.ExtractNotNullAs<string>(1);

            Assert.That(result, Is.EqualTo("banana"));
        }

        [Test]
        public void ExtractNotNullAs_ReturnsValue_IfValueCanBeCastToExpectedType()
        {
            var fooBar = new FooBar();
            var objects = new object[] { fooBar };

            var result = objects.ExtractNotNullAs<Foo>(0);

            Assert.That(result, Is.EqualTo(fooBar));
        }

        [Test]
        public void ExtractNotNullAs_Throws_IfValueWasNull()
        {
            var objects = new object[] { null };

            Assert.Throws<NullReferenceException>(() => objects.ExtractNotNullAs<string>(0));
        }

        #endregion ExtractNotNullAs
    }
}
