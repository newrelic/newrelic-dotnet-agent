// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace Agent.Extensions.Tests
{
    public class DelegatesTests
    {
        #region OnSuccess

        [Test]
        public void GetDelegateFor_RunsOnSuccess_IfNoException()
        {
            // Arrange
            var called = false;

            // Act
            var myDelegate = Delegates.GetDelegateFor(onSuccess: () => called = true);
            myDelegate(result: null, exception: null);

            ClassicAssert.That(called);
        }

        [Test]
        public void GetDelegateFor_RunsOnSuccessWithValue_IfNoException()
        {
            // Arrange
            const string expectedValue = "expectedValue";
            var passedValue = null as string;

            // Act
            var myDelegate = Delegates.GetDelegateFor<string>(onSuccess: value => passedValue = value);
            myDelegate(result: expectedValue, exception: null);

            ClassicAssert.AreEqual(expectedValue, passedValue);
        }

        [Test]
        public void GetDelegateFor_DoesNotRunOnSuccess_IfResultIsOfWrongType()
        {
            // Arrange
            var called = false;

            // Act
            var myDelegate = Delegates.GetDelegateFor<string>(onSuccess: _ => called = true);
            myDelegate(result: 42, exception: null);

            ClassicAssert.False(called);
        }

        [Test]
        public void GetDelegateFor_DoesNotRunOnSuccess_IfException()
        {
            // Arrange
            var called = false;

            // Act
            var myDelegate = Delegates.GetDelegateFor(onSuccess: () => called = true);
            myDelegate(result: null, exception: new Exception());

            ClassicAssert.False(called);
        }

        #endregion OnSuccess

        #region OnFailure

        [Test]
        public void GetDelegateFor_RunsOnFailure_IfException()
        {
            // Arrange
            var expectedException = new Exception();
            var passedException = null as Exception;

            // Act
            var myDelegate = Delegates.GetDelegateFor(onFailure: ex => passedException = ex);
            myDelegate(result: null, exception: expectedException);

            ClassicAssert.AreEqual(expectedException, passedException);
        }

        [Test]
        public void GetDelegateFor_DoesNotRunOnFailure_IfNoException()
        {
            // Arrange
            var called = false;

            // Act
            var myDelegate = Delegates.GetDelegateFor(onFailure: _ => called = true);
            myDelegate(result: null, exception: null);

            ClassicAssert.False(called);
        }

        #endregion OnFailure

        #region OnComplete

        [Test]
        public void GetDelegateFor_RunsOnComplete_IfNoException()
        {
            // Arrange
            var called = false;

            // Act
            var myDelegate = Delegates.GetDelegateFor(onComplete: () => called = true);
            myDelegate(result: null, exception: null);

            Assert.That(called);
        }

        [Test]
        public void GetDelegateFor_RunsOnComplete_IfException()
        {
            // Arrange
            var called = false;

            // Act
            var myDelegate = Delegates.GetDelegateFor(onComplete: () => called = true);
            myDelegate(result: null, exception: new Exception());

            Assert.That(called);
        }

        #endregion OnComplete

        #region Order of calls

        [Test]
        public void GetDelegateFor_RunsOnCompleteAfterOnSuccess()
        {
            // Arrange
            var expectedThingsCalled = new[] { "onSuccess", "onComplete" };
            var thingsCalled = new List<string>();

            // Act
            var myDelegate = Delegates.GetDelegateFor(
                onComplete: () => thingsCalled.Add("onComplete"),
                onSuccess: () => thingsCalled.Add("onSuccess")
                );
            myDelegate(result: null, exception: null);

            ClassicAssert.AreEqual(expectedThingsCalled, thingsCalled);
        }

        [Test]
        public void GetDelegateFor_RunsOnCompleteAfterOnFailure()
        {
            // Arrange
            var expectedThingsCalled = new[] { "onFailure", "onComplete" };
            var thingsCalled = new List<string>();

            // Act
            var myDelegate = Delegates.GetDelegateFor(
                onComplete: () => thingsCalled.Add("onComplete"),
                onFailure: _ => thingsCalled.Add("onFailure")
                );
            myDelegate(result: null, exception: new Exception());

            ClassicAssert.AreEqual(expectedThingsCalled, thingsCalled);
        }

        #endregion Order of calls
    }
}
