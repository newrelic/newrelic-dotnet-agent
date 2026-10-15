// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Helpers;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Helpers;

[TestFixture]
public class SignalRHelpersTests
{
    private class UnrelatedMetadata { }

    [Test]
    [TestCase("GET", "text/event-stream", "abc", true)]
    [TestCase("get", "TEXT/EVENT-STREAM", "abc", true)]
    [TestCase("GET", "text/html, text/event-stream;q=0.9", "abc", true)]
    [TestCase("POST", "text/event-stream", "abc", false)]
    [TestCase("DELETE", "text/event-stream", "abc", false)]
    [TestCase(null, "text/event-stream", "abc", false)]
    [TestCase("GET", "application/json", "abc", false)]
    [TestCase("GET", "", "abc", false)]
    [TestCase("GET", null, "abc", false)]
    [TestCase("GET", "text/event-stream", "", false)]
    [TestCase("GET", "text/event-stream", null, false)]
    public void IsServerSentEventsTransportRequest_MatchesSignalRDispatchShape(string method, string acceptHeader, string connectionId, bool expected)
    {
        Assert.That(SignalRHelpers.IsServerSentEventsTransportRequest(method, acceptHeader, connectionId), Is.EqualTo(expected));
    }

    [Test]
    public void IsHubTransportEndpoint_ReturnsFalse_WhenMetadataIsNull()
    {
        Assert.That(SignalRHelpers.IsHubTransportEndpoint(null), Is.False);
    }

    [Test]
    public void IsHubTransportEndpoint_ReturnsFalse_WhenMetadataIsEmpty()
    {
        Assert.That(SignalRHelpers.IsHubTransportEndpoint(new object[0]), Is.False);
    }

    [Test]
    public void IsHubTransportEndpoint_ReturnsTrue_WhenOnlyHubMetadata()
    {
        var metadata = new object[] { new UnrelatedMetadata(), new Microsoft.AspNetCore.SignalR.HubMetadata() };

        Assert.That(SignalRHelpers.IsHubTransportEndpoint(metadata), Is.True);
    }

    [Test]
    public void IsHubTransportEndpoint_ReturnsFalse_WhenHubMetadataFollowedByNegotiateMetadata()
    {
        var metadata = new object[] { new Microsoft.AspNetCore.SignalR.HubMetadata(), new Microsoft.AspNetCore.Http.Connections.NegotiateMetadata() };

        Assert.That(SignalRHelpers.IsHubTransportEndpoint(metadata), Is.False);
    }

    [Test]
    public void IsHubTransportEndpoint_ReturnsFalse_WhenNegotiateMetadataPrecedesHubMetadata()
    {
        var metadata = new object[] { new Microsoft.AspNetCore.Http.Connections.NegotiateMetadata(), new Microsoft.AspNetCore.SignalR.HubMetadata() };

        Assert.That(SignalRHelpers.IsHubTransportEndpoint(metadata), Is.False);
    }

    [Test]
    public void IsHubTransportEndpoint_ReturnsFalse_WhenOnlyNegotiateMetadata()
    {
        var metadata = new object[] { new Microsoft.AspNetCore.Http.Connections.NegotiateMetadata() };

        Assert.That(SignalRHelpers.IsHubTransportEndpoint(metadata), Is.False);
    }

    [Test]
    public void IsHubTransportEndpoint_ReturnsFalse_WhenOnlyUnrelatedMetadata()
    {
        var metadata = new object[] { new UnrelatedMetadata(), "text" };

        Assert.That(SignalRHelpers.IsHubTransportEndpoint(metadata), Is.False);
    }

    [Test]
    public void IsHubTransportEndpoint_IgnoresNullItems()
    {
        var metadata = new object[] { null, new Microsoft.AspNetCore.SignalR.HubMetadata(), null };

        Assert.That(SignalRHelpers.IsHubTransportEndpoint(metadata), Is.True);
    }

    [Test]
    public void IsHubTransportEndpoint_ReturnsFalse_WhenOnlyNullItems()
    {
        Assert.That(SignalRHelpers.IsHubTransportEndpoint(new object[] { null }), Is.False);
    }
}
