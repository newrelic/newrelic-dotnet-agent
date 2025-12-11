// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using System;

namespace NewRelic.Agent.Core.Attributes.Tests
{
    [TestFixture]
    public class GenericConverterTests
    {
        [Test]
        public void ReturnsNull_ForNullInput()
        {
            var result = AttributeDefinitionBuilder.GenericConverter(null);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ConvertsTimeSpan_ToTotalSeconds()
        {
            var ts = TimeSpan.FromMilliseconds(12345);
            var result = AttributeDefinitionBuilder.GenericConverter(ts);
            Assert.That(result, Is.EqualTo(ts.TotalSeconds));
            Assert.That(result, Is.TypeOf<double>());
        }

        [Test]
        public void ConvertsDateTimeOffset_ToIso8601String()
        {
            var dto = DateTimeOffset.UtcNow;
            var result = AttributeDefinitionBuilder.GenericConverter(dto);
            Assert.That(result, Is.EqualTo(dto.ToString("o")));
            Assert.That(result, Is.TypeOf<string>());
        }

        [TestCase((sbyte)1, 1L)]
        [TestCase((byte)2, 2L)]
        [TestCase((short)3, 3L)]
        [TestCase((ushort)4, 4L)]
        [TestCase(5, 5L)]
        [TestCase((uint)6, 6L)]
        [TestCase((long)7, 7L)]
        [TestCase((ulong)8, 8L)]
        public void ConvertsIntegralTypes_ToInt64(object input, long expected)
        {
            var result = AttributeDefinitionBuilder.GenericConverter(input);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(result, Is.TypeOf<long>());
        }

        [Test]
        public void LeavesDecimal_Unchanged()
        {
            decimal val = 1.23M;
            var result = AttributeDefinitionBuilder.GenericConverter(val);
            Assert.That(result, Is.EqualTo(val));
            Assert.That(result, Is.TypeOf<decimal>());
        }

        [Test]
        public void LeavesSingle_Unchanged()
        {
            float val = 4.56F;
            var result = AttributeDefinitionBuilder.GenericConverter(val);
            Assert.That(result, Is.EqualTo(val));
            Assert.That(result, Is.TypeOf<float>());
        }

        [Test]
        public void LeavesDouble_Unchanged()
        {
            double val = 7.89D;
            var result = AttributeDefinitionBuilder.GenericConverter(val);
            Assert.That(result, Is.EqualTo(val));
            Assert.That(result, Is.TypeOf<double>());
        }

        [Test]
        public void LeavesInt64_Unchanged()
        {
            long val = 42L;
            var result = AttributeDefinitionBuilder.GenericConverter(val);
            Assert.That(result, Is.EqualTo(val));
            Assert.That(result, Is.TypeOf<long>());
        }

        [Test]
        public void LeavesBoolean_Unchanged()
        {
            bool val = true;
            var result = AttributeDefinitionBuilder.GenericConverter(val);
            Assert.That(result, Is.EqualTo(val));
            Assert.That(result, Is.TypeOf<bool>());
        }

        [Test]
        public void LeavesString_Unchanged()
        {
            string val = "test";
            var result = AttributeDefinitionBuilder.GenericConverter(val);
            Assert.That(result, Is.EqualTo(val));
            Assert.That(result, Is.TypeOf<string>());
        }

        [Test]
        public void ConvertsDateTime_ToIso8601String()
        {
            var dt = DateTime.UtcNow;
            var result = AttributeDefinitionBuilder.GenericConverter(dt);
            Assert.That(result, Is.EqualTo(dt.ToString("o")));
            Assert.That(result, Is.TypeOf<string>());
        }

        private class CustomType { public override string ToString() => "Custom"; }

        [Test]
        public void FallsBack_ToToString_ForUnknownType()
        {
            var custom = new CustomType();
            var result = AttributeDefinitionBuilder.GenericConverter(custom);
            Assert.That(result, Is.EqualTo("Custom"));
            Assert.That(result, Is.TypeOf<string>());
        }
    }
}
