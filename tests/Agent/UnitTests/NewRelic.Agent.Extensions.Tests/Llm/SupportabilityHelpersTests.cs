// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Llm;
using NUnit.Framework;
using Telerik.JustMock;

namespace Agent.Extensions.Tests.Llm
{
    // When creating tests, make sure to not use duplicate model name since CreateModelIdSupportabilityMetricsForXXXX only creates the metric once.
    [TestFixture]
    public class SupportabilityHelpersTests
    {
        private IAgent _agent;

        [SetUp]
        public void Setup()
        {
            _agent = Mock.Create<IAgent>();
        }

        [TestCase("anthropic.claude-3-sonnet-20240229-v1:0", "anthropic", "claude-3")]
        [TestCase("us.anthropic.claude-3-sonnet-20240229-v1:0", "anthropic", "claude-3")]
        [TestCase("apac.anthropic.claude-3-sonnet-20240229-v1:0", "anthropic", "claude-3")]
        [TestCase("meta.llama3-2-3b-instruct-v1:0", "meta", "llama3")]
        [TestCase("us.meta.llama3-2-3b-instruct-v1:0", "meta", "llama3")]
        [TestCase("amazon.nova-lite-v1:0", "amazon", "nova-lite")]
        [TestCase("eu.amazon.nova-lite-v1:0", "amazon", "nova-lite")]
        [TestCase("amazon.titan-embed-text-v1", "amazon", "titan-embed")]
        [TestCase("us.amazon.titan-embed-text-v1", "amazon", "titan-embed")]
        [TestCase("ai21.jamba-1-5-large-v1:0", "ai21", "jamba")]
        [TestCase("apac.ai21.jamba-1-5-large-v1:0", "ai21", "jamba")]
        [TestCase("writer-palmyra-med-70b-32k", "writer", "palmyra")]
        public void Bedrock_ModelFormatsTests(string fullModel, string vendor, string model)
        {
            // Supportability/DotNet/LLM/{vendor}/{model}
            var expectedMetric = $"Supportability/DotNet/LLM/{vendor}/{model}";
            var actualMetric = string.Empty;
            Mock.Arrange(() => _agent.RecordSupportabilityMetric(Arg.AnyString, Arg.AnyLong))
                .DoInstead((string m, long c) => actualMetric = $"Supportability/{m}");

            SupportabilityHelpers.CreateModelIdSupportabilityMetricsForBedrock(fullModel, _agent);

            Assert.That(actualMetric == expectedMetric, $"Model: '{fullModel}', Actual: '{actualMetric}', Expected: '{expectedMetric}'");
        }

        [TestCase("o3-mini", "openai", "o3-mini")]
        [TestCase("gpt-4o-2024-11-20", "openai", "gpt-4o")]
        public void OpenAi_ModelFormatsTests(string fullModel, string vendor, string model)
        {
            // Supportability/DotNet/LLM/{vendor}/{model}
            var expectedMetric = $"Supportability/DotNet/LLM/{vendor}/{model}";
            var actualMetric = string.Empty;
            Mock.Arrange(() => _agent.RecordSupportabilityMetric(Arg.AnyString, Arg.AnyLong))
                .DoInstead((string m, long c) => actualMetric = $"Supportability/{m}");

            SupportabilityHelpers.CreateModelIdSupportabilityMetricsForOpenAi(fullModel, _agent);

            Assert.That(actualMetric == expectedMetric, $"Model: '{fullModel}', Actual: '{actualMetric}', Expected: '{expectedMetric}'");
        }

        [TestCase("bedrock", "")]
        [TestCase("bedrock", "bedrock.bad.model.more.than.four.sections")]
        [TestCase("openai", "")]
        public void BadModel_NoMetricTest(string source, string model)
        {
            // Supportability/DotNet/LLM/{vendor}/{model}
            var actualMetric = string.Empty;
            Mock.Arrange(() => _agent.RecordSupportabilityMetric(Arg.AnyString, Arg.AnyLong))
                .DoInstead((string m, long c) => actualMetric = $"Supportability/{m}"); // Will not get called

            // Model is always stored so we want to check different values
            if (source == "bedrock")
            {
                SupportabilityHelpers.CreateModelIdSupportabilityMetricsForBedrock(model, _agent);
            }
            else if (source == "openai")
            {
                SupportabilityHelpers.CreateModelIdSupportabilityMetricsForOpenAi(model, _agent);
            }

            Assert.That(actualMetric == string.Empty);
        }

