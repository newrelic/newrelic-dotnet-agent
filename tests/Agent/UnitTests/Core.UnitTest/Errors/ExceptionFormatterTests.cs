// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Errors.UnitTest
{
    [TestFixture]
    public class ExceptionFormatterTests
    {
        private const string OuterExceptionMessage = "Nope";
        private const string InnerExceptionMessage = "You can't touch this.";

        [Test]
        public void ShouldGenerateSameFormatAsToStringWhenStripFalse()
        {
            var exception = GetRealNestedException();

            var expected = exception.ToString();
            var actual = ExceptionFormatter.FormatStackTrace(exception, stripErrorMessage: false);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void ShouldNotContainOuterExceptionMessageWhenStripTrue()
        {
            var exception = GetRealNestedException();

            var actual = ExceptionFormatter.FormatStackTrace(exception, stripErrorMessage: true);

            var hasExceptionMessage = actual.Contains(OuterExceptionMessage);

            Assert.That(hasExceptionMessage, Is.False);
        }

        [Test]
        public void ShouldNotContainInnerExceptionMessageWhenStripTrue()
        {
            var exception = GetRealNestedException();

            var actual = ExceptionFormatter.FormatStackTrace(exception, stripErrorMessage: true);

            var hasExceptionMessage = actual.Contains(InnerExceptionMessage);

            Assert.That(hasExceptionMessage, Is.False);
        }

        private static Exception GetRealNestedException()
        {
            try
            {
                ThrowingMethodCall();
            }
            catch (Exception ex)
            {
                return ex;
            }

            throw new Exception("Should not get this exception. Logic bad");
        }

        private static void ThrowingMethodCall()
        {
            try
            {
                ThrowingSubMethodCall();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(OuterExceptionMessage, exception);
            }
        }

        private static void ThrowingSubMethodCall()
        {
            throw new AccessViolationException(InnerExceptionMessage);
        }
    }
}
