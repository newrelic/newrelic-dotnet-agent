// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace NewRelic.Testing.Assertions.UnitTests
{
    [TestFixture]
    public class NrAssertTests
    {
        #region Multiple

        [Test]
        public void Multiple_DoesNotThrow_IfAllAssertionsPass()
        {
            NrAssert.Multiple(
                () => Assert.IsTrue(true),
                () => Assert.IsFalse(false)
                );

            // Getting to the end of this test without throwing constitutes success, no further assertions needed
        }

        [Test]
        public void Multiple_Throws_IfAnyAssertionFails()
        {
            TestFailureException expectedException = null;

            try
            {
                NrAssert.Multiple(
                    () => Assert.IsTrue(true),
                    () => Assert.IsTrue(false)
                    );
            }
            catch (TestFailureException ex)
            {
                expectedException = ex;
            }

            Assert.NotNull(expectedException);
            Assert.IsTrue(expectedException.Message.IndexOf("assertion", StringComparison.InvariantCultureIgnoreCase) > -1);

            Assert.Pass(); // needed to override NUnit's failing status at this point
        }

        [Test]
        public void Multiple_ContainsAllAssertionFailures_IfMultipleAssertionsFail()
        {
            TestFailureException expectedException = null;

            try
            {
                NrAssert.Multiple(
                    () => Assert.IsTrue(false),
                    () => Assert.IsTrue(true),
                    () => Assert.IsTrue(false)
                    );
            }
            catch (TestFailureException ex)
            {
                expectedException = ex;
            }

            Assert.NotNull(expectedException);

            var failure1Index = expectedException.Message.IndexOf("assertion", StringComparison.InvariantCultureIgnoreCase);
            var failure2Index = expectedException.Message.IndexOf("assertion", failure1Index + 1, StringComparison.InvariantCultureIgnoreCase);

            Assert.IsTrue(failure1Index > -1);
            Assert.IsTrue(failure2Index > -1);

            Assert.Pass(); // needed to override NUnit's failing status at this point
        }

        [Test]
        public void Multiple_Throws_IfAnyActionThrows()
        {
            TestFailureException expectedException = null;

            try
            {
                NrAssert.Multiple(
                    () => Assert.IsTrue(true),
                    () => { throw new IndexOutOfRangeException(); }
                    );
            }
            catch (TestFailureException ex)
            {
                expectedException = ex;
            }

            Assert.NotNull(expectedException);
            Assert.IsTrue(expectedException.Message.IndexOf("IndexOutOfRangeException", StringComparison.InvariantCultureIgnoreCase) > -1);

            Assert.Pass(); // needed to override NUnit's failing status at this point
        }

        [Test]
        public void Multiple_ContainsAllExceptions_IfMultipleActionsThrow()
        {
            TestFailureException expectedException = null;

            try
            {
                NrAssert.Multiple(
                    () => { throw new IndexOutOfRangeException(); },
                    () => Assert.IsTrue(true),
                    () => { throw new NullReferenceException(); }
                    );
            }
            catch (TestFailureException ex)
            {
                expectedException = ex;
            }

            Assert.NotNull(expectedException);

            var exception1Index = expectedException.Message.IndexOf("IndexOutOfRangeException", StringComparison.InvariantCultureIgnoreCase);
            var exception2Index = expectedException.Message.IndexOf("NullReferenceException", exception1Index + 1, StringComparison.InvariantCultureIgnoreCase);

            Assert.IsTrue(exception1Index > -1);
            Assert.IsTrue(exception2Index > -1);

            Assert.Pass(); // needed to override NUnit's failing status at this point
        }

        [Test]
        public void Multiple_ContainsAllExceptionsAndAssertionFailures_IfAssertionsFailureAndThrowAtTheSameTime()
        {
            TestFailureException expectedException = null;

            try
            {
                NrAssert.Multiple(
                    () => { throw new IndexOutOfRangeException(); },
                    () => Assert.IsTrue(true),
                    () => Assert.IsTrue(false)
                    );
            }
            catch (TestFailureException ex)
            {
                expectedException = ex;
            }

            Assert.NotNull(expectedException);

            var exceptionIndex = expectedException.Message.IndexOf("IndexOutOfRangeException", StringComparison.InvariantCultureIgnoreCase);
            var failureIndex = expectedException.Message.IndexOf("assertion", exceptionIndex + 1, StringComparison.InvariantCultureIgnoreCase);

            Assert.IsTrue(exceptionIndex > -1);
            Assert.IsTrue(failureIndex > -1);

            Assert.Pass(); // needed to override NUnit's failing status at this point
        }

        #endregion Multiple

        #region Throws

        [Test]
        public void Throws_ReturnsException_IfTargetThrowsExpectedExceptionType()
        {
            var exception = NrAssert.Throws<NullReferenceException>(() => { throw new NullReferenceException("Test message"); });

            Assert.NotNull(exception);
            Assert.AreEqual("Test message", exception.Message);
        }

        [Test]
        public void Throws_ReturnsException_IfTargetThrowsASubclassOfExpectedExceptionType()
        {
            var exception = NrAssert.Throws<Exception>(() => { throw new NullReferenceException(); });

            Assert.NotNull(exception);
            Assert.AreEqual(typeof(NullReferenceException), exception.GetType());
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

            Assert.NotNull(testException);
            Assert.AreEqual("Expected exception of type 'System.NullReferenceException' was not thrown.", testException.Message);
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

            Assert.NotNull(testFailureException);
            Assert.IsTrue(testFailureException.Message.StartsWith(
                "Expected exception of type 'System.NullReferenceException', but exception of type 'System.InvalidOperationException' was thrown instead",
                StringComparison.InvariantCultureIgnoreCase));
        }

        #endregion Throws
    }
}
