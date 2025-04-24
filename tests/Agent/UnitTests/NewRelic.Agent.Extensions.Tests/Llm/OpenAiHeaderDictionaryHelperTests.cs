// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using System.Collections.Generic;
using NewRelic.Agent.Extensions.Llm.OpenAi;

namespace NewRelic.Agent.Extensions.Tests.Llm.OpenAi
{
    [TestFixture]
    public class OpenAiHeaderDictionaryHelperTests
    {
        [Test]
        public void GetOpenAiHeaders_ShouldReturnCorrectHeaders()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "openai-version", "1.0" },
                { "x-ratelimit-limit-requests", "1000" },
                { "x-ratelimit-limit-tokens", "5000" },
                { "x-ratelimit-remaining-requests", "900" },
                { "x-ratelimit-remaining-tokens", "4500" },
                { "x-ratelimit-reset-requests", "60" },
                { "x-ratelimit-reset-tokens", "30" }
            };

            // Act
            var result = headers.GetOpenAiHeaders();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Has.Count.EqualTo(7));
                Assert.That(result["llmVersion"], Is.EqualTo("1.0"));
                Assert.That(result["ratelimitLimitRequests"], Is.EqualTo("1000"));
                Assert.That(result["ratelimitLimitTokens"], Is.EqualTo("5000"));
                Assert.That(result["ratelimitRemainingRequests"], Is.EqualTo("900"));
                Assert.That(result["ratelimitRemainingTokens"], Is.EqualTo("4500"));
                Assert.That(result["ratelimitResetRequests"], Is.EqualTo("60"));
                Assert.That(result["ratelimitResetTokens"], Is.EqualTo("30"));
            });
        }

        [Test]
        public void TryGetOpenAiOrganization_ShouldReturnOrganization_WhenHeaderExists()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "openai-organization", "org-12345" }
            };

            // Act
            var result = headers.TryGetOpenAiOrganization();

            // Assert
            Assert.That(result, Is.EqualTo("org-12345"));
        }

        [Test]
        public void TryGetOpenAiOrganization_ShouldReturnNull_WhenHeaderDoesNotExist()
        {
            // Arrange
            var headers = new Dictionary<string, string>();

            // Act
            var result = headers.TryGetOpenAiOrganization();

            // Assert
            Assert.That(result, Is.Null);
        }
    }
}
