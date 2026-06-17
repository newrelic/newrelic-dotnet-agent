// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// The unary Infinite Tracing types are Grpc.Net.Client-only and are excluded from the .NET
// Framework (net462) build of Core, so they are unavailable on the net481 leg of this test
// project. These tests therefore compile and run only on the modern (net10.0) target.
#if !NETFRAMEWORK

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Collections;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Spans.Tests;

/// <summary>
/// Hand-written mock of the unary gRPC wrapper with tunable delegate hooks, mirroring the
/// streaming MockGrpcWrapper. The TrySendData hook returns a (success, response) tuple so a test
/// can control both whether the send "succeeded" and the RecordStatus that flows back inline.
/// </summary>
public class MockGrpcUnaryWrapper<TRequest, TResponse> : IGrpcUnaryWrapper<TRequest, TResponse>
    where TResponse : class
{
    public static void ThrowGrpcWrapperException(StatusCode statusCode, string message)
    {
        throw new GrpcWrapperException(statusCode, message, new Exception("Test Exception"));
    }

    public static void ThrowGrpcWrapperException(string message)
    {
        throw new GrpcWrapperException(message, new Exception("Test Exception"));
    }

    public Func<bool> WithIsConnectedImpl { get; set; } = () => true;

    public Func<string, int, bool, CancellationToken, bool> WithCreateChannelImpl { get; set; } = (host, port, ssl, cancellationToken) => true;

    public Action WithShutdownImpl { get; set; } = () => { };

    public Func<TRequest, Metadata, int, CancellationToken, Tuple<bool, TResponse>> WithTrySendDataImpl { get; set; } =
        (request, headers, timeoutMs, cancellationToken) => Tuple.Create(true, (TResponse)null);

    public bool IsConnected => WithIsConnectedImpl?.Invoke() ?? false;

    public bool CreateChannel(string host, int port, bool ssl, int connectTimeoutMs, CancellationToken cancellationToken)
    {
        return WithCreateChannelImpl?.Invoke(host, port, ssl, cancellationToken) ?? false;
    }

    public bool TrySendData(TRequest item, Metadata headers, int sendTimeoutMs, CancellationToken cancellationToken, out TResponse response)
    {
        var result = WithTrySendDataImpl(item, headers, sendTimeoutMs, cancellationToken);
        response = result.Item2;
        return result.Item1;
    }

    public void Shutdown()
    {
        WithShutdownImpl?.Invoke();
    }
}

[TestFixture]
[NonParallelizable]
internal class SpanUnaryServiceTests
{
    private const string _validHost = "infiniteTracing.net";
    private const string _validPort = "443";
    private const string DefaultLicenseKey = "defaultlicensekey";
    private const string DefaultAgentRunToken = "defaultagentruntoken";

    private const int _expectedDelayAfterError = 15000;
    private readonly int[] _expectedDelaySequenceConnect = new[] { 15000, 15000, 30000, 60000, 120000, 300000, 300000, 300000, 300000, 300000, 300000, 300000, 300000, 300000 };

    private float? TestFlakyValue;
    private int? TestDelayValue;
    private Dictionary<string, string> TestRequestHeadersMap;
    private bool TestCompressionSetting = true;

    private MockGrpcUnaryWrapper<SpanBatch, RecordStatus> _grpcWrapper;
    private IDelayer _delayer;
    private IConfigurationService _configSvc;
    private IConfiguration _currentConfiguration => _configSvc?.Configuration;
    private SpanUnaryService _service;
    private IAgentHealthReporter _agentHealthReporter;
    private IAgentTimerService _agentTimerService;
    private IEnvironment _environment;

    private IConfiguration GetDefaultConfiguration()
    {
        var config = Mock.Create<IConfiguration>();
        Mock.Arrange(() => config.SpanEventsEnabled).Returns(true);
        Mock.Arrange(() => config.CollectorSendDataOnExit).Returns(true);
        Mock.Arrange(() => config.CollectorSendDataOnExitThreshold).Returns(0);
        Mock.Arrange(() => config.InfiniteTracingTraceCountConsumers).Returns(1);
        Mock.Arrange(() => config.InfiniteTracingTraceObserverHost).Returns(_validHost);
        Mock.Arrange(() => config.InfiniteTracingTraceObserverPort).Returns(_validPort);
        Mock.Arrange(() => config.InfiniteTracingTraceTimeoutMsConnect).Returns(10000);
        Mock.Arrange(() => config.InfiniteTracingTraceTimeoutMsSendData).Returns(2000);
        Mock.Arrange(() => config.AgentLicenseKey).Returns(DefaultLicenseKey);
        Mock.Arrange(() => config.AgentRunId).Returns(DefaultAgentRunToken);
        Mock.Arrange(() => config.InfiniteTracingTraceObserverTestFlaky).Returns(() => TestFlakyValue);
        Mock.Arrange(() => config.InfiniteTracingTraceObserverTestDelayMs).Returns(() => TestDelayValue);
        Mock.Arrange(() => config.RequestHeadersMap).Returns(() => TestRequestHeadersMap);
        Mock.Arrange(() => config.InfiniteTracingBatchSizeSpans).Returns(1);
        Mock.Arrange(() => config.InfiniteTracingPartitionCountSpans).Returns(62);
        Mock.Arrange(() => config.InfiniteTracingCompression).Returns(() => TestCompressionSetting);

        return config;
    }

    private SpanUnaryService GetService()
    {
        return new SpanUnaryService(_grpcWrapper, _delayer, _configSvc, _agentHealthReporter, _agentTimerService, _environment);
    }

    private static RecordStatus GetResponseModel(ulong messagesSeen)
    {
        return new RecordStatus { MessagesSeen = messagesSeen };
    }

