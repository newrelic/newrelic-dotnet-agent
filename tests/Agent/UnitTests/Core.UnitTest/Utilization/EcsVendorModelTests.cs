// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilization
{
    [TestFixture]
    public class EcsVendorModelTests
    {
        [Test]
        public void EcsVendorModel_Properties_ShouldReturnCorrectValues()
        {
            // Arrange
            var ecsDockerId = "ecs-12345";

            // Act
            var model = new EcsVendorModel(ecsDockerId);

            // Assert
            Assert.That(model.EcsDockerId, Is.EqualTo(ecsDockerId));
            Assert.That(model.VendorName, Is.EqualTo("ecs"));
        }

        [Test]
        public void EcsVendorModel_Serialization_ShouldSerializeCorrectly()
        {
            // Arrange
            var ecsDockerId = "ecs-12345";
            var model = new EcsVendorModel(ecsDockerId);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"ecsDockerId\":\"ecs-12345\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void EcsVendorModel_Serialization_ShouldIgnoreNullValues()
        {
            // Arrange
            var model = new EcsVendorModel(null);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }
    }
}
