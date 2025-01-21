// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilization
{
    [TestFixture]
    public class GcpVendorModelTests
    {
        [Test]
        public void GcpVendorModel_Properties_ShouldReturnCorrectValues()
        {
            // Arrange
            var id = "gcp-12345";
            var machineType = "n1-standard-1";
            var name = "TestInstance";
            var zone = "us-central1-a";

            // Act
            var model = new GcpVendorModel(id, machineType, name, zone);

            // Assert
            Assert.That(model.Id, Is.EqualTo(id));
            Assert.That(model.MachineType, Is.EqualTo(machineType));
            Assert.That(model.Name, Is.EqualTo(name));
            Assert.That(model.Zone, Is.EqualTo(zone));
            Assert.That(model.VendorName, Is.EqualTo("gcp"));
        }

        [Test]
        public void GcpVendorModel_Serialization_ShouldSerializeCorrectly()
        {
            // Arrange
            var id = "gcp-12345";
            var machineType = "n1-standard-1";
            var name = "TestInstance";
            var zone = "us-central1-a";
            var model = new GcpVendorModel(id, machineType, name, zone);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"id\":\"gcp-12345\",\"machineType\":\"n1-standard-1\",\"name\":\"TestInstance\",\"zone\":\"us-central1-a\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void GcpVendorModel_Serialization_ShouldIgnoreNullValues()
        {
            // Arrange
            var id = "gcp-12345";
            var machineType = "n1-standard-1";
            var name = "TestInstance";
            var model = new GcpVendorModel(id, machineType, name, null);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"id\":\"gcp-12345\",\"machineType\":\"n1-standard-1\",\"name\":\"TestInstance\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void GcpVendorModel_Serialization_ShouldIgnoreNullId()
        {
            // Arrange
            var machineType = "n1-standard-1";
            var name = "TestInstance";
            var zone = "us-central1-a";
            var model = new GcpVendorModel(null, machineType, name, zone);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"machineType\":\"n1-standard-1\",\"name\":\"TestInstance\",\"zone\":\"us-central1-a\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void GcpVendorModel_Serialization_ShouldIgnoreNullMachineType()
        {
            // Arrange
            var id = "gcp-12345";
            var name = "TestInstance";
            var zone = "us-central1-a";
            var model = new GcpVendorModel(id, null, name, zone);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"id\":\"gcp-12345\",\"name\":\"TestInstance\",\"zone\":\"us-central1-a\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void GcpVendorModel_Serialization_ShouldIgnoreNullName()
        {
            // Arrange
            var id = "gcp-12345";
            var machineType = "n1-standard-1";
            var zone = "us-central1-a";
            var model = new GcpVendorModel(id, machineType, null, zone);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"id\":\"gcp-12345\",\"machineType\":\"n1-standard-1\",\"zone\":\"us-central1-a\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void GcpVendorModel_Serialization_ShouldIgnoreAllNullValues()
        {
            // Arrange
            var model = new GcpVendorModel(null, null, null, null);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }
    }
}
