// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Attributes;
using System.Linq;
using NUnit.Framework;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using Telerik.JustMock;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Spans;

namespace NewRelic.Agent.Core.Segments.Tests
{
    [TestFixture]
    public class CustomSegmentDataTests
    {
        private ITransactionSegmentState _transactionSegmentState;

        [SetUp]
        public void SetUp()
        {
            _transactionSegmentState = Mock.Create<ITransactionSegmentState>();
            Mock.Arrange(() => _transactionSegmentState.AttribDefs).Returns(() => new AttributeDefinitions(new AttributeFilter(new AttributeFilter.Settings())));

        }

        #region IsCombinableWith


        private Segment CreateCustomSegmentBuilder(MethodCallData methodCallData, string name, bool combinable)
        {
            var customSegmentData = new CustomSegmentData(name);
            var segment = new Segment(_transactionSegmentState, methodCallData);
            segment.Combinable = combinable;
            segment.SetSegmentData(customSegmentData);

            return segment;
        }

        [Test]
        public void IsCombinableWith_ReturnsTrue_ForIdenticalSegments()
        {
            var segment1 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", true);
            var segment2 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.True);
        }


        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentCombinable()
        {
            var segment1 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", true);
            var segment2 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", false);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfBothNotCombinable()
        {
            var segment1 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", false);
            var segment2 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", false);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentHashCode()
        {
            var segment1 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", true);
            var segment2 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 2), "name", true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentTypeName()
        {
            var segment1 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", true);
            var segment2 = CreateCustomSegmentBuilder(new MethodCallData("type2", "method", 1), "name", true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentMethodName()
        {
            var segment1 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", true);
            var segment2 = CreateCustomSegmentBuilder(new MethodCallData("type", "method2", 1), "name", true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentName()
        {
            var segment1 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", true);
            var segment2 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name2", true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentSegmentType()
        {
            var segment1 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", true);
            var segment2 = MethodSegmentDataTestHelpers.CreateMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "type", "method", true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        #endregion IsCombinableWith
    }
}
