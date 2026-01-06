// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    [TestFixture]
    public class ActivitySourceExtensionsTests
    {
        private static IConfiguration CreateConfig(
            List<string> defaultExcluded = null,
            List<string> customerIncluded = null,
            List<string> customerExcluded = null)
        {
            var config = Mock.Create<IConfiguration>();
            Mock.Arrange(() => config.OpenTelemetryTracingDefaultExcludedActivitySources)
                .Returns(defaultExcluded ?? new List<string>());
            Mock.Arrange(() => config.OpenTelemetryTracingIncludedActivitySources)
                .Returns(customerIncluded ?? new List<string>());
            Mock.Arrange(() => config.OpenTelemetryTracingExcludedActivitySources)
                .Returns(customerExcluded ?? new List<string>());
            return config;
        }

        [Test]
        public void NotInAnyExcludeLists_IncludedByDefault()
        {
            var config = CreateConfig(
                defaultExcluded: new List<string>(),
                customerIncluded: new List<string>(),
                customerExcluded: new List<string>());

            var result = ActivitySourceExtensions.ShouldListenToActivitySource("MySource", config);

            Assert.That(result, Is.True);
        }

        [Test]
        public void InDefaultExcluded_ButInCustomerIncluded_ShouldBeIncluded()
        {
            var config = CreateConfig(
                defaultExcluded: new List<string> { "Foo", "Bar" },
                customerIncluded: new List<string> { "Bar" },
                customerExcluded: new List<string>());

            var result = ActivitySourceExtensions.ShouldListenToActivitySource("Bar", config);

            Assert.That(result, Is.True, "Customer include list should override default excluded list.");
        }

        [Test]
        public void InCustomerIncluded_AndInCustomerExcluded_ExcludedWins()
        {
            var config = CreateConfig(
                defaultExcluded: new List<string> { "Alpha" },
                customerIncluded: new List<string> { "Foo", "Bar" },
                customerExcluded: new List<string> { "Bar" });

            var result = ActivitySourceExtensions.ShouldListenToActivitySource("Bar", config);

            Assert.That(result, Is.False, "Customer exclude list should override customer include list.");
        }

        [Test]
        public void IncludeListSpecified_OtherwiseNotExcluded_ShouldStillBeIncluded()
        {
            var config = CreateConfig(
                defaultExcluded: new List<string> { },
                customerIncluded: new List<string> { "Included.Source" },
                customerExcluded: new List<string> { });

            var result = ActivitySourceExtensions.ShouldListenToActivitySource("Other.Source", config);

            Assert.That(result, Is.True, "Activity sources not in any exclude list should be included by default, regardless of include list content.");
        }

        [Test]
        public void InBothDefaultExcluded_AndCustomerExcluded_ShouldBeExcluded()
        {
            var config = CreateConfig(
                defaultExcluded: new List<string> { "Baz" },
                customerIncluded: new List<string> { "Baz" },
                customerExcluded: new List<string> { "Baz" });

            var result = ActivitySourceExtensions.ShouldListenToActivitySource("Baz", config);

            Assert.That(result, Is.False, "Customer exclude should have highest priority.");
        }
    }
}
