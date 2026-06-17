// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.OpenTelemetryBridge.Tracing;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OpenTelemetryBridge;

// Covers the RPC path/URI building used by the OpenTelemetry bridge. These were previously
// untested and assumed every RPC activity was gRPC, which produced malformed URIs for other
// RPC systems such as SignalR (rpc.system="signalr").
[TestFixture]
public class ActivityBridgeSegmentHelpersTests
{
    // gRPC with a real peer address must be unchanged by the rpc-system-aware refactor.
    [Test]
    public void BuildRpcPath_Grpc_WithHostAndService_IsUnchanged()
    {
        var path = ActivityBridgeSegmentHelpers.BuildRpcPath("grpc", "10.0.0.4", 5001, "pkg.Svc", "Method", null);
        Assert.That(path, Is.EqualTo("grpc://10.0.0.4:5001/pkg.Svc/Method"));
    }

    // gRPC where only grpc.method is present (no rpc.service): grpcMethod is the full path.
    [Test]
    public void BuildRpcPath_Grpc_GrpcMethodOnly_IsUnchanged()
    {
        var path = ActivityBridgeSegmentHelpers.BuildRpcPath("grpc", "10.0.0.4", 5001, null, null, "pkg.Svc/Method");
        Assert.That(path, Is.EqualTo("grpc://10.0.0.4:5001/pkg.Svc/Method"));
    }

    // No rpc.system but a grpc.method present -> scheme falls back to grpc.
    [Test]
    public void BuildRpcPath_NullRpcSystem_WithGrpcMethod_DefaultsToGrpcScheme()
    {
        var path = ActivityBridgeSegmentHelpers.BuildRpcPath(null, "10.0.0.4", 5001, null, null, "pkg.Svc/Method");
        Assert.That(path, Does.StartWith("grpc://"));
    }

    // SignalR has no peer address tags. The path must use the signalr scheme, omit the bogus
    // "unknown:0" authority, include the method (no trailing slash), and preserve hub-name casing.
    [Test]
    public void BuildRpcPath_SignalR_NoHost_IsWellFormed()
    {
        var path = ActivityBridgeSegmentHelpers.BuildRpcPath(
            "signalr", null, null, "NewRelic.SignalRPoc.Server.ChatHub", "SendMessage", null);

        Assert.Multiple(() =>
        {
            Assert.That(path, Is.EqualTo("signalr:///NewRelic.SignalRPoc.Server.ChatHub/SendMessage"));
            Assert.That(path, Does.Not.Contain("grpc"), "scheme must reflect the RPC system, not gRPC");
            Assert.That(path, Does.Not.Contain("unknown:0"), "no fake authority when host/port are absent");
            Assert.That(path, Does.Not.EndWith("/"), "method must be present in the path");
        });
    }

    // The client side runs the built path through new Uri(...), so the SignalR form must parse.
    [Test]
    public void BuildRpcPath_SignalR_PathIsParseableUri_AndPreservesCase()
    {
        var path = ActivityBridgeSegmentHelpers.BuildRpcPath(
            "signalr", null, null, "NewRelic.SignalRPoc.Server.ChatHub", "SendMessage", null);

        Uri uri = null;
        Assert.DoesNotThrow(() => uri = new Uri(path));
        Assert.Multiple(() =>
        {
            Assert.That(uri.Scheme, Is.EqualTo("signalr"));
            Assert.That(uri.AbsolutePath, Is.EqualTo("/NewRelic.SignalRPoc.Server.ChatHub/SendMessage"),
                "hub/method casing must be preserved (lives in the path, not the lowercased authority)");
        });
    }

    // Regression for the extraction bug: rpc.method was being consumed into grpcMethod, leaving
    // method null -> a trailing-slash path. Method and service must both be extracted correctly.
    [Test]
    public void BuildRpcServerPath_SignalR_ExtractsServiceAndMethod()
    {
        var tags = new Dictionary<string, object>
        {
            ["rpc.system"] = "signalr",
            ["rpc.service"] = "NewRelic.SignalRPoc.Server.ChatHub",
            ["rpc.method"] = "SendMessage",
        };

        var path = ActivityBridgeSegmentHelpers.BuildRpcServerPath(tags, out var requestMethod);

        Assert.Multiple(() =>
        {
            Assert.That(path, Is.EqualTo("signalr:///NewRelic.SignalRPoc.Server.ChatHub/SendMessage"));
            Assert.That(requestMethod, Is.EqualTo("SendMessage"));
        });
    }

    // The library label drives the external segment name (External/{host}/{library}/{method}).
    // gRPC must stay "gRPC"; SignalR maps to a display-cased "SignalR"; unknown systems pass through;
    // a missing system preserves the historical "gRPC" default.
    [TestCase("grpc", "gRPC")]
    [TestCase("GRPC", "gRPC")]
    [TestCase("signalr", "SignalR")]
    [TestCase("SignalR", "SignalR")]
    [TestCase("dubbo", "dubbo")]
    [TestCase(null, "gRPC")]
    [TestCase("", "gRPC")]
    public void GetRpcLibraryName_MapsRpcSystemToLabel(string rpcSystem, string expected)
    {
        Assert.That(ActivityBridgeSegmentHelpers.GetRpcLibraryName(rpcSystem), Is.EqualTo(expected));
    }

    // gRPC server path with peer address still produces the expected gRPC URI and method.
    [Test]
    public void BuildRpcServerPath_Grpc_WithPeerAddress()
    {
        var tags = new Dictionary<string, object>
        {
            ["rpc.system"] = "grpc",
            ["rpc.service"] = "pkg.Svc",
            ["rpc.method"] = "Method",
            ["server.address"] = "10.0.0.4",
            ["server.port"] = 5001,
        };

        var path = ActivityBridgeSegmentHelpers.BuildRpcServerPath(tags, out var requestMethod);

        Assert.Multiple(() =>
        {
            Assert.That(path, Is.EqualTo("grpc://10.0.0.4:5001/pkg.Svc/Method"));
            Assert.That(requestMethod, Is.EqualTo("Method"));
        });
    }
}
