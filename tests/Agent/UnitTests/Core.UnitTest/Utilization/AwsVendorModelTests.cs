// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilization
{
    [TestFixture]
    public class AwsVendorModelTests
    {
        [Test]
        public void AwsVendorModel_SerializesToJsonCorrectly()
        {
            // Arrange
            var model = new AwsVendorModel("us-west-2a", "i-1234567890abcdef0", "t2.micro");

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"availabilityZone\":\"us-west-2a\",\"instanceId\":\"i-1234567890abcdef0\",\"instanceType\":\"t2.micro\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void AwsVendorModel_SerializesToJsonCorrectly_WhenInstanceIdIsNull()
        {
            // Arrange
            var model = new AwsVendorModel("us-west-2a", null, "t2.micro");

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"availabilityZone\":\"us-west-2a\",\"instanceType\":\"t2.micro\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void AwsVendorModel_SerializesToJsonCorrectly_WhenInstanceTypeIsNull()
        {
            // Arrange
            var model = new AwsVendorModel("us-west-2a", "i-1234567890abcdef0", null);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"availabilityZone\":\"us-west-2a\",\"instanceId\":\"i-1234567890abcdef0\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void AwsVendorModel_SerializesToJsonCorrectly_WhenAvailabilityZoneIsNull()
        {
            // Arrange
            var model = new AwsVendorModel(null, "i-1234567890abcdef0", "t2.micro");

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"instanceId\":\"i-1234567890abcdef0\",\"instanceType\":\"t2.micro\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void AwsVendorModel_SerializesToJsonCorrectly_WhenAllPropertiesAreNull()
        {
            // Arrange
            var model = new AwsVendorModel(null, null, null);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }
    }
}