        [Test]
        public void Bedrock_DuplicateModels_OnlyOneMetricTest()
        {
            var fullModel = "luma.ray-v2:0";

            // Supportability/DotNet/LLM/{vendor}/{model}
            var expectedMetric = $"Supportability/DotNet/LLM/luma/ray";
            var actualMetrics = new List<string>();
            Mock.Arrange(() => _agent.RecordSupportabilityMetric(Arg.AnyString, Arg.AnyLong))
                .DoInstead((string m, long c) => actualMetrics.Add($"Supportability/{m}"));

            SupportabilityHelpers.CreateModelIdSupportabilityMetricsForBedrock(fullModel, _agent);
            SupportabilityHelpers.CreateModelIdSupportabilityMetricsForBedrock(fullModel, _agent);
            SupportabilityHelpers.CreateModelIdSupportabilityMetricsForBedrock(fullModel, _agent);
            SupportabilityHelpers.CreateModelIdSupportabilityMetricsForBedrock(fullModel, _agent);

            Assert.That(actualMetrics.Count == 1);
            Assert.That(actualMetrics[0] == expectedMetric, $"Model: '{fullModel}', Actual: '{actualMetrics[0]}', Expected: '{expectedMetric}'");
        }

        [Test]
        public void OpenAi_DuplicateModels_OnlyOneMetricTest()
        {
            var fullModel = "gpt-4.5";

            // Supportability/DotNet/LLM/{vendor}/{model}
            var expectedMetric = $"Supportability/DotNet/LLM/openai/gpt-4.5";
            var actualMetrics = new List<string>();
            Mock.Arrange(() => _agent.RecordSupportabilityMetric(Arg.AnyString, Arg.AnyLong))
                .DoInstead((string m, long c) => actualMetrics.Add($"Supportability/{m}"));

            SupportabilityHelpers.CreateModelIdSupportabilityMetricsForOpenAi(fullModel, _agent);
            SupportabilityHelpers.CreateModelIdSupportabilityMetricsForOpenAi(fullModel, _agent);
            SupportabilityHelpers.CreateModelIdSupportabilityMetricsForOpenAi(fullModel, _agent);
            SupportabilityHelpers.CreateModelIdSupportabilityMetricsForOpenAi(fullModel, _agent);

            Assert.That(actualMetrics.Count == 1);
            Assert.That(actualMetrics[0] == expectedMetric, $"Model: '{fullModel}', Actual: '{actualMetrics[0]}', Expected: '{expectedMetric}'");
        }

        [Test]
        public void Bedrock_Exception_Test()
        {
            var fullModel = "meta.llama3-1-70b-instruct-v1:0";
            var exception = new Exception("Test exception");
            var expectedExceptionMessage = $"Error creating model supportability metric for {fullModel}: {exception.Message}";

            // Supportability/DotNet/LLM/{vendor}/{model}
            Mock.Arrange(() => _agent.RecordSupportabilityMetric(Arg.AnyString, Arg.AnyLong))
                .Throws(exception);

            var exceptionMessage = string.Empty;
            Mock.Arrange(() => _agent.Logger.Finest(Arg.AnyString))
                .DoInstead((string m) => exceptionMessage = m);

            SupportabilityHelpers.CreateModelIdSupportabilityMetricsForBedrock(fullModel, _agent);

            Assert.That(exceptionMessage == expectedExceptionMessage, message: exceptionMessage);
        }

        [Test]
        public void OpenAi_Exception_Test()
        {
            var fullModel = "chatgpt-4o";
            var exception = new Exception("Test exception");
            var expectedExceptionMessage = $"Error creating model supportability metric for {fullModel}: {exception.Message}";

            // Supportability/DotNet/LLM/{vendor}/{model}
            Mock.Arrange(() => _agent.RecordSupportabilityMetric(Arg.AnyString, Arg.AnyLong))
                .Throws(exception);

            var exceptionMessage = string.Empty;
            Mock.Arrange(() => _agent.Logger.Finest(Arg.AnyString))
                .DoInstead((string m) => exceptionMessage = m);

            SupportabilityHelpers.CreateModelIdSupportabilityMetricsForOpenAi(fullModel, _agent);

            Assert.That(exceptionMessage == expectedExceptionMessage, message: exceptionMessage);
        }
    }
}
