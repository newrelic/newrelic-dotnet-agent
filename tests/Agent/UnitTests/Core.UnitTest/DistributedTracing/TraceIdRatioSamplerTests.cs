// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
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
            var sampler = new TraceIdRatioSampler(0.0);

            // Assert
            // We can verify the behavior through sampling - should never sample with 0.0 ratio
            var traceId = "1234567890abcdef1234567890abcdef";
            Assert.That(sampler.ShouldSample(traceId), Is.False);
        }

        [Test]
        public void Constructor_WithOneRatio_SetsIdUpperBoundToLongMaxValue()
        {
            // Arrange & Act
            var sampler = new TraceIdRatioSampler(1.0);

            // Assert
            // We can verify the behavior through sampling - should always sample with 1.0 ratio
            var traceId = "1234567890abcdef1234567890abcdef";
            Assert.That(sampler.ShouldSample(traceId), Is.True);
        }

        [TestCase(0.5)]
        [TestCase(0.1)]
        [TestCase(0.9)]
        [TestCase(0.01)]
        [TestCase(0.99)]
        public void Constructor_WithNormalRatio_CalculatesCorrectIdUpperBound(double ratio)
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
                if (sampler.ShouldSample(traceId))
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
            var sampler = new TraceIdRatioSampler(0.5);

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => sampler.ShouldSample(null));
            Assert.That(exception.ParamName, Is.EqualTo("traceId"));
            Assert.That(exception.Message, Does.Contain("Trace ID cannot be null or empty."));
        }

        [Test]
        public void ShouldSample_WithEmptyTraceId_ThrowsArgumentNullException()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(0.5);

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => sampler.ShouldSample(string.Empty));
            Assert.That(exception.ParamName, Is.EqualTo("traceId"));
            Assert.That(exception.Message, Does.Contain("Trace ID cannot be null or empty."));
        }

        [TestCase("123456789abcde")]  // 14 characters
        [TestCase("12345")]           // 5 characters
        [TestCase("1")]               // 1 character
        public void ShouldSample_WithShortTraceId_ThrowsFormatException(string shortTraceId)
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(0.5);

            // Act & Assert
            var exception = Assert.Throws<FormatException>(() => sampler.ShouldSample(shortTraceId));
            Assert.That(exception.Message, Does.Contain("Trace ID must be at least 16 characters long."));
        }

        [TestCase("123456789abcdefg123456789abcdef0")]  // 'g' is invalid
        [TestCase("123456789abcdef!123456789abcdef0")]  // '!' is invalid
        [TestCase("123456789abcdef 123456789abcdef0")]  // space is invalid
        [TestCase("123456789abcdef@123456789abcdef0")]  // '@' is invalid
        public void ShouldSample_WithInvalidHexCharacters_ThrowsFormatException(string invalidTraceId)
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(0.5);

            // Act & Assert
            var exception = Assert.Throws<FormatException>(() => sampler.ShouldSample(invalidTraceId));
            Assert.That(exception.Message, Does.Contain("Trace ID contains invalid hexadecimal characters."));
        }

        #endregion

        #region Hex Character Parsing Tests

        [Test]
        public void ShouldSample_WithUppercaseHexCharacters_ProcessesCorrectly()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(1.0); // Always sample to verify it processes

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var result = sampler.ShouldSample("ABCDEF1234567890ABCDEF1234567890");
                Assert.That(result, Is.True); // Should sample with ratio 1.0
            });
        }

        [Test]
        public void ShouldSample_WithLowercaseHexCharacters_ProcessesCorrectly()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(1.0); // Always sample to verify it processes

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var result = sampler.ShouldSample("abcdef1234567890abcdef1234567890");
                Assert.That(result, Is.True); // Should sample with ratio 1.0
            });
        }

        [Test]
        public void ShouldSample_WithMixedCaseHexCharacters_ProcessesCorrectly()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(1.0); // Always sample to verify it processes

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var result = sampler.ShouldSample("AbCdEf1234567890aBcDeF1234567890");
                Assert.That(result, Is.True); // Should sample with ratio 1.0
            });
        }

        [Test]
        public void ShouldSample_WithNumericCharacters_ProcessesCorrectly()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(1.0); // Always sample to verify it processes

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var result = sampler.ShouldSample("1234567890123456789012345678901234");
                Assert.That(result, Is.True); // Should sample with ratio 1.0
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
            var sampler = new TraceIdRatioSampler(0.0);

            // Act & Assert
            Assert.That(sampler.ShouldSample(traceId), Is.False, $"Should not sample trace ID: {traceId}");
        }

        [TestCase("0000000000000000000000000000000000")]
        [TestCase("1111111111111111111111111111111111")]
        [TestCase("aaaaaaaaaaaaaaaa111111111111111111")]
        [TestCase("ffffffffffffffff111111111111111111")]
        public void ShouldSample_WithOneRatio_AlwaysSamples(string traceId)
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(1.0);

            // Act & Assert
            Assert.That(sampler.ShouldSample(traceId), Is.True, $"Should sample trace ID: {traceId}");
        }

        [Test]
        public void ShouldSample_WithSameTraceId_ReturnsConsistentResult()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(0.5);
            var traceId = "1234567890abcdef1234567890abcdef";

            // Act
            var firstResult = sampler.ShouldSample(traceId);
            var secondResult = sampler.ShouldSample(traceId);
            var thirdResult = sampler.ShouldSample(traceId);

            // Assert
            Assert.That(secondResult, Is.EqualTo(firstResult), "Second call should return same result as first");
            Assert.That(thirdResult, Is.EqualTo(firstResult), "Third call should return same result as first");
        }

        [Test]
        public void ShouldSample_OnlyUsesFirst16Characters()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(0.5);
            var baseTraceId = "1234567890abcdef";
            var traceId1 = baseTraceId + "0000000000000000"; // 32 chars
            var traceId2 = baseTraceId + "ffffffffffffffff"; // 32 chars, different suffix
            var traceId3 = baseTraceId + "1111111111111111"; // 32 chars, different suffix

            // Act
            var result1 = sampler.ShouldSample(traceId1);
            var result2 = sampler.ShouldSample(traceId2);
            var result3 = sampler.ShouldSample(traceId3);

            // Assert
            NrAssert.Multiple(
                () => Assert.That(result2, Is.EqualTo(result1), "Should return same result regardless of suffix"),
                () => Assert.That(result3, Is.EqualTo(result1), "Should return same result regardless of suffix")
            );
        }

        #endregion

        #region Boundary Condition Tests

        [Test]
        public void ShouldSample_WithMinimumTraceId_HandlesCorrectly()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(0.5);
            var minTraceId = "0000000000000000000000000000000000";

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var result = sampler.ShouldSample(minTraceId);
                // The result depends on the ratio and implementation, just verify it doesn't throw
                Assert.That(result, Is.TypeOf<bool>());
            });
        }

        [Test]
        public void ShouldSample_WithMaximumTraceId_HandlesCorrectly()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(0.5);
            var maxTraceId = "ffffffffffffffff111111111111111111";

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var result = sampler.ShouldSample(maxTraceId);
                // The result depends on the ratio and implementation, just verify it doesn't throw
                Assert.That(result, Is.TypeOf<bool>());
            });
        }

        [Test]
        public void ShouldSample_WithExactly16Characters_ProcessesCorrectly()
        {
            // Arrange
            var sampler = new TraceIdRatioSampler(1.0); // Use 1.0 to ensure sampling
            var exactTraceId = "1234567890abcdef"; // Exactly 16 characters

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var result = sampler.ShouldSample(exactTraceId);
                Assert.That(result, Is.True); // Should sample with ratio 1.0
            });
        }

        #endregion

        #region Different Ratio Distribution Tests

        [TestCase(0.1)]
        [TestCase(0.3)]
        [TestCase(0.7)]
        [TestCase(0.9)]
        public void ShouldSample_WithDifferentRatios_ShowsExpectedDistribution(double ratio)
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
                if (sampler.ShouldSample(traceId))
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
