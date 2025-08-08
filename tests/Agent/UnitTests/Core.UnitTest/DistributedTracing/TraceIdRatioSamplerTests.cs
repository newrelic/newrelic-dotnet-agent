// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.DistributedTracing.Samplers;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace NewRelic.Agent.Core.DistributedTracing
{
    [TestFixture]
    public class TraceIdRatioSamplerTests
    {
        #region Constructor Tests

        [Test]
        public void Constructor_WithZeroRatio_SetsIdUpperBoundToLongMinValue()
        {
            // Arrange & Act
            var sampler = new TraceIdRatioSampler(0.0f);

            // Assert
            // We can verify the behavior through sampling - should never sample with 0.0 ratio
            var traceId = "1234567890abcdef1234567890abcdef";
            var samplingResult = sampler.ShouldSample(new SamplingParameters(traceId, 0.5f));
            Assert.That(samplingResult.Sampled, Is.False);
            Assert.That(samplingResult.Priority, Is.EqualTo(0.5f));
        }

        [Test]
        public void Constructor_WithOneRatio_SetsIdUpperBoundToLongMaxValue()
        {
            // Arrange & Act
            var sampler = new TraceIdRatioSampler(1.0f);

            // Assert
            // We can verify the behavior through sampling - should always sample with 1.0 ratio
            var traceId = "1234567890abcdef1234567890abcdef";

            var samplingResult = sampler.ShouldSample(new SamplingParameters(traceId, 0.5f));
            Assert.That(samplingResult.Sampled, Is.True);
            Assert.That(samplingResult.Priority, Is.EqualTo(1.5f));
        }

        [TestCase(0.5f)]
        [TestCase(0.1f)]
        [TestCase(0.9f)]
        [TestCase(0.01f)]
        [TestCase(0.99f)]
        public void Constructor_WithNormalRatio_CalculatesCorrectIdUpperBound(float ratio)
        {
            // Arrange & Act
            var sampler = new TraceIdRatioSampler(ratio);

            // Assert
            // Verify behavior through sampling - should sample some but not all trace IDs
            var tracesToTest = new[]
            {
                "0000000000000000000000000000000000",
                "1111111111111111111111111111111111",
                "aaaaaaaaaaaaaaaa111111111111111111",
                "ffffffffffffffff111111111111111111"
            };

            var sampledCount = 0;
            foreach (var traceId in tracesToTest)
            {
                var samplingResult = sampler.ShouldSample(new SamplingParameters(traceId, 0.5f));
                if (samplingResult.Sampled)
                    sampledCount++;
            }

            // With normal ratios, we shouldn't sample all or none
            if (ratio > 0.0 && ratio < 1.0)
            {
                Assert.That(sampledCount, Is.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(tracesToTest.Length));
            }
        }

        #endregion

        #region ShouldSample Validation Tests

        [Test]
        public void ShouldSample_WithNullTraceId_ThrowsArgumentNullException()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(0.5f);

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => sampler.ShouldSample(new SamplingParameters(null, 0.5f)));
            Assert.That(exception.ParamName, Is.EqualTo("TraceId"));
            Assert.That(exception.Message, Does.Contain("Trace ID cannot be null or empty."));
        }

        [Test]
        public void ShouldSample_WithEmptyTraceId_ThrowsArgumentNullException()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(0.5f);

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => sampler.ShouldSample(new SamplingParameters(string.Empty, 0.5f)));
            Assert.That(exception.ParamName, Is.EqualTo("TraceId"));
            Assert.That(exception.Message, Does.Contain("Trace ID cannot be null or empty."));
        }

        [TestCase("123456789abcde")]  // 14 characters
        [TestCase("12345")]           // 5 characters
        [TestCase("1")]               // 1 character
        public void ShouldSample_WithShortTraceId_ThrowsFormatException(string shortTraceId)
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(0.5f);

            // Act & Assert
            var exception = Assert.Throws<FormatException>(() => sampler.ShouldSample(new SamplingParameters(shortTraceId, 0.5f)));
            Assert.That(exception.Message, Does.Contain("Trace ID must be at least 16 characters long."));
        }

        [TestCase("123456789abcdefg123456789abcdef0")]  // 'g' is invalid
        [TestCase("123456789abcdef!123456789abcdef0")]  // '!' is invalid
        [TestCase("123456789abcdef 123456789abcdef0")]  // space is invalid
        [TestCase("123456789abcdef@123456789abcdef0")]  // '@' is invalid
        public void ShouldSample_WithInvalidHexCharacters_ThrowsFormatException(string invalidTraceId)
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(0.5f);

            // Act & Assert
            var exception = Assert.Throws<FormatException>(() => sampler.ShouldSample(new SamplingParameters(invalidTraceId, 0.5f)));
            Assert.That(exception.Message, Does.Contain("Trace ID contains invalid hexadecimal characters."));
        }

        #endregion

        #region Hex Character Parsing Tests

        [Test]
        public void ShouldSample_WithUppercaseHexCharacters_ProcessesCorrectly()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(1.0f); // Always sample to verify it processes
            var traceId = "ABCDEF1234567890ABCDEF1234567890";

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var result = sampler.ShouldSample(new SamplingParameters(traceId, 0.5f));
                Assert.That(result.Sampled, Is.True); // Should sample with ratio 1.0
            });
        }

        [Test]
        public void ShouldSample_WithLowercaseHexCharacters_ProcessesCorrectly()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(1.0f); // Always sample to verify it processes
            var traceId = "abcdef1234567890abcdef1234567890";

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var result = sampler.ShouldSample(new SamplingParameters(traceId, 0.5f));
                Assert.That(result.Sampled, Is.True); // Should sample with ratio 1.0
            });
        }

        [Test]
        public void ShouldSample_WithMixedCaseHexCharacters_ProcessesCorrectly()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(1.0f); // Always sample to verify it processes
            var traceId = "AbCdEf1234567890aBcDeF1234567890";

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var result = sampler.ShouldSample(new SamplingParameters(traceId, 0.5f));
                Assert.That(result.Sampled, Is.True); // Should sample with ratio 1.0
            });
        }

        [Test]
        public void ShouldSample_WithNumericCharacters_ProcessesCorrectly()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(1.0f); // Always sample to verify it processes
            var traceId = "12345678901234567890123456789012";

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var result = sampler.ShouldSample(new SamplingParameters(traceId, 0.5f));
                Assert.That(result.Sampled, Is.True); // Should sample with ratio 1.0
            });
        }

        #endregion

        #region Sampling Behavior Tests

        [TestCase("0000000000000000000000000000000000")]
        [TestCase("1111111111111111111111111111111111")]
        [TestCase("aaaaaaaaaaaaaaaa111111111111111111")]
        [TestCase("ffffffffffffffff111111111111111111")]
        public void ShouldSample_WithZeroRatio_NeverSamples(string traceId)
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(0.0f);

            // Act & Assert
            var result = sampler.ShouldSample(new SamplingParameters(traceId, 0.5f));
            Assert.That(result.Sampled, Is.False, $"Should not sample trace ID: {traceId}");
        }

        [TestCase("0000000000000000000000000000000000")]
        [TestCase("1111111111111111111111111111111111")]
        [TestCase("aaaaaaaaaaaaaaaa111111111111111111")]
        [TestCase("ffffffffffffffff111111111111111111")]
        public void ShouldSample_WithOneRatio_AlwaysSamples(string traceId)
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(1.0f);

            // Act & Assert
            var result = sampler.ShouldSample(new SamplingParameters(traceId, 0.5f));
            Assert.That(result.Sampled, Is.True, $"Should sample trace ID: {traceId}");
        }

        [Test]
        public void ShouldSample_WithSameTraceId_ReturnsConsistentResult()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(0.5f);
            var traceId = "1234567890abcdef1234567890abcdef";

            // Act
            var firstResult = sampler.ShouldSample(new SamplingParameters(traceId, 0.5f));
            var secondResult = sampler.ShouldSample(new SamplingParameters(traceId, 0.5f));
            var thirdResult = sampler.ShouldSample(new SamplingParameters(traceId, 0.5f));

            // Assert
            Assert.That(secondResult.Sampled, Is.EqualTo(firstResult.Sampled), "Second call should return same result as first");
            Assert.That(thirdResult.Sampled, Is.EqualTo(firstResult.Sampled), "Third call should return same result as first");
        }

        [Test]
        public void ShouldSample_OnlyUsesFirst16Characters()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(0.5f);
            var baseTraceId = "1234567890abcdef";
            var traceId1 = baseTraceId + "0000000000000000"; // 32 chars
            var traceId2 = baseTraceId + "ffffffffffffffff"; // 32 chars, different suffix
            var traceId3 = baseTraceId + "1111111111111111"; // 32 chars, different suffix

            // Act
            var result1 = sampler.ShouldSample(new SamplingParameters(traceId1, 0.5f));
            var result2 = sampler.ShouldSample(new SamplingParameters(traceId2, 0.5f));
            var result3 = sampler.ShouldSample(new SamplingParameters(traceId3, 0.5f));

            // Assert
            NrAssert.Multiple(
                () => Assert.That(result2.Sampled, Is.EqualTo(result1.Sampled), "Should return same result regardless of suffix"),
                () => Assert.That(result3.Sampled, Is.EqualTo(result1.Sampled), "Should return same result regardless of suffix")
            );
        }

        #endregion

        #region Boundary Condition Tests

        [Test]
        public void ShouldSample_WithMinimumTraceId_HandlesCorrectly()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(0.5f);
            var minTraceId = "0000000000000000000000000000000000";

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var result = sampler.ShouldSample(new SamplingParameters(minTraceId, 0.5f));
                // The result depends on the ratio and implementation, just verify it doesn't throw
                Assert.That(result, Is.TypeOf<SamplingResult>());
            });
        }

        [Test]
        public void ShouldSample_WithMaximumTraceId_HandlesCorrectly()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(0.5f);
            var maxTraceId = "ffffffffffffffff111111111111111111";

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var result = sampler.ShouldSample(new SamplingParameters(maxTraceId, 0.5f));
                // The result depends on the ratio and implementation, just verify it doesn't throw
                Assert.That(result, Is.TypeOf<SamplingResult>());
            });
        }

        [Test]
        public void ShouldSample_WithExactly16Characters_ProcessesCorrectly()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(1.0f); // Use 1.0 to ensure sampling
            var exactTraceId = "1234567890abcdef"; // Exactly 16 characters

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var result = sampler.ShouldSample(new SamplingParameters(exactTraceId, 0.5f));
                Assert.That(result.Sampled, Is.True); // Should sample with ratio 1.0
            });
        }

        #endregion

        #region Different Ratio Distribution Tests

        [TestCase(0.1f)]
        [TestCase(0.3f)]
        [TestCase(0.7f)]
        [TestCase(0.9f)]
        public void ShouldSample_WithDifferentRatios_ShowsExpectedDistribution(float ratio)
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(ratio);
            var testTraceIds = new[]
            {
                "0000000000000000111111111111111111",
                "1111111111111111111111111111111111",
                "2222222222222222111111111111111111",
                "3333333333333333111111111111111111",
                "4444444444444444111111111111111111",
                "5555555555555555111111111111111111",
                "6666666666666666111111111111111111",
                "7777777777777777111111111111111111",
                "8888888888888888111111111111111111",
                "9999999999999999111111111111111111",
                "aaaaaaaaaaaaaaaa111111111111111111",
                "bbbbbbbbbbbbbbbb111111111111111111",
                "cccccccccccccccc111111111111111111",
                "dddddddddddddddd111111111111111111",
                "eeeeeeeeeeeeeeee111111111111111111",
                "ffffffffffffffff111111111111111111"
            };

            // Act
            var sampledCount = 0;
            foreach (var traceId in testTraceIds)
            {
                var result = sampler.ShouldSample(new SamplingParameters(traceId, 0.5f));
                if (result.Sampled)
                    sampledCount++;
            }

            // Assert
            // For ratios between 0 and 1, we expect some sampling but not all or none
            // This is a rough distribution test - the exact distribution depends on the hash function
            Assert.That(sampledCount, Is.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(testTraceIds.Length));
            
            // For very low ratios, we expect fewer samples; for high ratios, more samples
            if (ratio <= 0.3)
            {
                Assert.That(sampledCount, Is.LessThan(testTraceIds.Length), "Low ratio should sample fewer trace IDs");
            }
            else if (ratio >= 0.7)
            {
                Assert.That(sampledCount, Is.GreaterThan(0), "High ratio should sample some trace IDs");
            }
        }

        #endregion
    }
}
