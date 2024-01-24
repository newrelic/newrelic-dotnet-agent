// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NewRelic.Agent.Core.Attributes.Tests.Models;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Attributes.Tests
{
    public class AttributeFilterTests
    {
        private static string TestCaseData
        {
            get
            {
                return
@"[
  {
	""testname"":""everything enabled, no include/exclude"",
	""config"":
	{
	  ""browser_monitoring.attributes.enabled"":true
	},
	""input_key"":""alpha"",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"", ""browser_monitoring""],
	""expected_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""]
  },

  {
	""testname"":""browser monitoring attributes disabled by default"",
	""config"":
	{
	},
	""input_key"":""alpha"",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"", ""browser_monitoring""],
	""expected_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector""]
  },

  {
	""testname"":""attributes globally disabled"",
	""config"":
	{
	  ""attributes.enabled"":false
	},
	""input_key"":""alpha"",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""],
	""expected_destinations"":
	  []
  },

  {
	""testname"":""all categories disabled"",
	""config"":
	{
	  ""transaction_events.attributes.enabled"":false,
	  ""transaction_tracer.attributes.enabled"":false,
	  ""error_collector.attributes.enabled"":false
	},
	""input_key"":""alpha"",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""],
	""expected_destinations"":
	  []
  },

  {
	""testname"":""global exclude"",
	""config"":
	{
	  ""attributes.exclude"":[""alpha""],
	  ""browser_monitoring.attributes.enabled"":true
	},
	""input_key"":""alpha"",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""],
	""expected_destinations"":
	  []
  },

  {
	""testname"":""exclude in each category"",
	""config"":
	{
	  ""transaction_events.attributes.exclude"":[""alpha""],
	  ""transaction_tracer.attributes.exclude"":[""alpha""],
	  ""error_collector.attributes.exclude"":[""alpha""],
	  ""browser_monitoring.attributes.enabled"":true,
	  ""browser_monitoring.attributes.exclude"":[""alpha""]
	},
	""input_key"":""alpha"",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""],
	""expected_destinations"":
	  []
  },

  {
	""testname"":""global include"",
	""config"":
	{
	  ""attributes.include"":[""alpha""],
	  ""browser_monitoring.attributes.enabled"":true
	},
	""input_key"":""alpha"",
	""input_default_destinations"":
	  [],
	""expected_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""]
  },

  {
	""testname"":""each category include"",
	""config"":
	{
	  ""transaction_events.attributes.include"":[""alpha""],
	  ""transaction_tracer.attributes.include"":[""alpha""],
	  ""error_collector.attributes.include"":[""alpha""],
	  ""browser_monitoring.attributes.enabled"":true,
	  ""browser_monitoring.attributes.include"":[""alpha""]
	},
	""input_key"":""alpha"",
	""input_default_destinations"":
	  [],
	""expected_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""]
  },

  {
	""testname"":""global include/exclude contradict"",
	""config"":
	{
	  ""attributes.exclude"":[""alpha""],
	  ""attributes.include"":[""alpha""],
	  ""browser_monitoring.attributes.enabled"":true
	},
	""input_key"":""alpha"",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""],
	""expected_destinations"":
	  []
  },

  {
	""testname"":""include/exclude contradict in each category"",
	""config"":
	{
	  ""transaction_events.attributes.exclude"":[""alpha""],
	  ""transaction_events.attributes.include"":[""alpha""],
	  ""transaction_tracer.attributes.exclude"":[""alpha""],
	  ""transaction_tracer.attributes.include"":[""alpha""],
	  ""error_collector.attributes.exclude"":[""alpha""],
	  ""error_collector.attributes.include"":[""alpha""],
	  ""browser_monitoring.attributes.enabled"":true,
	  ""browser_monitoring.attributes.exclude"":[""alpha""],
	  ""browser_monitoring.attributes.include"":[""alpha""]
	},
	""input_key"":""alpha"",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""],
	""expected_destinations"":
	  []
  },

  {
	""testname"":""global exclude contradicts category include"",
	""config"":
	{
	  ""attributes.exclude"":[""alpha""],
	  ""transaction_events.attributes.include"":[""alpha""],
	  ""transaction_tracer.attributes.include"":[""alpha""],
	  ""error_collector.attributes.include"":[""alpha""],
	  ""browser_monitoring.attributes.enabled"":true,
	  ""browser_monitoring.attributes.include"":[""alpha""]
	},
	""input_key"":""alpha"",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""],
	""expected_destinations"":
	  []
  },

  {
	""testname"":""global include contradicts category exclude"",
	""config"":
	{
	  ""attributes.include"":[""alpha""],
	  ""transaction_events.attributes.exclude"":[""alpha""],
	  ""transaction_tracer.attributes.exclude"":[""alpha""],
	  ""error_collector.attributes.exclude"":[""alpha""],
	  ""browser_monitoring.attributes.enabled"":true,
	  ""browser_monitoring.attributes.exclude"":[""alpha""]
	},
	""input_key"":""alpha"",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""],
	""expected_destinations"":
	  []
  },

  {
	""testname"":""alpha is more specific than alpha*"",
	""config"":
	{
	  ""attributes.include"":[""alpha""],
	  ""transaction_events.attributes.exclude"":[""alpha*""],
	  ""transaction_tracer.attributes.exclude"":[""alpha*""],
	  ""error_collector.attributes.exclude"":[""alpha*""],
	  ""browser_monitoring.attributes.enabled"":true,
	  ""browser_monitoring.attributes.exclude"":[""alpha*""]
	},
	""input_key"":""alpha"",
	""input_default_destinations"":
	  [],
	""expected_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""]
  },

  {
	""testname"":""all destination modifiers applied, not only the most specific one"",
	""config"":
	{
	  ""attributes.exclude"":[""a*""],
	  ""transaction_events.attributes.include"":[""ab*""],
	  ""transaction_events.attributes.exclude"":[""abc*""],
	  ""transaction_tracer.attributes.exclude"":[""abcd*""],
	  ""transaction_tracer.attributes.include"":[""abcde*""],
	  ""error_collector.attributes.include"":   [""abcdef*""],
	  ""error_collector.attributes.exclude"":   [""abcdefg*""],
	  ""browser_monitoring.attributes.exclude"":[""abcdefgh*""],
	  ""browser_monitoring.attributes.include"":[""abcdefghi*""],
	  ""browser_monitoring.attributes.enabled"":true
	},
	""input_key"":""abcdefghik"",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""],
	""expected_destinations"":
	  [""transaction_tracer"",""browser_monitoring""]
  },


  {
	""testname"":""venn diagram part 1"",
	""config"":
	{
	  ""attributes.exclude"":[""alpha.*"", ""alpha.beta.gamma.*""],
	  ""attributes.include"":[""alpha.beta.*""],
	  ""browser_monitoring.attributes.enabled"":true
	},
	""input_key"":""alpha."",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""],
	""expected_destinations"":
	  []
  },

  {
	""testname"":""venn diagram part 2"",
	""config"":
	{
	  ""attributes.exclude"":[""alpha.*"", ""alpha.beta.gamma.*""],
	  ""attributes.include"":[""alpha.beta.*""],
	  ""browser_monitoring.attributes.enabled"":true
	},
	""input_key"":""alpha.psi"",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""],
	""expected_destinations"":
	  []
  },

  {
	""testname"":""venn diagram part 3"",
	""config"":
	{
	  ""attributes.exclude"":[""alpha.*"", ""alpha.beta.gamma.*""],
	  ""attributes.include"":[""alpha.beta.*""],
	  ""browser_monitoring.attributes.enabled"":true
	},
	""input_key"":""alpha.beta."",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""],
	""expected_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""]
  },

  {
	""testname"":""venn diagram part 4"",
	""config"":
	{
	  ""attributes.exclude"":[""alpha.*"", ""alpha.beta.gamma.*""],
	  ""attributes.include"":[""alpha.beta.*""],
	  ""browser_monitoring.attributes.enabled"":true
	},
	""input_key"":""alpha.beta.psi"",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""],
	""expected_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""]
  },

  {
	""testname"":""venn diagram part 5"",
	""config"":
	{
	  ""attributes.exclude"":[""alpha.*"", ""alpha.beta.gamma.*""],
	  ""attributes.include"":[""alpha.beta.*""],
	  ""browser_monitoring.attributes.enabled"":true
	},
	""input_key"":""alpha.beta.gamma."",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""],
	""expected_destinations"":
	  []
  },

  {
	""testname"":""alpha is not mistaken for alpha*"",
	""config"":
	{
	  ""transaction_events.attributes.include"":[""alpha""],
	  ""transaction_tracer.attributes.exclude"":[""alpha*""],
	  ""error_collector.attributes.include"":   [""alpha""],
	  ""browser_monitoring.attributes.exclude"":[""alpha*""],
	  ""browser_monitoring.attributes.enabled"":true
	},
	""input_key"":""alpha.beta"",
	""input_default_destinations"":
	  [""transaction_tracer"",""browser_monitoring""],
	""expected_destinations"":
	  []
  },

  {
	""testname"":""exact match is case sensitive"",
	""config"":
	{
	  ""attributes.exclude"":[""alpha""],
	  ""browser_monitoring.attributes.enabled"":true
	},
	""input_key"":""ALPHA"",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""],
	""expected_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""]
  },

  {
	""testname"":""wildcard match is case sensitive"",
	""config"":
	{
	  ""attributes.exclude"":[""alpha.*""],
	  ""browser_monitoring.attributes.enabled"":true
	},
	""input_key"":""ALPHA.BETA"",
	""input_default_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""],
	""expected_destinations"":
	  [""transaction_events"", ""transaction_tracer"", ""error_collector"",""browser_monitoring""]
  }
]";
            }
        }

        public static IEnumerable<TestCase[]> TestCases
        {
            get
            {
                return JsonConvert.DeserializeObject<IEnumerable<TestCase>>(TestCaseData)
                    .Select(testCase => new[] { testCase });
            }
        }

        [TestCaseSource(typeof(AttributeFilterTests), nameof(TestCases))]
        public void when(TestCase testCase)
        {
            // Arrange
            var unfilteredAttribs = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

            var attributeFilterSettings = testCase.Configuration.ToAttributeFilterSettings();
            var testCaseDestinations = testCase.AttributeDestinations.ToAttributeDestinations();

            testCaseDestinations = testCaseDestinations.Length > 0
                ? testCaseDestinations
                : new AttributeDestinations[]
                        {
                            AttributeDestinations.TransactionEvent,
                            AttributeDestinations.TransactionTrace,
                            AttributeDestinations.JavaScriptAgent,
                            AttributeDestinations.ErrorTrace
                        };

            var expectedDestinations = testCase.ExpectedDestinations.ToAttributeDestinations();
            var attributeFilter = new AttributeFilter(attributeFilterSettings);

            var attrib = AttributeDefinitionBuilder.Create<string>(testCase.AttributeKey, AttributeClassification.UserAttributes)
                .AppliesTo(testCaseDestinations)
                .Build(attributeFilter);

            attrib.TrySetValue(unfilteredAttribs, "foo");

            foreach(var testDestination in AttributeValueCollection.AllTargetModelTypes)
            {
                var filteredAttribs = new AttributeValueCollection(unfilteredAttribs, testDestination);

                var countMatchAttribValues = filteredAttribs.GetAttributeValues(AttributeClassification.UserAttributes)
                        .Count(x => x.AttributeDefinition.Name == testCase.AttributeKey);

                var expectedCount = expectedDestinations.Contains(testDestination) ? 1 : 0;

                Assert.That(countMatchAttribValues, Is.EqualTo(expectedCount), $"{testDestination}");

            }
        }
    }
}
