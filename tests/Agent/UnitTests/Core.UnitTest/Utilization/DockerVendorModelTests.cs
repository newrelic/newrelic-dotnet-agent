// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilization
{
    [TestFixture]
    public class DockerVendorModelTests
    {
        [Test]
        public void DockerVendorModel_Properties_ShouldReturnCorrectValues()
        {
            // Arrange
            var id = "docker123";

            // Act
            var model = new DockerVendorModel(id);

            // Assert
            Assert.That(model.Id, Is.EqualTo(id));
            Assert.That(model.VendorName, Is.EqualTo("docker"));
        }

        [Test]
        public void DockerVendorModel_Serialization_ShouldSerializeCorrectly()
        {
            // Arrange
            var id = "docker123";
            var model = new DockerVendorModel(id);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"id\":\"docker123\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void DockerVendorModel_Serialization_ShouldIgnoreNullValues()
        {
            // Arrange
            var model = new DockerVendorModel(null);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }
    }
}
