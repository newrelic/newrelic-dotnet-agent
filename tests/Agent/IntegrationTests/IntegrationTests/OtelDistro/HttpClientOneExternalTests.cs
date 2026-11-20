// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using SpanEvent = NewRelic.Agent.Tests.TestSerializationHelpers.Models.SpanEvent;

namespace NewRelic.Agent.IntegrationTests.OtelDistro;

// One HttpClient GET to a local endpoint inside a transaction must report exactly one External segment, in agent
// mode and in OTel distro mode (OTel native profiler + New Relic distro plugin). The HttpClient wrapper XML splits
// the instrumented method by System.Net.Http assembly version: HttpClient.SendAsync below 5.0 (.NET Framework) and
// SocketsHttpHandler.SendAsync from 5.0 (.NET 5+). If a mode instrumented both on .NET 5+, or neither, the counts
// below would be 2 or 0. The agent-mode classes are the side-by-side control for the distro classes.
// This namespace is deliberately not in integration_all (.github/workflows/test_selection.yml): not run by CI
// until the distro is packaged. The distro classes require NR_DEV_OTEL_HOME.
public abstract class HttpClientOneExternalTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    protected const string LegacyClassName = "System.Net.Http.HttpClient";
    protected const string SocketsHandlerClassName = "System.Net.Http.SocketsHttpHandler";
    private const string MethodName = "SendAsync";
    private const string TransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Internal.HttpClientDriver/Get";

    private readonly TFixture _fixture;
    private readonly string _requestPath;

    protected abstract string ExpectedClassName { get; }
    protected abstract string UnexpectedClassName { get; }

    protected HttpClientOneExternalTestsBase(TFixture fixture, ITestOutputHelper output)
        : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.AddActions(
            setupConfiguration: () =>
            {
                new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath)
                    .ForceTransactionTraces()
                    .SetLogLevel("finest");
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2), 1);
            }
        );

        // No external network: the request goes to a responder in this test process on a loopback port. The path is
        // unique to this test class, so the assertions read only this test's request whatever else the process sent.
        _requestPath = "/" + GetType().Name;
        _fixture.AddCommand($"HttpClientDriver Get http://127.0.0.1:{LocalHttpResponder.Port}{_requestPath}");

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"External/all", CallCountAllHarvests = 1 },
            new Assertions.ExpectedMetric { metricName = @"External/allOther", CallCountAllHarvests = 1 },
            new Assertions.ExpectedMetric { metricName = @"External/127.0.0.1/all", CallCountAllHarvests = 1 },
            new Assertions.ExpectedMetric { metricName = @"External/127.0.0.1/Stream/GET", CallCountAllHarvests = 1 },
            new Assertions.ExpectedMetric { metricName = @"External/127.0.0.1/Stream/GET", metricScope = TransactionName, CallCountAllHarvests = 1 },
        };

        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var transactionSample = _fixture.AgentLog.GetTransactionSamples()
            .FirstOrDefault(sample => sample.Path == TransactionName);

        Assert.NotNull(transactionSample);

        var externalSpanEvents = _fixture.AgentLog.GetSpanEvents()
            .Where(e => e.IntrinsicAttributes.TryGetValue("category", out var value) && value.Equals("http"))
            .ToList();

        var agentWrapperErrorRegex = AgentLogBase.ErrorLogLinePrefixRegex + @"An exception occurred in a wrapper: (.*)";

        var requests = LocalHttpResponder.ReceivedRequests.Where(r => r.StartsWith("GET " + _requestPath + " ", StringComparison.Ordinal)).ToList();
        foreach (var request in requests)
        {
            _fixture.TestLogger?.WriteLine("LocalHttpResponder received:" + Environment.NewLine + request);
        }

        NrAssert.Multiple
        (
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assert.Single(externalSpanEvents),
            // The request reached the local responder, so the segment has a status code.
            () => Assert.Equal(200L, Convert.ToInt64(externalSpanEvents.Single().AgentAttributes["http.statusCode"])),
            () => Assertions.TransactionTraceSegmentsExist(new List<string> { @"External/127.0.0.1/Stream/GET" }, transactionSample),
            // The one segment comes from the method in range for this runtime's System.Net.Http version.
            () => Assertions.TransactionTraceSegmentExists(ExpectedClassName, MethodName, transactionSample),
            () => Assertions.TransactionTraceSegmentDoesNotExist(UnexpectedClassName, MethodName, transactionSample),
            () => Assert.Null(_fixture.AgentLog.TryGetLogLine(agentWrapperErrorRegex)),
            () => AssertTraceparentIsTheTransactions(requests, externalSpanEvents)
        );
    }

    // The traceparent the server received carries the transaction's trace id, the External segment's span id as parent
    // and the sampled flag, as agent mode sends it (W3C: version-traceid-parentid-flags).
    private static void AssertTraceparentIsTheTransactions(List<string> requests, List<SpanEvent> externalSpanEvents)
    {
        var request = Assert.Single(requests);
        var traceparents = request.Split(new[] { "\r\n" }, StringSplitOptions.None)
            .Where(line => line.StartsWith("traceparent:", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Substring("traceparent:".Length).Trim())
            .ToList();
        var parts = Assert.Single(traceparents).Split('-');
        var span = Assert.Single(externalSpanEvents);

        Assert.Equal(4, parts.Length);
        Assert.Equal((string)span.IntrinsicAttributes["traceId"], parts[1]);
        Assert.Equal((string)span.IntrinsicAttributes["guid"], parts[2]);
        Assert.Equal("01", parts[3]);
    }

    // Minimal HTTP/1.1 responder on a loopback port, process-wide: answers every request with 200 and closes the
    // connection. Raw sockets rather than HttpListener, so no URL ACL or admin rights are needed.
    private static class LocalHttpResponder
    {
        private static readonly Lazy<int> _port = new Lazy<int>(Start);

        public static int Port => _port.Value;

        public static ConcurrentQueue<string> ReceivedRequests { get; } = new ConcurrentQueue<string>();

        private static int Start()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var thread = new Thread(() => AcceptLoop(listener)) { IsBackground = true, Name = "LocalHttpResponder" };
            thread.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        private static void AcceptLoop(TcpListener listener)
        {
            while (true)
            {
                var client = listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(_ => Respond(client));
            }
        }

        private static void Respond(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    // Read up to the end of the request headers (a GET has no body).
                    var buffer = new byte[8192];
                    var received = new StringBuilder();
                    while (!received.ToString().Contains("\r\n\r\n"))
                    {
                        var read = stream.Read(buffer, 0, buffer.Length);
                        if (read == 0)
                            return;
                        received.Append(Encoding.ASCII.GetString(buffer, 0, read));
                    }

                    ReceivedRequests.Enqueue(received.ToString());

                    var response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK");
                    stream.Write(response, 0, response.Length);
                }
            }
            catch
            {
                // A failed exchange shows up in the assertions (no status code); nothing to do here.
            }
        }
    }
}

