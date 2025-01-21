// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilization
{
    [TestFixture]
    public class PcfVendorModelTests
    {
        [Test]
        public void PcfVendorModel_Properties_ShouldReturnCorrectValues()
        {
            // Arrange
            var cfInstanceGuid = "guid-12345";
            var cfInstanceIp = "192.168.1.1";
            var memoryLimit = "1024M";

            // Act
            var model = new PcfVendorModel(cfInstanceGuid, cfInstanceIp, memoryLimit);

            // Assert
            Assert.That(model.CfInstanceGuid, Is.EqualTo(cfInstanceGuid));
            Assert.That(model.CfInstanceIp, Is.EqualTo(cfInstanceIp));
            Assert.That(model.MemoryLimit, Is.EqualTo(memoryLimit));
            Assert.That(model.VendorName, Is.EqualTo("pcf"));
        }

        [Test]
        public void PcfVendorModel_Serialization_ShouldSerializeCorrectly()
        {
            // Arrange
            var cfInstanceGuid = "guid-12345";
            var cfInstanceIp = "192.168.1.1";
            var memoryLimit = "1024M";
            var model = new PcfVendorModel(cfInstanceGuid, cfInstanceIp, memoryLimit);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"cf_instance_guid\":\"guid-12345\",\"cf_instance_ip\":\"192.168.1.1\",\"memory_limit\":\"1024M\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void PcfVendorModel_Serialization_ShouldIgnoreNullValues()
        {
            // Arrange
            var cfInstanceGuid = "guid-12345";
            var cfInstanceIp = "192.168.1.1";
            var model = new PcfVendorModel(cfInstanceGuid, cfInstanceIp, null);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"cf_instance_guid\":\"guid-12345\",\"cf_instance_ip\":\"192.168.1.1\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void PcfVendorModel_Serialization_ShouldIgnoreNullCfInstanceGuid()
        {
            // Arrange
            var cfInstanceIp = "192.168.1.1";
            var memoryLimit = "1024M";
            var model = new PcfVendorModel(null, cfInstanceIp, memoryLimit);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"cf_instance_ip\":\"192.168.1.1\",\"memory_limit\":\"1024M\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void PcfVendorModel_Serialization_ShouldIgnoreNullCfInstanceIp()
        {
            // Arrange
            var cfInstanceGuid = "guid-12345";
            var memoryLimit = "1024M";
            var model = new PcfVendorModel(cfInstanceGuid, null, memoryLimit);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"cf_instance_guid\":\"guid-12345\",\"memory_limit\":\"1024M\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void PcfVendorModel_Serialization_ShouldIgnoreAllNullValues()
        {
            // Arrange
            var model = new PcfVendorModel(null, null, null);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }
    }
}
