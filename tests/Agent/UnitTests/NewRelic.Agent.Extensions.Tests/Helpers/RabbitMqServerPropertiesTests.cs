// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text;
using NewRelic.Agent.Extensions.Helpers;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Helpers;

[TestFixture]
internal class RabbitMqServerPropertiesTests
{
    [Test]
    public void GetClusterName_ReturnsNull_WhenDictionaryIsNull()
    {
        Assert.That(RabbitMqServerProperties.GetClusterName(null), Is.Null);
    }

    [Test]
    public void GetClusterName_ReturnsNull_WhenKeyIsMissing()
    {
        var properties = new Dictionary<string, object> { { "product", "RabbitMQ" } };

        Assert.That(RabbitMqServerProperties.GetClusterName(properties), Is.Null);
    }

    [Test]
    public void GetClusterName_DecodesUtf8_WhenValueIsByteArray()
    {
        var properties = new Dictionary<string, object> { { "cluster_name", Encoding.UTF8.GetBytes("rabbit@node1") } };

        Assert.That(RabbitMqServerProperties.GetClusterName(properties), Is.EqualTo("rabbit@node1"));
    }

    [Test]
    public void GetClusterName_ReturnsValue_WhenValueIsString()
    {
        var properties = new Dictionary<string, object> { { "cluster_name", "rabbit@node1" } };

        Assert.That(RabbitMqServerProperties.GetClusterName(properties), Is.EqualTo("rabbit@node1"));
    }

    [Test]
    public void GetClusterName_ReturnsNull_WhenValueIsNull()
    {
        var properties = new Dictionary<string, object> { { "cluster_name", null } };

        Assert.That(RabbitMqServerProperties.GetClusterName(properties), Is.Null);
    }

    [Test]
    public void GetClusterName_ReturnsNull_WhenStringValueIsEmpty()
    {
        var properties = new Dictionary<string, object> { { "cluster_name", "" } };

        Assert.That(RabbitMqServerProperties.GetClusterName(properties), Is.Null);
    }

    [Test]
    public void GetClusterName_ReturnsNull_WhenByteArrayValueIsEmpty()
    {
        var properties = new Dictionary<string, object> { { "cluster_name", new byte[0] } };

        Assert.That(RabbitMqServerProperties.GetClusterName(properties), Is.Null);
    }

    [Test]
    public void GetClusterName_DecodesNonAsciiUtf8()
    {
        const string name = "cluster-é中";
        var properties = new Dictionary<string, object> { { "cluster_name", Encoding.UTF8.GetBytes(name) } };

        Assert.That(RabbitMqServerProperties.GetClusterName(properties), Is.EqualTo(name));
    }

    [Test]
    public void GetClusterName_ReturnsNull_WhenValueIsOtherType()
    {
        var properties = new Dictionary<string, object> { { "cluster_name", 42 } };

        Assert.That(RabbitMqServerProperties.GetClusterName(properties), Is.Null);
    }
}