    [SetUp]
    public void Setup()
    {
        TestFlakyValue = null;
        TestDelayValue = null;
        TestRequestHeadersMap = null;
        TestCompressionSetting = true;

        _grpcWrapper = new MockGrpcUnaryWrapper<SpanBatch, RecordStatus>();
        // A successful unary call always returns a RecordStatus; default to an empty one so the
        // service's inline HandleServerResponse doesn't dereference null.
        _grpcWrapper.WithTrySendDataImpl = (batch, headers, timeout, token) => Tuple.Create(true, new RecordStatus());

        _delayer = Mock.Create<IDelayer>();
        _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
        _agentTimerService = Mock.Create<IAgentTimerService>();
        _configSvc = Mock.Create<IConfigurationService>();
        _environment = Mock.Create<IEnvironment>();

        Mock.Arrange(() => _configSvc.Configuration).Returns(GetDefaultConfiguration());
    }

    [TearDown]
    public void Teardown()
    {
        _service?.Shutdown(false);
        _service?.Dispose();
        _agentHealthReporter = null;
    }

    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, true)]
    public void IsServiceAvailableTests(bool isServiceEnabled, bool isChannelConnected, bool expectedIsServiceAvailable)
    {
        Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceObserverHost).Returns(isServiceEnabled ? _validHost : null as string);

        _service = GetService();

        var signalIsDone = new ManualResetEventSlim();
        var countSends = 0;

        _grpcWrapper.WithIsConnectedImpl = () => isChannelConnected;
        _grpcWrapper.WithTrySendDataImpl = (batch, headers, timeout, token) =>
        {
            countSends++;
            if (countSends > 1)
            {
                signalIsDone.Set();
            }

            return Tuple.Create(true, new RecordStatus());
        };

        if (!isServiceEnabled || !isChannelConnected)
        {
            signalIsDone.Set();
        }

        var collection = new PartitionedBlockingCollection<Span>(10, 3);
        collection.TryAdd(new Span());
        collection.TryAdd(new Span());
        collection.TryAdd(new Span());

        _service.StartConsumingCollection(collection);

        Assert.Multiple(() =>
        {
            Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(_service.IsServiceAvailable, Is.EqualTo(expectedIsServiceAvailable), $"If IsServiceEnabled={isServiceEnabled} and IsGrpcChannelConnected={isChannelConnected}, IsServiceAvailable should be {expectedIsServiceAvailable}");
        });
    }

    [TestCase(false, false, false, false)]
    [TestCase(true, false, false, false)]
    [TestCase(false, true, false, false)]
    [TestCase(false, false, true, false)]
    [TestCase(false, false, false, true)]
    [TestCase(true, true, true, true)]
    public void ShouldSendCorrectMetadataWithEachUnaryCall(bool includeFlaky, bool includeDelay, bool includeRequestHeadersMap, bool enableCompression)
    {
        var expectedMetadata = new Dictionary<string, string>
        {
            { "license_key", DefaultLicenseKey },
            { "agent_run_token", DefaultAgentRunToken }
        };

        if (includeFlaky)
        {
            TestFlakyValue = 100.0f;
            expectedMetadata["flaky"] = TestFlakyValue.ToString();
        }

        if (includeDelay)
        {
            TestDelayValue = 50000;
            expectedMetadata["delay"] = "50000";
        }

        if (includeRequestHeadersMap)
        {
            TestRequestHeadersMap = new Dictionary<string, string>
            {
                { "rhm_key1", "rhm_value1" },
                { "RHM_Key2", "Rhm_Value2" }
            };
            expectedMetadata["rhm_key1"] = "rhm_value1";
            expectedMetadata["rhm_key2"] = "Rhm_Value2"; //The key is expected to be lower-cased, but the value should be unmodified
        }

        TestCompressionSetting = enableCompression;
        if (enableCompression)
        {
            expectedMetadata["grpc-internal-encoding-request"] = "gzip";
        }

        _service = GetService();

        // Unlike streaming (which sends metadata once at stream creation), unary attaches the
        // metadata to every call - so capture it from TrySendData.
        var actualConnectionMetadata = new Dictionary<string, string>();
        var signalIsDone = new ManualResetEventSlim();
        _grpcWrapper.WithTrySendDataImpl = (batch, headers, timeout, token) =>
        {
            actualConnectionMetadata = headers.ToDictionary(k => k.Key, v => v.Value);
            signalIsDone.Set();
            return Tuple.Create(true, new RecordStatus());
        };

        var sourceCollection = new PartitionedBlockingCollection<Span>(10, 3);
        sourceCollection.TryAdd(new Span());

        _service.StartConsumingCollection(sourceCollection);

        NrAssert.Multiple
        (
            () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
            () => Assert.That(actualConnectionMetadata, Is.EquivalentTo(expectedMetadata), "connection metadata did not match")
        );
    }

    [TestCase(null, null, false)]
    [TestCase(null, "", false)]
    [TestCase(null, "something", false)]
    [TestCase("", null, false)]
    [TestCase("", "", false)]
    [TestCase("", "something", false)]
    [TestCase("something", null, true)]
    [TestCase("something", "", true)]
    [TestCase("something", "something", false)]
    public void ShouldLogProxyWarningIfOnlyTheLegacyProxyIsDetected(string grpcProxyValue, string httpsProxyValue, bool expectWarningInLogs)
    {
        Mock.Arrange(() => _environment.GetEnvironmentVariable("grpc_proxy")).Returns(grpcProxyValue);
        Mock.Arrange(() => _environment.GetEnvironmentVariable("https_proxy")).Returns(httpsProxyValue);

        using (var logger = new TestUtilities.Logging())
        {
            _service = GetService();

            _grpcWrapper.WithCreateChannelImpl = (host, port, ssl, token) =>
            {
                //Throw the unimplemented exception so that the service will shutdown
                MockGrpcUnaryWrapper<SpanBatch, RecordStatus>.ThrowGrpcWrapperException(StatusCode.Unimplemented, "Test gRPC Exception");
                return false;
            };

            var countShutdowns = 0;
            var signalIsDone = new ManualResetEventSlim();
            // When starting the channel, a shutdown is initiated to reset everything
            // Ignore this in our determination of svc.stop
            _grpcWrapper.WithShutdownImpl = () =>
            {
                countShutdowns++;

                if (countShutdowns > 1)
                {
                    signalIsDone.Set();
                }
            };

            var sourceCollection = new PartitionedBlockingCollection<Span>(10, 3);

            _service.StartConsumingCollection(sourceCollection);

            NrAssert.Multiple
            (
                () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
                () => Assert.That(logger.HasMessageThatContains("'grpc_proxy'"), Is.EqualTo(expectWarningInLogs))
            );
        }
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    [TestCase(3, true)]
    [TestCase(4, false)]
    [TestCase(5, true)]
    [TestCase(6, false)]
    [TestCase(7, true)]
    [TestCase(8, true)]
    public void BackoffRetry_Connect_DelaySequence(int succeedOnAttempt, bool throwExceptionDuringConnect)
    {
        _service = GetService();

        var expectedDelays = _expectedDelaySequenceConnect.Take(succeedOnAttempt).ToList();
        var actualDelays = new List<int>();

        Mock.Arrange(() => _delayer.Delay(Arg.IsAny<int>(), Arg.IsAny<CancellationToken>()))
            .DoInstead<int, CancellationToken>((delay, token) =>
            {
                actualDelays.Add(delay);
            });

        var signalIsDone = new ManualResetEventSlim();
        int attemptsMade = 0;
        _grpcWrapper.WithCreateChannelImpl = (host, port, ssl, token) =>
        {
            var attempt = attemptsMade++;
            if (attempt == succeedOnAttempt)
            {
                signalIsDone.Set();
                return true;
            }

            if (throwExceptionDuringConnect)
            {
                throw new Exception("Test Exception");
            }

            return false;
        };

        var sourceCollection = new PartitionedBlockingCollection<Span>(1000, 3);

        _service.StartConsumingCollection(sourceCollection);

        NrAssert.Multiple
        (
            () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal Didn't Fire"),
            () => Assert.That(_expectedDelaySequenceConnect.Count(), Is.GreaterThanOrEqualTo(succeedOnAttempt)),
            () => Assert.That(actualDelays, Is.EqualTo(expectedDelays).AsCollection, $"After {succeedOnAttempt} attempt(s), delays should have been {string.Join(",", expectedDelays)}, but were {string.Join(",", actualDelays)}")
        );
    }

    [Test]
    public void FailedItemsReturnedToQueue()
    {
        _service = GetService();

        var actualAttempts = new List<Span>();
        var haveProcessedFailure = false;

        var item1 = new Span();
        var item2 = new Span();
        var item3 = new Span();
        var item4 = new Span();
        var item5 = new Span();

        _grpcWrapper.WithTrySendDataImpl = (batch, headers, timeout, token) =>
        {
            actualAttempts.AddRange(batch.Spans);

            if (batch.Spans.Contains(item3) && !haveProcessedFailure)
            {
                haveProcessedFailure = true;
                return Tuple.Create(false, (RecordStatus)null);
            }

            return Tuple.Create(true, new RecordStatus());
        };

        var sourceCollection = new PartitionedBlockingCollection<Span>(100, 5);

        sourceCollection.TryAdd(item1);
        sourceCollection.TryAdd(item2);
        sourceCollection.TryAdd(item3);
        sourceCollection.TryAdd(item4);
        sourceCollection.TryAdd(item5);

        var waitForConsumptionTask = Task.Run(() =>
        {
            while (sourceCollection.Count > 0)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(250));
            }
        });

        _service.StartConsumingCollection(sourceCollection);

        var expectedAttempts = new[] { item1, item2, item3, item4, item5, item3 };

        NrAssert.Multiple
        (
            () => Assert.That(waitForConsumptionTask.Wait(TimeSpan.FromSeconds(10)), Is.True, "Task didn't complete"),
            () => Assert.That(actualAttempts, Is.EqualTo(expectedAttempts).AsCollection)
        );
    }

    [Test]
    [NonParallelizable]
    public void DelayCallingSendAfterAnError()
    {
        _service = GetService();

        var actualDelays = new List<int>();

        Mock.Arrange(() => _delayer.Delay(Arg.IsAny<int>(), Arg.IsAny<CancellationToken>()))
            .DoInstead<int, CancellationToken>((delay, token) =>
            {
                actualDelays.Add(delay);
            });

        var actualAttempts = new List<Span>();
        var haveProcessedFailure = false;

        var item1 = new Span();

        _grpcWrapper.WithTrySendDataImpl = (batch, headers, timeout, token) =>
        {
            actualAttempts.AddRange(batch.Spans);

            if (batch.Spans.Contains(item1) && !haveProcessedFailure)
            {
                haveProcessedFailure = true;
                return Tuple.Create(false, (RecordStatus)null);
            }

            return Tuple.Create(true, new RecordStatus());
        };

        var sourceCollection = new PartitionedBlockingCollection<Span>(1000, 3);

        sourceCollection.TryAdd(item1);

        var waitForConsumptionTask = Task.Run(() =>
        {
            while (sourceCollection.Count > 0)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(250));
            }
            Thread.Sleep(TimeSpan.FromMilliseconds(250));
        });

        _service.StartConsumingCollection(sourceCollection);

        var expectedAttempts = new List<Span>() { item1, item1 };

        Assert.Multiple(() =>
        {
            Assert.That(waitForConsumptionTask.Wait(TimeSpan.FromSeconds(10)), Is.True, "Task didn't complete");
            Assert.That(actualAttempts, Is.EqualTo(expectedAttempts).AsCollection);
            Assert.That(actualDelays, Is.EqualTo(new[] { _expectedDelayAfterError }).AsCollection);
        });
    }

    [Test]
    public void MultipleConsumersItemsSentOnlyOnce()
    {
        Mock.Arrange(() => _currentConfiguration.InfiniteTracingBatchSizeSpans).Returns(4);
        Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceCountConsumers).Returns(3);
        _service = GetService();

        var actualItems = new ConcurrentBag<Span>();
        var requestItems = new ConcurrentBag<Span>();
        for (var i = 0; i < 100; i++)
        {
            requestItems.Add(new Span());
        }

        var expectedItems = requestItems.ToList();

        _grpcWrapper.WithTrySendDataImpl = (batch, headers, timeout, token) =>
        {
            foreach (var span in batch.Spans)
            {
                actualItems.Add(span);
            }

            return Tuple.Create(true, new RecordStatus());
        };

        var sourceCollection = new PartitionedBlockingCollection<Span>(requestItems.Count + 100, 3, requestItems);

        var waitForConsumptionTask = Task.Run(() =>
        {
            while (sourceCollection.Count > 0)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(250));
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(250));
        });

        _service.StartConsumingCollection(sourceCollection);

        NrAssert.Multiple
        (
            () => Assert.That(waitForConsumptionTask.Wait(TimeSpan.FromSeconds(60)), Is.True, "Task didn't complete"),
            () => Assert.That(actualItems.ToList(), Is.EquivalentTo(expectedItems))
        );
    }

    [TestCase(3, 10, new int[] { 3, 3, 3, 1 })]
    [TestCase(100, 10, new int[] { 10 })]
    public void BatchSizeConfigIsHonored(int configBatchSize, int expectedCountItems, int[] expectedBatchSizes)
    {
        var signalIsDone = new ManualResetEventSlim();

        Mock.Arrange(() => _currentConfiguration.InfiniteTracingBatchSizeSpans).Returns(configBatchSize);

        var actualBatchSizes = new List<int>();
        _grpcWrapper.WithTrySendDataImpl = (batch, headers, timeout, token) =>
        {
            actualBatchSizes.Add(batch.Spans.Count);

            if (actualBatchSizes.Sum(x => x) >= expectedCountItems)
            {
                signalIsDone.Set();
            }

            return Tuple.Create(true, new RecordStatus());
        };

        var queue = new PartitionedBlockingCollection<Span>(1000, 2);
        for (var i = 0; i < expectedCountItems; i++)
        {
            queue.TryAdd(new Span());
        }

        var queueCount = queue.Count;

        _service = GetService();
        _service.StartConsumingCollection(queue);

        NrAssert.Multiple
        (
            () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
            () => Assert.That(queueCount, Is.EqualTo(expectedCountItems), "Collection count"),
            () => Assert.That(actualBatchSizes, Has.Count.EqualTo(expectedBatchSizes.Length), "Number of Batches"),
            () => Assert.That(actualBatchSizes.ToArray(), Is.EqualTo(expectedBatchSizes).AsCollection, "Batch Sizes")
        );
    }

    [Test]
    public void SupportabilityMetrics_ResponseReceived()
    {
        using (var gotResponseReceivedEvent = new AutoResetEvent(false))
        {
            long responseMessagesSeen = 0;

            Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceCountConsumers).Returns(1);

            // In the unary model the RecordStatus is the return value of the call (not a separate
            // response stream), so it is handled inline after a successful send.
            _grpcWrapper.WithTrySendDataImpl = (batch, headers, timeout, token) => Tuple.Create(true, GetResponseModel(5));

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanEventsReceived(Arg.IsAny<ulong>()))
                .DoInstead<ulong>(count =>
                {
                    responseMessagesSeen += (long)count;
                    gotResponseReceivedEvent.Set();
                });

            _service = GetService();

            var collection = new PartitionedBlockingCollection<Span>(1000, 3);
            collection.TryAdd(new Span());

            _service.StartConsumingCollection(collection);

            Assert.Multiple(() =>
            {
                Assert.That(gotResponseReceivedEvent.WaitOne(TimeSpan.FromSeconds(10)), Is.True, "Trigger Didn't Fire");
                Assert.That(responseMessagesSeen, Is.EqualTo(5));
            });
        }
    }

    [Test]
    public void GrpcUnimplementedDuringSendShutsDownService()
    {
        var actualCountGrpcErrors = 0;
        var actualCountGeneralErrors = 0;
        var actualCountSpansSent = 0L;

        var expectedCountGrpcErrors = 1;
        var expectedCountGeneralErrors = 1;
        var expectedCountSpansSent = 2;

        var countShutdowns = 0;
        var signalIsDone = new ManualResetEventSlim();

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
            .DoInstead<string>(sc => actualCountGrpcErrors++);

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
            .DoInstead(() => actualCountGeneralErrors++);

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanEventsSent(Arg.IsAny<long>()))
            .DoInstead<long>(cnt => actualCountSpansSent += cnt);

        var invocationId = 0;
        _grpcWrapper.WithTrySendDataImpl = (batch, headers, timeout, token) =>
        {
            var localInvocationId = Interlocked.Increment(ref invocationId);

            if (localInvocationId == 3)
            {
                MockGrpcUnaryWrapper<SpanBatch, RecordStatus>.ThrowGrpcWrapperException(StatusCode.Unimplemented, "Test gRPC Exception");
            }

            return Tuple.Create(true, new RecordStatus());
        };

        // When starting the channel, a shutdown is initiated to reset everything
        // Ignore this in our determination of svc.stop
        _grpcWrapper.WithShutdownImpl = () =>
        {
            countShutdowns++;

            if (countShutdowns > 1)
            {
                signalIsDone.Set();
            }
        };

        var queue = new PartitionedBlockingCollection<Span>(1000, 3);
        queue.TryAdd(new Span());
        queue.TryAdd(new Span());
        queue.TryAdd(new Span());
        queue.TryAdd(new Span());
        queue.TryAdd(new Span());

        _service = GetService();
        _service.StartConsumingCollection(queue);

        NrAssert.Multiple
        (
            () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
            () => Assert.That(actualCountGrpcErrors, Is.EqualTo(expectedCountGrpcErrors), "gRPC Error Count"),
            () => Assert.That(actualCountGeneralErrors, Is.EqualTo(expectedCountGeneralErrors), "General Error Count"),
            () => Assert.That(actualCountSpansSent, Is.EqualTo(expectedCountSpansSent), "Span Sent Events")
        );
    }

    [Test]
    public void GrpcUnimplementedDuringConnectShutsDownService()
    {
        _service = GetService();

        var actualCountGrpcErrors = 0;
        var actualCountGeneralErrors = 0;

        var expectedCountGrpcErrors = 1;
        var expectedCountGeneralErrors = 1;

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
            .DoInstead<string>(sc => actualCountGrpcErrors++);

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
            .DoInstead(() => actualCountGeneralErrors++);

        _grpcWrapper.WithCreateChannelImpl = (host, port, ssl, token) =>
        {
            MockGrpcUnaryWrapper<SpanBatch, RecordStatus>.ThrowGrpcWrapperException(StatusCode.Unimplemented, "Test gRPC Exception");
            return false;
        };

        var countShutdowns = 0;
        var signalIsDone = new ManualResetEventSlim();
        _grpcWrapper.WithShutdownImpl = () =>
        {
            countShutdowns++;

            if (countShutdowns > 1)
            {
                signalIsDone.Set();
            }
        };

        var sourceCollection = new PartitionedBlockingCollection<Span>(1000, 3);

        _service.StartConsumingCollection(sourceCollection);

        Assert.Multiple(() =>
        {
            Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire");
            Assert.That(actualCountGrpcErrors, Is.EqualTo(expectedCountGrpcErrors), "gRPC Error Count");
            Assert.That(actualCountGeneralErrors, Is.EqualTo(expectedCountGeneralErrors), "General Error Count");
        });
    }

    [TestCase(StatusCode.Unavailable)]
    [TestCase(StatusCode.FailedPrecondition)]
    public void GrpcUnavailableOrFailedPreconditionDuringSendRestartsService(StatusCode statusCode)
    {
        var actualCountGrpcErrors = 0;
        var actualCountGeneralErrors = 0;
        var actualCountSpansSent = 0L;

        var expectedCountGrpcErrors = 1;
        var expectedCountGeneralErrors = 1;
        var expectedCountSpansSent = 1;
        var expectedCreateChannelCount = 2;

        var signalIsDone = new ManualResetEventSlim();
        var actualDelays = new List<int>();

        Mock.Arrange(() => _delayer.Delay(Arg.IsAny<int>(), Arg.IsAny<CancellationToken>()))
            .DoInstead<int, CancellationToken>((delay, token) => actualDelays.Add(delay));

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
            .DoInstead<string>(sc => actualCountGrpcErrors++);

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
            .DoInstead(() => actualCountGeneralErrors++);

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanEventsSent(Arg.IsAny<long>()))
            .DoInstead<long>(cnt =>
            {
                actualCountSpansSent += cnt;
                signalIsDone.Set();
            });

        var sendInvocationId = 0;
        _grpcWrapper.WithTrySendDataImpl = (batch, headers, timeout, token) =>
        {
            var localInvocationId = Interlocked.Increment(ref sendInvocationId);

            if (localInvocationId == 1)
            {
                MockGrpcUnaryWrapper<SpanBatch, RecordStatus>.ThrowGrpcWrapperException(statusCode, "Test gRPC Exception");
            }

            return Tuple.Create(true, new RecordStatus());
        };

        var createChannelInvocationCount = 0;
        _grpcWrapper.WithCreateChannelImpl = (host, port, ssl, token) =>
        {
            Interlocked.Increment(ref createChannelInvocationCount);
            return true;
        };

        var queue = new PartitionedBlockingCollection<Span>(1000, 3);
        queue.TryAdd(new Span());

        _service = GetService();
        _service.StartConsumingCollection(queue);

        NrAssert.Multiple
        (
            () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
            () => Assert.That(actualCountGrpcErrors, Is.EqualTo(expectedCountGrpcErrors), "gRPC Error Count"),
            () => Assert.That(actualCountGeneralErrors, Is.EqualTo(expectedCountGeneralErrors), "General Error Count"),
            () => Assert.That(actualCountSpansSent, Is.EqualTo(expectedCountSpansSent), "Span Sent Events"),
            () => Assert.That(actualDelays, Is.Empty, "The service should restart without triggering a delay"),
            () => Assert.That(createChannelInvocationCount, Is.EqualTo(expectedCreateChannelCount), "CreateChannel call count")
        );
    }

    [Test]
    public void DeadlineExceededDuringSendIsTreatedAsTimeout()
    {
        var actualCountTimeouts = 0;
        var actualCountGrpcErrors = 0;
        var actualCountGeneralErrors = 0;
        var actualCountSpansSent = 0L;

        var actualDelays = new List<int>();
        var signalIsDone = new ManualResetEventSlim();

        Mock.Arrange(() => _delayer.Delay(Arg.IsAny<int>(), Arg.IsAny<CancellationToken>()))
            .DoInstead<int, CancellationToken>((delay, token) => actualDelays.Add(delay));

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcTimeout())
            .DoInstead(() => actualCountTimeouts++);

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
            .DoInstead<string>(sc => actualCountGrpcErrors++);

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
            .DoInstead(() => actualCountGeneralErrors++);

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanEventsSent(Arg.IsAny<long>()))
            .DoInstead<long>(cnt =>
            {
                actualCountSpansSent += cnt;
                signalIsDone.Set();
            });

        var invocationId = 0;
        _grpcWrapper.WithTrySendDataImpl = (batch, headers, timeout, token) =>
        {
            if (Interlocked.Increment(ref invocationId) == 1)
            {
                MockGrpcUnaryWrapper<SpanBatch, RecordStatus>.ThrowGrpcWrapperException(StatusCode.DeadlineExceeded, "Test gRPC Exception");
            }

            return Tuple.Create(true, new RecordStatus());
        };

        var queue = new PartitionedBlockingCollection<Span>(1000, 3);
        queue.TryAdd(new Span());

        _service = GetService();
        _service.StartConsumingCollection(queue);

        NrAssert.Multiple
        (
            () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
            () => Assert.That(actualCountTimeouts, Is.EqualTo(1), "Timeout Count"),
            () => Assert.That(actualCountGrpcErrors, Is.EqualTo(1), "gRPC Error Count"),
            () => Assert.That(actualCountGeneralErrors, Is.EqualTo(1), "General Error Count"),
            () => Assert.That(actualCountSpansSent, Is.EqualTo(1), "Span Sent Events"),
            () => Assert.That(actualDelays, Is.EqualTo(new[] { _expectedDelayAfterError }).AsCollection, "A deadline-exceeded send should retry after the standard delay")
        );
    }

    [Test]
    public void GrpcInternalDuringSendRetriesAfterDelay()
    {
        var actualCountTimeouts = 0;
        var actualCountGrpcErrors = 0;
        var actualCountGeneralErrors = 0;
        var actualCountSpansSent = 0L;

        var actualDelays = new List<int>();
        var signalIsDone = new ManualResetEventSlim();

        Mock.Arrange(() => _delayer.Delay(Arg.IsAny<int>(), Arg.IsAny<CancellationToken>()))
            .DoInstead<int, CancellationToken>((delay, token) => actualDelays.Add(delay));

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcTimeout())
            .DoInstead(() => actualCountTimeouts++);

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
            .DoInstead<string>(sc => actualCountGrpcErrors++);

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
            .DoInstead(() => actualCountGeneralErrors++);

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanEventsSent(Arg.IsAny<long>()))
            .DoInstead<long>(cnt =>
            {
                actualCountSpansSent += cnt;
                signalIsDone.Set();
            });

        var invocationId = 0;
        _grpcWrapper.WithTrySendDataImpl = (batch, headers, timeout, token) =>
        {
            if (Interlocked.Increment(ref invocationId) == 1)
            {
                MockGrpcUnaryWrapper<SpanBatch, RecordStatus>.ThrowGrpcWrapperException(StatusCode.Internal, "Test gRPC Exception");
            }

            return Tuple.Create(true, new RecordStatus());
        };

        var queue = new PartitionedBlockingCollection<Span>(1000, 3);
        queue.TryAdd(new Span());

        _service = GetService();
        _service.StartConsumingCollection(queue);

        NrAssert.Multiple
        (
            () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
            () => Assert.That(actualCountTimeouts, Is.EqualTo(0), "Internal errors are not timeouts"),
            () => Assert.That(actualCountGrpcErrors, Is.EqualTo(1), "gRPC Error Count"),
            () => Assert.That(actualCountGeneralErrors, Is.EqualTo(1), "General Error Count"),
            () => Assert.That(actualCountSpansSent, Is.EqualTo(1), "Span Sent Events"),
            () => Assert.That(actualDelays, Is.EqualTo(new[] { _expectedDelayAfterError }).AsCollection, "An internal-error send should retry after the standard delay")
        );
    }

    [Test]
    public void SendReturningFalseReEnqueuesWithoutRecordingError()
    {
        var actualCountTimeouts = 0;
        var actualCountGrpcErrors = 0;
        var actualCountGeneralErrors = 0;
        var actualCountSpansSent = 0L;

        var signalIsDone = new ManualResetEventSlim();

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcTimeout())
            .DoInstead(() => actualCountTimeouts++);

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
            .DoInstead<string>(sc => actualCountGrpcErrors++);

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
            .DoInstead(() => actualCountGeneralErrors++);

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanEventsSent(Arg.IsAny<long>()))
            .DoInstead<long>(cnt =>
            {
                actualCountSpansSent += cnt;
                signalIsDone.Set();
            });

        // A 'false' (without an exception) means the batch could not be sent at all - cancellation
        // or an unavailable channel during shutdown/restart. It must re-queue without recording a
        // gRPC error or a timeout.
        var invocationId = 0;
        _grpcWrapper.WithTrySendDataImpl = (batch, headers, timeout, token) =>
        {
            if (Interlocked.Increment(ref invocationId) == 1)
            {
                return Tuple.Create(false, (RecordStatus)null);
            }

            return Tuple.Create(true, new RecordStatus());
        };

        var queue = new PartitionedBlockingCollection<Span>(1000, 3);
        queue.TryAdd(new Span());

        _service = GetService();
        _service.StartConsumingCollection(queue);

        NrAssert.Multiple
        (
            () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
            () => Assert.That(actualCountSpansSent, Is.EqualTo(1), "The re-queued span should eventually be sent"),
            () => Assert.That(actualCountTimeouts, Is.EqualTo(0), "A benign not-sent must not record a timeout"),
            () => Assert.That(actualCountGrpcErrors, Is.EqualTo(0), "A benign not-sent must not record a gRPC error"),
            () => Assert.That(actualCountGeneralErrors, Is.EqualTo(0), "A benign not-sent must not record a general error")
        );
    }

    [Test]
    public void WaitLogsFinishedWhenNothingIsPending()
    {
        _service = GetService();

        using (var logger = new TestUtilities.Logging())
        {
            _service.Wait(5000);

            Assert.That(logger.HasMessageThatContains("Finished sending span data on exit"), Is.True);
        }
    }

    [Test]
    [NonParallelizable]
    public void WaitLogsCouldNotFinishWhenItemsRemainPending()
    {
        var channelCreated = new ManualResetEventSlim();

        // IsConnected == false makes StartConsumers return before spawning any worker, so nothing
        // drains the queue. CreateChannel still succeeds, and the collection is assigned at the top
        // of Restart (before CreateChannel runs), so once this hook fires the queue is non-empty
        // with no consumer.
        _grpcWrapper.WithIsConnectedImpl = () => false;
        _grpcWrapper.WithCreateChannelImpl = (host, port, ssl, token) =>
        {
            channelCreated.Set();
            return true;
        };

        var queue = new PartitionedBlockingCollection<Span>(1000, 3);
        queue.TryAdd(new Span());

        _service = GetService();
        _service.StartConsumingCollection(queue);

        Assert.That(channelCreated.Wait(TimeSpan.FromSeconds(5)), Is.True, "Service did not start");

        using (var logger = new TestUtilities.Logging())
        {
            _service.Wait(500);

            Assert.That(logger.HasMessageThatContains("Could not finish sending span data on exit"), Is.True);
        }

        // Cleanup: drain the queue so the Wait helper task can complete.
        while (queue.TryTake(out _))
        {
        }
    }

    [Test]
    public void ConfigSettingInvalidTestFlakyIsInvalidAndLogged()
    {
        TestFlakyValue = 150.0f; // valid range is 0..100

        using (var logger = new TestUtilities.Logging())
        {
            var svc = GetService();

            NrAssert.Multiple
            (
                () => Assert.That(svc.ReadAndValidateConfiguration(), Is.False, "Out-of-range flaky % should invalidate the configuration"),
                () => Assert.That(logger.HasMessageThatContains("Flaky %"), Is.True, "Expected a flaky % warning")
            );
        }
    }

    [Test]
    public void ConfigSettingInvalidTestFlakyCodeIsInvalidAndLogged()
    {
        Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceObserverTestFlakyCode).Returns(99); // valid range is 0..16

        using (var logger = new TestUtilities.Logging())
        {
            var svc = GetService();

            NrAssert.Multiple
            (
                () => Assert.That(svc.ReadAndValidateConfiguration(), Is.False, "Out-of-range flaky response code should invalidate the configuration"),
                () => Assert.That(logger.HasMessageThatContains("Flaky response code"), Is.True, "Expected a flaky response code warning")
            );
        }
    }

    [Test]
    public void ConfigSettingInvalidTestDelayIsInvalidAndLogged()
    {
        TestDelayValue = -5; // valid is >= 0

        using (var logger = new TestUtilities.Logging())
        {
            var svc = GetService();

            NrAssert.Multiple
            (
                () => Assert.That(svc.ReadAndValidateConfiguration(), Is.False, "Negative test delay should invalidate the configuration"),
                () => Assert.That(logger.HasMessageThatContains("Delay Ms"), Is.True, "Expected a delay warning")
            );
        }
    }

    [Test]
    [NonParallelizable]
    public void GrpcFailedPreconditionDuringConnectRestartsService()
    {
        var actualCountGrpcErrors = 0;
        var actualCountGeneralErrors = 0;

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
            .DoInstead<string>(sc => actualCountGrpcErrors++);

        Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
            .DoInstead(() => actualCountGeneralErrors++);

        var signalConnected = new ManualResetEventSlim();
        var createChannelCount = 0;
        _grpcWrapper.WithCreateChannelImpl = (host, port, ssl, token) =>
        {
            var attempt = Interlocked.Increment(ref createChannelCount);
            if (attempt == 1)
            {
                // FAILED_PRECONDITION on connect indicates a host redirect; the service should
                // restart and reconnect rather than shut down.
                MockGrpcUnaryWrapper<SpanBatch, RecordStatus>.ThrowGrpcWrapperException(StatusCode.FailedPrecondition, "Test gRPC Exception");
                return false;
            }

            signalConnected.Set();
            return true;
        };

        var sourceCollection = new PartitionedBlockingCollection<Span>(1000, 3);

        _service = GetService();
        _service.StartConsumingCollection(sourceCollection);

        NrAssert.Multiple
        (
            () => Assert.That(signalConnected.Wait(TimeSpan.FromSeconds(10)), Is.True, "Service did not reconnect after FAILED_PRECONDITION"),
            () => Assert.That(actualCountGrpcErrors, Is.EqualTo(1), "gRPC Error Count"),
            () => Assert.That(actualCountGeneralErrors, Is.EqualTo(1), "General Error Count"),
            () => Assert.That(createChannelCount, Is.EqualTo(2), "CreateChannel call count")
        );
    }

    //Valid host, various Port combos
    [TestCase("infiniteTracing.com", "443", null, true, 443, true)]
    [TestCase("infiniteTracing.com", null, "True", true, 443, true)]
    [TestCase("infiniteTracing.com", "", null, true, 443, true)]
    [TestCase("infiniteTracing.com", "abc", "False", false, -1, false)]
    [TestCase("my8Tdomain", "-1", "False", false, -1, false)]
    [TestCase("my8Tdomain", "80", "False", true, 80, false)]
    [TestCase("my8Tdomain", "20", "True", true, 20, true)]
    [TestCase("my8Tdomain", "8080", "False", true, 8080, false)]

    //No host means service always disabled
    [TestCase(null, "443", null, false, -1, true)]
    [TestCase(null, "", null, false, -1, true)]
    [TestCase(null, null, "True", false, -1, true)]
    [TestCase(null, "abc", "False", false, -1, true)]

    //No host means service disabled
    [TestCase("", "443", "False", false, -1, true)]
    [TestCase("", "", "True", false, -1, true)]
    [TestCase("", null, "False", false, -1, true)]
    [TestCase("", "abc", "True", false, -1, true)]

    //Hosts should not have scheme or port
    [TestCase("http://infiniteTracing.com", "443", null, false, -1, true)]
    [TestCase("http://infiniteTracing.com", null, null, false, -1, true)]
    [TestCase("http://infiniteTracing.com", "", null, false, -1, true)]
    [TestCase("http://infiniteTracing.com", "abc", null, false, -1, true)]
    [TestCase("infiniteTracing.com:443", "443", null, false, -1, true)]
    [TestCase("infiniteTracing.com:443", null, null, false, -1, true)]

    //Various invalid hosts
    [TestCase("/relativeUrl", null, null, false, -1, true)]
    [TestCase("//mydomain", null, null, false, -1, true)]
    [TestCase("//mydomain/", null, null, false, -1, true)]
    [TestCase("https:///", null, null, false, -1, true)]
    [TestCase("my8Tdomain/some/path", null, null, false, -1, true)]

    //various ssl setting
    [TestCase("infiniteTracing.com", "443", "1", false, -1, true)]
    [TestCase("infiniteTracing.com", "443", "0", false, -1, true)]
    [TestCase("infiniteTracing.com", "443", "a", false, -1, true)]
    [TestCase("infiniteTracing.com", "443", "", true, 443, true)]
    [TestCase("infiniteTracing.com", "443", " ", true, 443, true)]
    [TestCase("infiniteTracing.com", "443", null, true, 443, true)]
    [TestCase("infiniteTracing.com", "443", "true", true, 443, true)]
    [TestCase("infiniteTracing.com", "443", "false", true, 443, false)]
    public void MalformedUriPreventsStart(string testHost, string testPort, string testSsl, bool expectedIsValidConfig, int expectedPort, bool expectedSsl)
    {
        Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceObserverHost).Returns(testHost);
        Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceObserverPort).Returns(testPort);
        Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceObserverSsl).Returns(testSsl);

        var service = GetService();
        var actualIsValidConfig = service.ReadAndValidateConfiguration();
        var actualHost = service.EndpointHost;
        var actualPort = service.EndpointPort;
        var actualSsl = service.EndpointSsl;

        NrAssert.Multiple
        (
            () => Assert.That(actualIsValidConfig, Is.EqualTo(expectedIsValidConfig), "Configuration is valid")
        );

        if (!expectedIsValidConfig)
        {
            NrAssert.Multiple
            (
                () => Assert.That(actualHost, Is.Null, "Invalid config shouldn't have host"),
                () => Assert.That(actualPort, Is.EqualTo(-1), "Invalid config should have port -1"),
                () => Assert.That(actualSsl, Is.EqualTo(true), "Invalid config should have SSL true")
            );
        }
        else
        {
            NrAssert.Multiple
            (
                () => Assert.That(actualHost, Is.EqualTo(testHost), "Host"),
                () => Assert.That(actualPort, Is.EqualTo(expectedPort), "Port"),
                () => Assert.That(actualSsl, Is.EqualTo(expectedSsl), "SSL")
            );
        }
    }

    [TestCase(-1, false)]
    [TestCase(0, false)]
    [TestCase(10, true)]
    [TestCase(1000, true)]
    public void ConfigSettingsValidTimeoutConnect(int configValue, bool expectedIsConfigValid)
    {
        Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceTimeoutMsConnect).Returns(configValue);

        var svc = GetService();

        var actualIsConfigValid = svc.ReadAndValidateConfiguration();

        Assert.That(actualIsConfigValid, Is.EqualTo(expectedIsConfigValid), "Is Config Valid");
    }

    [TestCase(-1, false)]
    [TestCase(0, false)]
    [TestCase(10, true)]
    [TestCase(1000, true)]
    public void ConfigSettingsValidTimeoutSendData(int configValue, bool expectedIsConfigValid)
    {
        Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceTimeoutMsSendData).Returns(configValue);

        var svc = GetService();

        var actualIsConfigValid = svc.ReadAndValidateConfiguration();

        Assert.That(actualIsConfigValid, Is.EqualTo(expectedIsConfigValid), "Is Config Valid");
    }

    [TestCase(-1, false)]
    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(10, true)]
    [TestCase(500, true)]
    public void ConfigSettingValidBatchSize(int configValue, bool expectedIsConfigValid)
    {
        Mock.Arrange(() => _currentConfiguration.InfiniteTracingBatchSizeSpans).Returns(configValue);

        var svc = GetService();

        var actualIsConfigValid = svc.ReadAndValidateConfiguration();

        Assert.That(actualIsConfigValid, Is.EqualTo(expectedIsConfigValid), "Is Config Valid");
    }
}

#endif
