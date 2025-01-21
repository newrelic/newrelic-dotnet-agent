// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilization
{
    [TestFixture]
    public class KubernetesVendorModelTests
    {
        [Test]
        public void KubernetesVendorModel_Properties_ShouldReturnCorrectValues()
        {
            // Arrange
            var kubernetesServiceHost = "k8s-service-host";

            // Act
            var model = new KubernetesVendorModel(kubernetesServiceHost);

            // Assert
            Assert.That(model.KubernetesServiceHost, Is.EqualTo(kubernetesServiceHost));
            Assert.That(model.VendorName, Is.EqualTo("kubernetes"));
        }

        [Test]
        public void KubernetesVendorModel_Serialization_ShouldSerializeCorrectly()
        {
            // Arrange
            var kubernetesServiceHost = "k8s-service-host";
            var model = new KubernetesVendorModel(kubernetesServiceHost);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{\"kubernetes_service_host\":\"k8s-service-host\"}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }

        [Test]
        public void KubernetesVendorModel_Serialization_ShouldIgnoreNullValues()
        {
            // Arrange
            var model = new KubernetesVendorModel(null);

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            var expectedJson = "{}";
            Assert.That(json, Is.EqualTo(expectedJson));
        }
    }
}