public class HttpClientOneExternalAgentTestsCoreOldest : HttpClientOneExternalTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    protected override string ExpectedClassName => SocketsHandlerClassName;
    protected override string UnexpectedClassName => LegacyClassName;

    public HttpClientOneExternalAgentTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class HttpClientOneExternalAgentTestsFW48 : HttpClientOneExternalTestsBase<ConsoleDynamicMethodFixtureFW48>
{
    protected override string ExpectedClassName => LegacyClassName;
    protected override string UnexpectedClassName => SocketsHandlerClassName;

    public HttpClientOneExternalAgentTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class HttpClientOneExternalDistroTestsCoreOldest : HttpClientOneExternalTestsBase<ConsoleDynamicMethodFixtureCoreOldestDistro>
{
    protected override string ExpectedClassName => SocketsHandlerClassName;
    protected override string UnexpectedClassName => LegacyClassName;

    public HttpClientOneExternalDistroTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldestDistro fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class HttpClientOneExternalDistroTestsFW48 : HttpClientOneExternalTestsBase<ConsoleDynamicMethodFixtureFW48Distro>
{
    protected override string ExpectedClassName => LegacyClassName;
    protected override string UnexpectedClassName => SocketsHandlerClassName;

    public HttpClientOneExternalDistroTestsFW48(ConsoleDynamicMethodFixtureFW48Distro fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
