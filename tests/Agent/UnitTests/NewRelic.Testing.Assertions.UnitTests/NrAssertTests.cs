// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace NewRelic.Testing.Assertions.UnitTests
{
    [TestFixture]
    public class NrAssertTests
    {
        // Note: Intentionally not testing `NrAssert.Multiple()`, as it's now just a pass-through to `NUnit.Framework.Assert.Multiple()`

        #region Throws

        [Test]
        public void Throws_ReturnsException_IfTargetThrowsExpectedExceptionType()
        {
            var exception = NrAssert.Throws<NullReferenceException>(() => { throw new NullReferenceException("Test message"); });

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.Message, Is.EqualTo("Test message"));
        }

        [Test]
        public void Throws_ReturnsException_IfTargetThrowsASubclassOfExpectedExceptionType()
        {
            var exception = NrAssert.Throws<Exception>(() => { throw new NullReferenceException(); });

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.GetType(), Is.EqualTo(typeof(NullReferenceException)));
        }

        [Test]
        public void Throws_ThrowsTestFailureException_IfTargetDoesNotThrowAnException()
        {
            TestFailureException testException = null;
            try
            {
                NrAssert.Throws<NullReferenceException>(() => { });
            }
            catch (TestFailureException ex)
            {
                testException = ex;
            }

            Assert.That(testException, Is.Not.Null);
            Assert.That(testException.Message, Is.EqualTo("Expected exception of type 'System.NullReferenceException' was not thrown."));
        }

        [Test]
        public void Throws_ThrowsTestFailureException_IfTargetThrowsIncompatibleExceptionType()
        {
            TestFailureException testFailureException = null;
            try
            {
                NrAssert.Throws<NullReferenceException>(() => { throw new InvalidOperationException(); });
            }
            catch (TestFailureException ex)
            {
                testFailureException = ex;
            }

            Assert.That(testFailureException, Is.Not.Null);
            Assert.That(testFailureException.Message.StartsWith(
                "Expected exception of type 'System.NullReferenceException', but exception of type 'System.InvalidOperationException' was thrown instead",
                StringComparison.InvariantCultureIgnoreCase), Is.True);
        }

        #endregion Throws
    }
}
