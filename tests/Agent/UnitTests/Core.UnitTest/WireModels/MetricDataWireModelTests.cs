// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace NewRelic.Agent.Core.WireModels
{
    [TestFixture]
    public class MetricDataWireModelTests
    {

        [Test]
        public void BuildCountData_CorrectsNegativeValues()
        {
            var actualData = MetricDataWireModel.BuildCountData(-30);

            Assert.AreEqual(MetricDataWireModel.BuildCountData(0), actualData);
        }

        [Test]
        public void BuildCountData_IdentityTest()
        {
            var input = new Random().Next();

            var actualData = MetricDataWireModel.BuildCountData(input);

            NrAssert.Multiple(
                () => Assert.AreEqual(actualData.Value0, input),
                () => Assert.AreEqual(actualData.Value1, 0),
                () => Assert.AreEqual(actualData.Value2, 0),
                () => Assert.AreEqual(actualData.Value3, 0),
                () => Assert.AreEqual(actualData.Value4, 0),
                () => Assert.AreEqual(actualData.Value5, 0)
            );
        }

    }
}
