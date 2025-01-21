// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilization
{
    [TestFixture]
    public class AzureVendorModelTests
    {
        [Test]
        public void AzureVendorModel_Properties_ShouldReturnCorrectValues()
        {
            // Arrange
            var location = "East US";
            var name = "TestVM";
            var vmId = "12345";
            var vmSize = "Standard_D2s_v3";

            // Act
            var model = new AzureVendorModel(location, name, vmId, vmSize);

            // Assert
            Assert.That(model.Location, Is.EqualTo(location));
            Assert.That(model.Name, Is.EqualTo(name));
            Assert.That(model.VmId, Is.EqualTo(vmId));
            Assert.That(model.VmSize, Is.EqualTo(vmSize));
            Assert.That(model.VendorName, Is.EqualTo("azure"));
        }

        [Test]
        public void AzureVendorModel_Serialization_ShouldSerializeCorrectly()
        {
            // Arrange
            var location = "East US";
            var name = "TestVM";
            var vmId = "12345";
            var vmSize = "Standard_D2s_v3";
            var model = new AzureVendorModel(location, name, vmId, vmSize);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"location\":\"East US\",\"name\":\"TestVM\",\"vmId\":\"12345\",\"vmSize\":\"Standard_D2s_v3\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void AzureVendorModel_Serialization_ShouldIgnoreNullValues()
        {
            // Arrange
            var location = "East US";
            var name = "TestVM";
            var vmId = "12345";
            var model = new AzureVendorModel(location, name, vmId, null);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"location\":\"East US\",\"name\":\"TestVM\",\"vmId\":\"12345\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }
        [Test]
        public void AzureVendorModel_Serialization_ShouldIgnoreNullLocation()
        {
            // Arrange
            var name = "TestVM";
            var vmId = "12345";
            var vmSize = "Standard_D2s_v3";
            var model = new AzureVendorModel(null, name, vmId, vmSize);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"name\":\"TestVM\",\"vmId\":\"12345\",\"vmSize\":\"Standard_D2s_v3\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void AzureVendorModel_Serialization_ShouldIgnoreNullName()
        {
            // Arrange
            var location = "East US";
            var vmId = "12345";
            var vmSize = "Standard_D2s_v3";
            var model = new AzureVendorModel(location, null, vmId, vmSize);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"location\":\"East US\",\"vmId\":\"12345\",\"vmSize\":\"Standard_D2s_v3\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void AzureVendorModel_Serialization_ShouldIgnoreNullVmId()
        {
            // Arrange
            var location = "East US";
            var name = "TestVM";
            var vmSize = "Standard_D2s_v3";
            var model = new AzureVendorModel(location, name, null, vmSize);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"location\":\"East US\",\"name\":\"TestVM\",\"vmSize\":\"Standard_D2s_v3\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void AzureVendorModel_Serialization_ShouldIgnoreAllNullValues()
        {
            // Arrange
            var model = new AzureVendorModel(null, null, null, null);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }
    }
}
