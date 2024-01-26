// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Collections;
using NewRelic.SystemInterfaces;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Spans.Tests
{
    public class MockResponseStream<TResponse> : IAsyncStreamReader<TResponse>
        where TResponse : class
    {
        private BlockingCollection<TResponse> _responses = new BlockingCollection<TResponse>();

        public TResponse Current { get; private set; }

        public void AddResponse(TResponse response)
        {
            _responses.Add(response);
        }

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {

            try
            {
                Current = _responses.Take(cancellationToken);
                return Task.FromResult(true);
            }
            catch
            {
                Current = null;
                return Task.FromResult(false);
            }
        }
    }

    public class MockGrpcWrapper<TRequest, TResponse> : IGrpcWrapper<TRequest, TResponse>
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

        public static void ThrowGrpcWrapperStreamsNotAvailableException(string message)
        {
            throw new GrpcWrapperStreamNotAvailableException(message, new Exception("Test Exception"));
        }

        public static Tuple<IClientStreamWriter<TRequest>, MockResponseStream<TResponse>> CreateStreams()
        {
            var requestStream = Mock.Create<IClientStreamWriter<TRequest>>();
            var responseStream = new MockResponseStream<TResponse>();

            return new Tuple<IClientStreamWriter<TRequest>, MockResponseStream<TResponse>>(requestStream, responseStream);
        }

        public System.Func<bool> WithIsConnectedImpl { get; set; } = () => true;

        public System.Func<string, int, bool, Metadata, CancellationToken, bool> WithCreateChannelImpl { get; set; } = (host, port, ssl, headers, cancellationToken) => true;

        public System.Func<Metadata, CancellationToken, Tuple<IClientStreamWriter<TRequest>, MockResponseStream<TResponse>>> WithCreateStreamsImpl { get; set; } = (metadata, CancellationToken) =>
        {
            return CreateStreams();
        };

        public System.Action WithShutdownImpl { get; set; } = () => { };

        public System.Func<IClientStreamWriter<TRequest>, TRequest, int, CancellationToken, bool> WithTrySendDataImpl { get; set; } = (requestStream, request, timeoutMs, CancellationToken) => true;

        public bool IsConnected => WithIsConnectedImpl?.Invoke() ?? false;

        public bool CreateChannel(string host, int port, bool ssl, Metadata headers, int connectTimeoutMs, CancellationToken cancellationToken)
        {
            var connected = WithCreateChannelImpl?.Invoke(host, port, ssl, headers, cancellationToken) ?? false;
            return connected;
        }

        public bool CreateStreams(Metadata headers, int connectTimeoutMs, CancellationToken cancellationToken, out IClientStreamWriter<TRequest> requestStream, out IAsyncStreamReader<TResponse> responseStream)
        {
            var streams = WithCreateStreamsImpl?.Invoke(headers, cancellationToken);

            requestStream = streams?.Item1;
            responseStream = streams?.Item2;

            return streams != null;
        }

        public void Shutdown()
        {
            WithShutdownImpl?.Invoke();
        }

        public void TryCloseRequestStream(IClientStreamWriter<TRequest> requestStream)
        {
        }

        public bool TrySendData(IClientStreamWriter<TRequest> stream, TRequest item, int timeoutWindowMs, CancellationToken cancellationToken)
        {
            return WithTrySendDataImpl(stream, item, timeoutWindowMs, cancellationToken);
        }
    }

    internal class SpanStreamingServiceTests : DataStreamingServiceTests<SpanStreamingService, Span, SpanBatch, RecordStatus>
    {
        public SpanStreamingServiceTests() : base("Span")
        {
        }

        protected override IConfiguration GetDefaultConfiguration()
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

        protected override SpanStreamingService GetService(IDelayer delayer, IGrpcWrapper<SpanBatch, RecordStatus> grpcWrapper, IConfigurationService configSvc, IAgentHealthReporter agentHealthReporter)
        {
            return new SpanStreamingService(grpcWrapper, delayer, configSvc, agentHealthReporter, _agentTimerService, _environment);
        }

        protected override Span GetRequestModel()
        {
            return new Span();
        }

        protected override IEnumerable<Span> GetBatchItems(SpanBatch batch)
        {
            return batch.Spans;
        }

        protected override RecordStatus GetResponseModel(ulong messagesSeen)
        {
            return new RecordStatus { MessagesSeen = messagesSeen };
        }
    }

    [TestFixture]
    [NonParallelizable]
    internal abstract class DataStreamingServiceTests<TService, TRequest, TRequestBatch, TResponse>
        where TService : IDataStreamingService<TRequest, TRequestBatch, TResponse>
        where TRequest : class, IStreamingModel
        where TRequestBatch : class, IStreamingBatchModel<TRequest>
        where TResponse : class
    {
        protected const string _validHost = "infiniteTracing.net";
        protected const string _validPort = "443";
        protected const string DefaultLicenseKey = "defaultlicensekey";
        protected const string DefaultAgentRunToken = "defaultagentruntoken";

        protected abstract IConfiguration GetDefaultConfiguration();
        protected abstract TService GetService(IDelayer delayer, IGrpcWrapper<TRequestBatch, TResponse> grpcWrapper, IConfigurationService configSvc, IAgentHealthReporter agentHealthReporter);
        protected abstract TRequest GetRequestModel();
        protected abstract TResponse GetResponseModel(ulong messagesSeen);
        protected abstract IEnumerable<TRequest> GetBatchItems(TRequestBatch batch);

        protected float? TestFlakyValue;
        protected int? TestDelayValue;
        protected Dictionary<string, string> TestRequestHeadersMap;
        protected bool TestCompressionSetting = true;
        protected readonly string _requestObjectTypeName;

        private MockGrpcWrapper<TRequestBatch, TResponse> _grpcWrapper;
        private IDelayer _delayer;
        private IConfigurationService _configSvc;
        protected IConfiguration _currentConfiguration => _configSvc?.Configuration;
        private TService _streamingSvc;
        protected IAgentHealthReporter _agentHealthReporter;
        protected IAgentTimerService _agentTimerService;
        protected IEnvironment _environment;

        private StatusCode[] _grpcErrorStatusCodes;

        public DataStreamingServiceTests(string requestObjectTypeName)
        {
            _requestObjectTypeName = requestObjectTypeName;

            var statusCodes = new List<StatusCode>();
            foreach (var statusCodeObj in Enum.GetValues(typeof(StatusCode)))
            {
                statusCodes.Add((StatusCode)statusCodeObj);
            }

            _grpcErrorStatusCodes = statusCodes.ToArray();
        }

        protected IClientStreamWriter<TRequest> GetMockRequestStream()
        {
            return Mock.Create<IClientStreamWriter<TRequest>>();
        }

        [SetUp]
        public virtual void Setup()
        {
            TestFlakyValue = null;
            TestDelayValue = null;
            TestRequestHeadersMap = null;

            _grpcWrapper = new MockGrpcWrapper<TRequestBatch, TResponse>();
            _delayer = Mock.Create<IDelayer>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            _agentTimerService = Mock.Create<IAgentTimerService>();
            _configSvc = Mock.Create<IConfigurationService>();
            _environment = Mock.Create<IEnvironment>();

            var defaultConfig = GetDefaultConfiguration();
            Mock.Arrange(() => _configSvc.Configuration).Returns(defaultConfig);
        }

        [TearDown]
        public void Teardown()
        {
            _streamingSvc?.Shutdown(false);
            _streamingSvc?.Dispose();
            _agentHealthReporter = null;
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void IsServiceEnabledTests(bool isServiceEnabled, bool isChannelConnected, bool expectedIsServiceAvailable)
        {
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceObserverHost).Returns(isServiceEnabled ? _validHost : null as string);

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);

            var signalIsDone = new ManualResetEventSlim();
            var countSends = 0;

            _grpcWrapper.WithIsConnectedImpl = () => isChannelConnected;
            _grpcWrapper.WithTrySendDataImpl = (requestStream, request, timeoutMs, CancellationToken) =>
            {
                countSends++;
                if (countSends > 1)
                {
                    signalIsDone.Set();
                }

                return true;
            };

            if (!isServiceEnabled || !isChannelConnected)
            {
                signalIsDone.Set();
            }

            var collection = new PartitionedBlockingCollection<TRequest>(10, 3);
            collection.TryAdd(GetRequestModel());
            collection.TryAdd(GetRequestModel());
            collection.TryAdd(GetRequestModel());

            _streamingSvc.StartConsumingCollection(collection);

            Assert.Multiple(() =>
            {
                Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(_streamingSvc.IsServiceAvailable, Is.EqualTo(expectedIsServiceAvailable), $"If IsServiceEnabled={isServiceEnabled} and IsGrpcChannelConnected={isChannelConnected}, IsServiceAvailable should be {expectedIsServiceAvailable}");
            });
        }

        [TestCase(false, false, false, false)]
        [TestCase(true, false, false, false)]
        [TestCase(false, true, false, false)]
        [TestCase(false, false, true, false)]
        [TestCase(false, false, false, true)]
        [TestCase(true, true, true, true)]
        public void ShouldHaveCorrectConnectionMetadata(bool includeFlaky, bool includeDelay, bool includeRequestHeadersMap, bool enableCompression)
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

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);

            Dictionary<string, string> actualConnectionMetadata = new Dictionary<string, string>();
            _grpcWrapper.WithCreateChannelImpl = (host, port, ssl, headers, token) =>
            {
                actualConnectionMetadata = headers.ToDictionary(k => k.Key, v => v.Value);

                //Throw the unimplemented exception so that the service will shutdown
                MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.Unimplemented, "Test gRPC Exception");
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

            var sourceCollection = new PartitionedBlockingCollection<TRequest>(10, 3);

            _streamingSvc.StartConsumingCollection(sourceCollection);

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
                _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);

                Dictionary<string, string> actualConnectionMetadata = new Dictionary<string, string>();
                _grpcWrapper.WithCreateChannelImpl = (host, port, ssl, headers, token) =>
                {
                    //Throw the unimplemented exception so that the service will shutdown
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.Unimplemented, "Test gRPC Exception");
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

                var sourceCollection = new PartitionedBlockingCollection<TRequest>(10, 3);

                _streamingSvc.StartConsumingCollection(sourceCollection);

                NrAssert.Multiple
                (
                    () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
                    () => Assert.That(logger.HasMessageThatContains("'grpc_proxy'"), Is.EqualTo(expectWarningInLogs))
                );
            }
        }

        private const int _expectedDelayAfterErrorSendingASpan = 15000;
        private readonly int[] _expectedDelaySequenceConnect = new[] { 15000, 15000, 30000, 60000, 120000, 300000, 300000, 300000, 300000, 300000, 300000, 300000, 300000, 300000 };

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
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);

            var expectedDelays = _expectedDelaySequenceConnect.Take(succeedOnAttempt).ToList();
            var actualDelays = new List<int>();

            //Arrange
            Mock.Arrange(() => _delayer.Delay(Arg.IsAny<int>(), Arg.IsAny<CancellationToken>()))
                .DoInstead<int, CancellationToken>((delay, token) =>
                {
                    actualDelays.Add(delay);
                });

            var signalIsDone = new ManualResetEventSlim();
            int attemptsMade = 0;
            _grpcWrapper.WithCreateChannelImpl = (host, port, ssl, headers, token) =>
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

            var sourceCollection = new PartitionedBlockingCollection<TRequest>(1000, 3);

            _streamingSvc.StartConsumingCollection(sourceCollection);

            NrAssert.Multiple
            (
                () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal Didn't Fire"),
                () => Assert.That(_expectedDelaySequenceConnect.Count(), Is.GreaterThanOrEqualTo(succeedOnAttempt)),
                () => Assert.That(actualDelays, Is.EqualTo(expectedDelays).AsCollection, $"After {succeedOnAttempt} attempt(s), delays should have been {string.Join(",", expectedDelays)}, but were {string.Join(",", actualDelays)}")
            );
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
        public void BackoffRetry_CreateStream_DelaySequence(int succeedOnAttempt, bool throwExceptionDuringCreateStream)
        {
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);

            var expectedDelays = _expectedDelaySequenceConnect.Take(succeedOnAttempt).ToList();
            var actualDelays = new List<int>();

            //Arrange
            Mock.Arrange(() => _delayer.Delay(Arg.IsAny<int>(), Arg.IsAny<CancellationToken>()))
                .DoInstead<int, CancellationToken>((delay, token) =>
                {
                    actualDelays.Add(delay);
                });

            var signalIsDone = new ManualResetEventSlim();
            int attemptsMade = 0;
            _grpcWrapper.WithCreateStreamsImpl = (metadata, CancellationToken) =>
            {
                var attempt = attemptsMade++;
                if (attempt == succeedOnAttempt)
                {
                    signalIsDone.Set();
                    return MockGrpcWrapper<TRequestBatch, TResponse>.CreateStreams();
                }

                if (throwExceptionDuringCreateStream)
                {
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.Unknown, "Test gRPC Exception");
                }

                return null;
            };

            var sourceCollection = new PartitionedBlockingCollection<TRequest>(1000, 3);

            _streamingSvc.StartConsumingCollection(sourceCollection);

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
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);

            var actualAttempts = new List<TRequest>();
            var haveProcessedFailure = false;

            var item1 = GetRequestModel();
            var item2 = GetRequestModel();
            var item3 = GetRequestModel();
            var item4 = GetRequestModel();
            var item5 = GetRequestModel();

            _grpcWrapper.WithTrySendDataImpl = (stream, requestBatch, timeout, token) =>
                {
                    var requests = GetBatchItems(requestBatch);
                    actualAttempts.AddRange(requests);

                    if (requests.Contains(item3) && !haveProcessedFailure)
                    {
                        haveProcessedFailure = true;
                        return false;
                    }

                    return true;
                };

            var sourceCollection = new PartitionedBlockingCollection<TRequest>(100, 5);

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

            _streamingSvc.StartConsumingCollection(sourceCollection);

            var expectedAttempts = new[] { item1, item2, item3, item4, item5, item3 };

            NrAssert.Multiple
            (
                () => Assert.That(waitForConsumptionTask.Wait(TimeSpan.FromSeconds(10)), Is.True, "Task didn't complete"),
                () => Assert.That(actualAttempts, Is.EqualTo(expectedAttempts).AsCollection)
            );
        }

        [Test]
        [NonParallelizable]
        public void DelayCallingRecordSpanAfterAnErrorStreamingASpan()
        {
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);

            var actualDelays = new List<int>();

            Mock.Arrange(() => _delayer.Delay(Arg.IsAny<int>(), Arg.IsAny<CancellationToken>()))
                .DoInstead<int, CancellationToken>((delay, token) =>
                {
                    actualDelays.Add(delay);
                });

            var actualAttempts = new List<TRequest>();
            var haveProcessedFailure = false;

            var item1 = GetRequestModel();

            _grpcWrapper.WithTrySendDataImpl = (stream, requestBatch, timeout, token) =>
            {

                var requests = GetBatchItems(requestBatch);
                actualAttempts.AddRange(requests);

                if (requests.Contains(item1) && !haveProcessedFailure)
                {
                    haveProcessedFailure = true;
                    return false;
                }

                return true;
            };

            var sourceCollection = new PartitionedBlockingCollection<TRequest>(1000, 3);

            sourceCollection.TryAdd(item1);

            var waitForConsumptionTask = Task.Run(() =>
            {
                while (sourceCollection.Count > 0)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(250));
                }
                Thread.Sleep(TimeSpan.FromMilliseconds(250));
            });

            _streamingSvc.StartConsumingCollection(sourceCollection);

            //var expectedAttempts = new[] { item1, item1 };
            var expectedAttempts = new List<TRequest>() { item1, item1 };

            Assert.Multiple(() =>
            {
                Assert.That(waitForConsumptionTask.Wait(TimeSpan.FromSeconds(10)), Is.True, "Task didn't complete");
                Assert.That(actualAttempts, Is.EqualTo(expectedAttempts).AsCollection);
                Assert.That(actualDelays, Is.EqualTo(new[] { _expectedDelayAfterErrorSendingASpan }).AsCollection);
            });
        }

        [Test]
        public void MultipleConsumersItemsSentOnlyOnce()
        {
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingBatchSizeSpans).Returns(4);
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceCountConsumers).Returns(3);
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);

            var actualItems = new ConcurrentBag<TRequest>();
            var requestItems = new ConcurrentBag<TRequest>();
            for (var i = 0; i < 100; i++)
            {
                requestItems.Add(GetRequestModel());
            }

            var expectedItems = requestItems.ToList();

            _grpcWrapper.WithTrySendDataImpl = (stream, requestBatch, timeout, token) =>
                {
                    var requests = GetBatchItems(requestBatch);
                    foreach (var streamingModel in requests)
                        actualItems.Add(streamingModel);

                    return true;
                };

            var sourceCollection = new PartitionedBlockingCollection<TRequest>(requestItems.Count + 100, 3, requestItems);

            var waitForConsumptionTask = Task.Run(() =>
            {
                while (sourceCollection.Count > 0)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(250));
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(250));
            });

            _streamingSvc.StartConsumingCollection(sourceCollection);

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

            var config = GetDefaultConfiguration();
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingBatchSizeSpans).Returns(configBatchSize);

            var actualBatchSizes = new List<int>();
            _grpcWrapper.WithTrySendDataImpl = (stream, batch, timeoutMs, token) =>
            {
                var items = GetBatchItems(batch);
                actualBatchSizes.Add(items.Count());

                if (actualBatchSizes.Sum(x => x) >= expectedCountItems)
                {
                    signalIsDone.Set();
                }

                return true;
            };

            var queue = new PartitionedBlockingCollection<TRequest>(1000, 2);
            for (var i = 0; i < expectedCountItems; i++)
            {
                queue.TryAdd(GetRequestModel());
            }

            var queueCount = queue.Count;



            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);
            _streamingSvc.StartConsumingCollection(queue);


            NrAssert.Multiple
            (
                () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
                () => Assert.That(queueCount, Is.EqualTo(expectedCountItems), "Collection count"),
                () => Assert.That(actualBatchSizes, Has.Count.EqualTo(expectedBatchSizes.Length), "Number of Batches"),
                () => Assert.That(actualBatchSizes.ToArray(), Is.EqualTo(expectedBatchSizes).AsCollection, "Batch Sizes")
            );
        }

        [Test]
        public void ErrorsWithTheRequestStream_ShouldShutdownResponseStream()
        {
            var signalIsDone = new ManualResetEventSlim();
            var streamCancellationTokens = new List<CancellationToken>();

            var actualAttempts = 0;
            _grpcWrapper.WithTrySendDataImpl = (stream, item, timeoutMs, token) =>
            {
                var attempt = actualAttempts++;
                if (attempt > 0)
                {
                    signalIsDone.Set();
                    return true;
                }

                return false;
            };

            _grpcWrapper.WithCreateStreamsImpl = (metadata, cancellationToken) =>
            {
                streamCancellationTokens.Add(cancellationToken);
                return MockGrpcWrapper<TRequestBatch, TResponse>.CreateStreams();
            };

            var queue = new PartitionedBlockingCollection<TRequest>(1000, 3);

            queue.TryAdd(GetRequestModel());

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);
            _streamingSvc.StartConsumingCollection(queue);

            NrAssert.Multiple
            (
                () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
                () => Assert.That(streamCancellationTokens, Has.Count.EqualTo(2), "Did not see enough streams created"),
                () => Assert.That(streamCancellationTokens[0].IsCancellationRequested, Is.True, "The first stream cancellation token was not triggered."),
                () => Assert.That(streamCancellationTokens[1].IsCancellationRequested, Is.False, "The second stream cancellation token was triggered.")
            );
        }

        [Test]
        [NonParallelizable]
        public void ShuttingDownTheDataStreamingService_ShouldShutdownResponseStream()
        {
            var signalIsDone = new ManualResetEventSlim();
            var streamCancellationTokens = new List<CancellationToken>();

            var actualAttempts = 0;
            _grpcWrapper.WithTrySendDataImpl = (stream, item, timeoutMs, token) =>
            {
                var attempt = actualAttempts++;
                if (attempt > 0)
                {
                    _streamingSvc.Shutdown(false);
                    signalIsDone.Set();
                    return true;
                }

                return false;
            };

            _grpcWrapper.WithCreateStreamsImpl = (metadata, cancellationToken) =>
            {
                streamCancellationTokens.Add(cancellationToken);
                return MockGrpcWrapper<TRequestBatch, TResponse>.CreateStreams();
            };


            var queue = new PartitionedBlockingCollection<TRequest>(1000, 3);

            queue.TryAdd(GetRequestModel());

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);
            _streamingSvc.StartConsumingCollection(queue);

            Assert.Multiple(() =>
            {
                Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire");
                Assert.That(streamCancellationTokens, Has.Count.EqualTo(2), "Did not see enough streams created");

                Assert.That(WaitForCancellationTokenToBeCancelled(streamCancellationTokens[0]), Is.True,
                    "The first stream cancellation token was not triggered.");
                Assert.That(WaitForCancellationTokenToBeCancelled(streamCancellationTokens[1]), Is.True,
                    "The second stream cancellation token was not triggered.");
            });
        }

        private static bool WaitForCancellationTokenToBeCancelled(CancellationToken cancellationToken, int seconds = 1)
        {
            try
            {
                Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken).Wait(cancellationToken);
            }
            catch (Exception)
            {
                return true;
            }
            return false;
        }

        [Test]
        public void SupportabilityMetrics_ResponseReceived()
        {
            using (var gotResponseReceivedEvent = new AutoResetEvent(false))
            {
                long responseMessagesSeen = 0;

                Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceCountConsumers).Returns(1);

                _grpcWrapper.WithCreateStreamsImpl = (metadata, CancellationToken) =>
                {
                    var streams = MockGrpcWrapper<TRequestBatch, TResponse>.CreateStreams();
                    streams.Item2.AddResponse(GetResponseModel(5));

                    return streams;
                };

                Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanEventsReceived(Arg.IsAny<ulong>()))
                    .DoInstead<ulong>(count =>
                    {
                        responseMessagesSeen += (long)count;
                        gotResponseReceivedEvent.Set();
                    });

                _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);

                _streamingSvc.StartConsumingCollection(new PartitionedBlockingCollection<TRequest>(1000, 3));

                Assert.Multiple(() =>
                {
                    Assert.That(gotResponseReceivedEvent.WaitOne(TimeSpan.FromSeconds(10)), Is.True, "Trigger Didn't Fire");
                    Assert.That(responseMessagesSeen, Is.EqualTo(5));
                });
            }
        }

        [Test]
        public void SupportabilityMetrics_Errors_OnConnect()
        {
            var statusCodesForNormalErrors = _grpcErrorStatusCodes.Where(s => s != StatusCode.Unimplemented && s != StatusCode.OK).ToArray();
            var expectedCountGrpcErrors = _grpcErrorStatusCodes.ToDictionary(x => EnumNameCache<StatusCode>.GetNameToUpperSnakeCase(x), x => 2);
            expectedCountGrpcErrors.Remove("UNIMPLEMENTED"); //This error triggers a shutdown so we'll test it separately
            expectedCountGrpcErrors.Remove("OK"); //This error signals an immediate restart so we'll test it separately
            var expectedCountGeneralErrors = (statusCodesForNormalErrors.Length * 2) + 1;

            var actualCountGrpcErrors = new Dictionary<string, int>();
            var actualCountGeneralErrors = 0;

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
                .DoInstead<string>((sc) =>
                {
                    if (!actualCountGrpcErrors.ContainsKey(sc))
                    {
                        actualCountGrpcErrors[sc] = 0;
                    }

                    actualCountGrpcErrors[sc]++;
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
                .DoInstead(() => actualCountGeneralErrors++);


            var signalIsDone = new ManualResetEventSlim();
            var actualAttempts = 0;
            _grpcWrapper.WithCreateChannelImpl = (host, port, ssl, headers, token) =>
            {
                var attempt = actualAttempts++;
                if (attempt > statusCodesForNormalErrors.Length * 2)
                {
                    signalIsDone.Set();
                    return true;
                }


                if (attempt == statusCodesForNormalErrors.Length * 2)
                {
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException("Test General Exception");
                    return false;
                }

                var statusCodeToThrow = statusCodesForNormalErrors[attempt % statusCodesForNormalErrors.Length];

                MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(statusCodeToThrow, "Test gRPC Exception");
                return false;
            };

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);
            _streamingSvc.StartConsumingCollection(new PartitionedBlockingCollection<TRequest>(1000, 3));

            NrAssert.Multiple
            (
                () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
                () => Assert.That(actualCountGrpcErrors, Is.EqualTo(expectedCountGrpcErrors).AsCollection, "gRPC Error Count"),
                () => Assert.That(actualCountGeneralErrors, Is.EqualTo(expectedCountGeneralErrors), "General Error Count")
            );
        }

        [Test]
        public void SupportabilityMetrics_TimeoutOnSend()
        {
            var actualCountTimeouts = 0;
            var signalIsDone = new ManualResetEventSlim();

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcTimeout())
                .DoInstead(() => actualCountTimeouts++);

            var actualAttempts = 0;
            _grpcWrapper.WithTrySendDataImpl = (stream, item, timeoutMs, token) =>
            {
                var attempt = actualAttempts++;
                if (attempt > 0)
                {
                    signalIsDone.Set();
                    return true;
                }

                return false;
            };


            var queue = new PartitionedBlockingCollection<TRequest>(1000, 3);
            queue.TryAdd(GetRequestModel());

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);
            _streamingSvc.StartConsumingCollection(queue);

            NrAssert.Multiple
            (
                () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
                () => Assert.That(actualCountTimeouts, Is.EqualTo(1), "gRPC Timeout Count")
            );
        }

        [Test]
        public void SupportabilityMetrics_Errors_OnSend()
        {
            var expectedCountGrpcErrors = _grpcErrorStatusCodes
                .Where(x => x != StatusCode.Unimplemented)
                .ToDictionary(x => EnumNameCache<StatusCode>.GetNameToUpperSnakeCase(x), x => 2);

            var testGrpcErrorCodes = _grpcErrorStatusCodes
                .Where(x => x != StatusCode.Unimplemented)
                .ToArray();


            var expectedCountGeneralErrors = (testGrpcErrorCodes.Length * 2)    //Each status error thrown 2x
                                            + 1;                                //General Error by itself

            var actualCountGrpcErrors = new Dictionary<string, int>();
            var actualCountGeneralErrors = 0;

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
                .DoInstead<string>((sc) =>
                {
                    if (!actualCountGrpcErrors.ContainsKey(sc))
                    {
                        actualCountGrpcErrors[sc] = 0;
                    }

                    actualCountGrpcErrors[sc]++;
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
                .DoInstead(() =>
                {
                    actualCountGeneralErrors++;
                });


            var signalIsDone = new ManualResetEventSlim();
            var invocationId = -1;
            _grpcWrapper.WithTrySendDataImpl = (stream, item, timeoutMs, token) =>
            {
                var localInvocationId = Interlocked.Increment(ref invocationId);

                if (localInvocationId == (testGrpcErrorCodes.Length * 2) + 1)
                {
                    signalIsDone.Set();
                    return true;
                }

                if (localInvocationId == testGrpcErrorCodes.Length * 2)
                {
                    throw new Exception("Test General Exception");
                }

                var statusCodeToThrow = testGrpcErrorCodes[localInvocationId % testGrpcErrorCodes.Length];
                MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(statusCodeToThrow, "Test gRPC Exception");
                return false;
            };

            var queue = new PartitionedBlockingCollection<TRequest>(1000, 3);
            queue.TryAdd(GetRequestModel());

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);
            _streamingSvc.StartConsumingCollection(queue);

            NrAssert.Multiple
            (
                () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
                () => Assert.That(actualCountGrpcErrors, Is.EqualTo(expectedCountGrpcErrors).AsCollection, "gRPC Error Count"),
                () => Assert.That(actualCountGeneralErrors, Is.EqualTo(expectedCountGeneralErrors), "General Error Count")
            );
        }


        [Test]
        [NonParallelizable]
        public void SupportabilityMetrics_ItemsSent_BatchSizeAndCount()
        {
            const int maxBatchSize = 17;

            Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceCountConsumers).Returns(3);
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingBatchSizeSpans).Returns(maxBatchSize);

            var signalIsDone = new ManualResetEventSlim();
            const int countItemsToProcess = 987;

            var countBatchesSent = 0;
            var countItemsSent = 0;
            _grpcWrapper.WithTrySendDataImpl = (requestStream, request, timeoutMs, CancellationToken) =>
            {
                Interlocked.Increment(ref countBatchesSent);
                if (Interlocked.Add(ref countItemsSent, request.Count) == countItemsToProcess)
                {
                    signalIsDone.Set();
                }

                return true;
            };

            var metricBuilder = Mock.Create<IMetricBuilder>();

            float? actualBatchSizeTotal = null;
            int? actualBatchCountSamples = null;
            float? actualBatchSizeMin = null;
            float? actualBatchSizeMax = null;
            float? actualAvgBatchSize = null;

            Mock.Arrange(() => metricBuilder.TryBuildSupportabilitySummaryMetric($"InfiniteTracing/{_requestObjectTypeName}/BatchSize", Arg.IsAny<float>(), Arg.IsAny<int>(), Arg.IsAny<float>(), Arg.IsAny<float>()))
                .DoInstead<string, float, int, float, float>((metricName, total, count, min, max) =>
                {
                    actualBatchSizeTotal = total;
                    actualBatchCountSamples = count;
                    actualBatchSizeMin = min;
                    actualBatchSizeMax = max;
                    actualAvgBatchSize = total / count;
                });

            long? actualCountSent = null;
            Mock.Arrange(() => metricBuilder.TryBuildSupportabilityCountMetric($"InfiniteTracing/{_requestObjectTypeName}/Sent", Arg.IsAny<long>()))
                .DoInstead<string, long>((metricName, countSent) =>
                {
                    actualCountSent = countSent;
                });

            var agentHealthReporter = new AgentHealthReporter(metricBuilder, Mock.Create<IScheduler>());

            agentHealthReporter.RegisterPublishMetricHandler(metric => { });

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, agentHealthReporter);

            var collection = new PartitionedBlockingCollection<TRequest>(1000, 3);
            for (var i = 0; i < countItemsToProcess; i++)
            {
                collection.TryAdd(GetRequestModel());
            }

            _streamingSvc.StartConsumingCollection(collection);

            Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(5)), Is.True, "Signal Didn't fire");

            // We sleep here since there are actually 3 consumers all doing things, and we need to give them time
            // to finish aggregating their stats (what this test covers).
            Thread.Sleep(TimeSpan.FromSeconds(5));

            agentHealthReporter.CollectMetrics();

            Assert.Multiple(() =>
            {
                Assert.That(actualCountSent, Is.EqualTo(countItemsToProcess), "All Items Processed through GRPC");
                Assert.That(actualBatchSizeTotal, Is.EqualTo(countItemsToProcess), "All items reported through Agent Health Reporter");
                Assert.That(actualBatchSizeMin, Is.LessThanOrEqualTo(actualBatchSizeMax), "Min batch size should be less than max");
                Assert.That(actualBatchSizeMin, Is.LessThanOrEqualTo(actualAvgBatchSize), "Avg batch size should be greater than min");
                Assert.That(actualAvgBatchSize, Is.LessThanOrEqualTo(actualBatchSizeMax), "Avg batch size should be less than than max");
                Assert.That(actualBatchSizeMax, Is.LessThanOrEqualTo(maxBatchSize), "Max Batch Size should not exceed the constrained value");
            });
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
        [TestCase(null, "abc", "False", false, -1, true)]      //??

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
        public void MalformedUriPreventsStartRecordSupportabilityMetric(string testHost, string testPort, string testSsl, bool expectedIsValidConfig, int expectedPort, bool expectedSsl)
        {
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceObserverHost).Returns(testHost);
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceObserverPort).Returns(testPort);
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceObserverSsl).Returns(testSsl);

            var streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);
            var actualIsValidConfig = streamingSvc.ReadAndValidateConfiguration();
            var actualHost = streamingSvc.EndpointHost;
            var actualPort = streamingSvc.EndpointPort;
            var actualSsl = streamingSvc.EndpointSsl;


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

        [Test]
        public void GrpcUnimplementedDuringTrySendDataShutsDownService()
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
                .DoInstead<string>((sc) =>
                {
                    actualCountGrpcErrors++;
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
                .DoInstead(() =>
                {
                    actualCountGeneralErrors++;
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanEventsSent(Arg.IsAny<long>()))
                .DoInstead<long>((cnt) =>
                {
                    actualCountSpansSent += cnt;
                });


            var invocationId = 0;
            _grpcWrapper.WithTrySendDataImpl = (stream, item, timeoutMs, token) =>
            {
                var localInvocationId = Interlocked.Increment(ref invocationId);

                if (localInvocationId == 3)
                {
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.Unimplemented, "Test gRPC Exception");
                }

                return true;
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

            var queue = new PartitionedBlockingCollection<TRequest>(1000, 3);
            queue.TryAdd(GetRequestModel());
            queue.TryAdd(GetRequestModel());
            queue.TryAdd(GetRequestModel());
            queue.TryAdd(GetRequestModel());
            queue.TryAdd(GetRequestModel());

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);
            _streamingSvc.StartConsumingCollection(queue);

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
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);

            var actualCountGrpcErrors = 0;
            var actualCountGeneralErrors = 0;

            var expectedCountGrpcErrors = 1;
            var expectedCountGeneralErrors = 1;

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
                .DoInstead<string>((sc) =>
                {
                    actualCountGrpcErrors++;
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
                .DoInstead(() =>
                {
                    actualCountGeneralErrors++;
                });

            _grpcWrapper.WithCreateChannelImpl = (host, port, ssl, headers, token) =>
            {
                MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.Unimplemented, "Test gRPC Exception");
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

            var sourceCollection = new PartitionedBlockingCollection<TRequest>(1000, 3);

            _streamingSvc.StartConsumingCollection(sourceCollection);

            Assert.Multiple(() =>
            {
                Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire");
                Assert.That(actualCountGrpcErrors, Is.EqualTo(expectedCountGrpcErrors), "gRPC Error Count");
                Assert.That(actualCountGeneralErrors, Is.EqualTo(expectedCountGeneralErrors), "General Error Count");
            });
        }

        [Test]
        public void GrpcUnimplementedDuringCreateStreamShutsDownService()
        {
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);

            var actualCountGrpcErrors = 0;
            var actualCountGeneralErrors = 0;

            var expectedCountGrpcErrors = 1;
            var expectedCountGeneralErrors = 1;

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
                .DoInstead<string>((sc) =>
                {
                    actualCountGrpcErrors++;
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
                .DoInstead(() =>
                {
                    actualCountGeneralErrors++;
                });

            _grpcWrapper.WithCreateStreamsImpl = (metadata, CancellationToken) =>
            {
                MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.Unimplemented, "Test gRPC Exception");
                return null;
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

            var sourceCollection = new PartitionedBlockingCollection<TRequest>(1000, 3);

            _streamingSvc.StartConsumingCollection(sourceCollection);

            NrAssert.Multiple
            (
                () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
                () => Assert.That(actualCountGrpcErrors, Is.EqualTo(expectedCountGrpcErrors), "gRPC Error Count"),
                () => Assert.That(actualCountGeneralErrors, Is.EqualTo(expectedCountGeneralErrors), "General Error Count")
            );
        }

        [TestCase(StatusCode.Unavailable)]
        [TestCase(StatusCode.FailedPrecondition)]
        public void GrpcUnavailableOrFailedPreconditionDuringTrySendDataRestartsService(StatusCode statusCode)
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
                .DoInstead<int, CancellationToken>((delay, token) =>
                {
                    actualDelays.Add(delay);
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
                .DoInstead<string>((sc) =>
                {
                    actualCountGrpcErrors++;
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
                .DoInstead(() =>
                {
                    actualCountGeneralErrors++;
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanEventsSent(Arg.IsAny<long>()))
                .DoInstead<long>((cnt) =>
                {
                    actualCountSpansSent += cnt;
                    signalIsDone.Set();
                });

            var sendInvocationId = 0;
            _grpcWrapper.WithTrySendDataImpl = (stream, item, timeoutMs, token) =>
            {
                var localInvocationId = Interlocked.Increment(ref sendInvocationId);

                if (localInvocationId == 1)
                {
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(statusCode, "Test gRPC Exception");
                    return false;
                }

                return true;
            };

            var createChannelInvocationCount = 0;
            _grpcWrapper.WithCreateChannelImpl = (host, port, ssl, metadata, token) =>
            {
                Interlocked.Increment(ref createChannelInvocationCount);
                return true;
            };


            var queue = new PartitionedBlockingCollection<TRequest>(1000, 3);
            queue.TryAdd(GetRequestModel());

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);
            _streamingSvc.StartConsumingCollection(queue);

            NrAssert.Multiple
            (
                () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(1000)), Is.True, "Signal didn't fire"),
                () => Assert.That(actualCountGrpcErrors, Is.EqualTo(expectedCountGrpcErrors), "gRPC Error Count"),
                () => Assert.That(actualCountGeneralErrors, Is.EqualTo(expectedCountGeneralErrors), "General Error Count"),
                () => Assert.That(actualCountSpansSent, Is.EqualTo(expectedCountSpansSent), "Span Sent Events"),
                () => Assert.That(actualDelays, Is.Empty, "The service should restart without triggering a delay"),
                () => Assert.That(createChannelInvocationCount, Is.EqualTo(expectedCreateChannelCount), "CreateChannel call count")
            );
        }

        [Test]
        public void GrpcUnavailableDuringConnectIsTreatedAsAnError()
        {
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);

            var actualCountGrpcErrors = 0;
            var actualCountGeneralErrors = 0;
            var actualDelays = new List<int>();

            var expectedCountGrpcErrors = 1;
            var expectedCountGeneralErrors = 1;

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
                .DoInstead<string>((sc) =>
                {
                    actualCountGrpcErrors++;
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
                .DoInstead(() =>
                {
                    actualCountGeneralErrors++;
                });

            Mock.Arrange(() => _delayer.Delay(Arg.IsAny<int>(), Arg.IsAny<CancellationToken>()))
                .DoInstead<int, CancellationToken>((delay, token) =>
                {
                    actualDelays.Add(delay);
                });

            var channelCreationCallCount = 0;
            _grpcWrapper.WithCreateChannelImpl = (host, port, ssl, headers, token) =>
            {
                Interlocked.Increment(ref channelCreationCallCount);
                if (channelCreationCallCount == 1)
                {
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.Unavailable, "Test gRPC Exception");
                    return false;
                }

                return true;
            };


            var sourceCollection = new PartitionedBlockingCollection<TRequest>(1000, 3);
            sourceCollection.TryAdd(GetRequestModel());

            var waitForConsumptionTask = Task.Run(() =>
            {
                while (sourceCollection.Count > 0)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(250));
                }
            });

            _streamingSvc.StartConsumingCollection(sourceCollection);

            NrAssert.Multiple
            (
                () => Assert.That(waitForConsumptionTask.Wait(TimeSpan.FromSeconds(10)), Is.True, "Task didn't complete"),
                () => Assert.That(actualCountGrpcErrors, Is.EqualTo(expectedCountGrpcErrors), "gRPC Error Count"),
                () => Assert.That(actualCountGeneralErrors, Is.EqualTo(expectedCountGeneralErrors), "General Error Count"),
                () => Assert.That(actualDelays, Is.EqualTo(_expectedDelaySequenceConnect.Take(1)).AsCollection, "The delay sequence did not match")
            );
        }

        [TestCase(StatusCode.Unavailable)]
        [TestCase(StatusCode.FailedPrecondition)]
        public void GrpcUnavailableOrFailedPreconditionDuringCreateStreamRestartsService(StatusCode grpcStatusCode)
        {
            var actualDelays = new List<int>();

            Mock.Arrange(() => _delayer.Delay(Arg.IsAny<int>(), Arg.IsAny<CancellationToken>()))
                .DoInstead<int, CancellationToken>((delay, token) =>
                {
                    actualDelays.Add(delay);
                });

            var actualCountGrpcErrors = 0;
            var actualCountGeneralErrors = 0;
            var actualCountSpansSent = 0L;

            var expectedCountGrpcErrors = 1;
            var expectedCountGeneralErrors = 1;
            var expectedCountSpansSent = 1;
            var expectedCreateChannelCount = 2;

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
                .DoInstead<string>((sc) =>
                {
                    actualCountGrpcErrors++;
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
                .DoInstead(() =>
                {
                    actualCountGeneralErrors++;
                });

            var signalIsDone = new ManualResetEventSlim();
            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanEventsSent(Arg.IsAny<long>()))
                .DoInstead<long>((cnt) =>
                {
                    actualCountSpansSent += cnt;
                    signalIsDone.Set();
                });


            var countCreateStreamsCalls = 0;
            _grpcWrapper.WithCreateStreamsImpl = (metadata, CancellationToken) =>
            {
                Interlocked.Increment(ref countCreateStreamsCalls);

                if (countCreateStreamsCalls == 1)
                {
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(grpcStatusCode, "Test gRPC Exception");
                    return null;
                }

                return MockGrpcWrapper<TRequestBatch, TResponse>.CreateStreams();
            };

            var createChannelInvocationCount = 0;
            _grpcWrapper.WithCreateChannelImpl = (host, port, ssl, metadata, token) =>
            {
                Interlocked.Increment(ref createChannelInvocationCount);
                return true;
            };


            var sourceCollection = new PartitionedBlockingCollection<TRequest>(1000, 3);
            sourceCollection.TryAdd(GetRequestModel());

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);
            _streamingSvc.StartConsumingCollection(sourceCollection);

            NrAssert.Multiple
            (
                () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
                () => Assert.That(actualCountGrpcErrors, Is.EqualTo(expectedCountGrpcErrors), "gRPC Error Count"),
                () => Assert.That(actualCountGeneralErrors, Is.EqualTo(expectedCountGeneralErrors), "General Error Count"),
                () => Assert.That(actualCountSpansSent, Is.EqualTo(expectedCountSpansSent), "Span Sent Events"),
                () => Assert.That(actualDelays, Is.Empty, "The service should restart without a delay"),
                () => Assert.That(createChannelInvocationCount, Is.EqualTo(expectedCreateChannelCount), "CreateChannel call count")
            );
        }

        [TestCase(StatusCode.Internal)]
        public void GrpcInternalDuringTrySendDataCreatesNewStreamDelayed(StatusCode statusCode)
        {
            var actualCountGrpcErrors = 0;
            var actualCountGeneralErrors = 0;
            var actualCountSpansSent = 0L;

            var expectedCountGrpcErrors = 3;
            var expectedCountGeneralErrors = 3;
            var expectedCountSpansSent = 1;

            var signalIsDone = new ManualResetEventSlim();

            var actualDelays = new List<int>();

            Mock.Arrange(() => _delayer.Delay(Arg.IsAny<int>(), Arg.IsAny<CancellationToken>()))
                .DoInstead<int, CancellationToken>((delay, token) =>
                {
                    actualDelays.Add(delay);
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
                .DoInstead<string>((sc) =>
                {
                    actualCountGrpcErrors++;
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
                .DoInstead(() =>
                {
                    actualCountGeneralErrors++;
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanEventsSent(Arg.IsAny<long>()))
                            .DoInstead<long>((cnt) =>
                            {
                                actualCountSpansSent += cnt;
                                signalIsDone.Set();
                            });


            var invocationId = 0;
            _grpcWrapper.WithTrySendDataImpl = (stream, item, timeoutMs, token) =>
            {
                var localInvocationId = Interlocked.Increment(ref invocationId);

                if (localInvocationId < 2 || localInvocationId == 3)
                {
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.Unknown, "Test gRPC Exception");
                    return false;
                }

                if (localInvocationId == 2)
                {
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(statusCode, "Test gRPC Exception");
                    return false;
                }

                return true;
            };

            var queue = new PartitionedBlockingCollection<TRequest>(1000, 3);
            queue.TryAdd(GetRequestModel());

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);
            _streamingSvc.StartConsumingCollection(queue);

            NrAssert.Multiple
            (
                () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
                () => Assert.That(actualCountGrpcErrors, Is.EqualTo(expectedCountGrpcErrors), "gRPC Error Count"),
                () => Assert.That(actualCountGeneralErrors, Is.EqualTo(expectedCountGeneralErrors), "General Error Count"),
                () => Assert.That(actualCountSpansSent, Is.EqualTo(expectedCountSpansSent), "Span Sent Events"),
                () => Assert.That(actualDelays, Is.EqualTo(new[] { _expectedDelayAfterErrorSendingASpan, _expectedDelayAfterErrorSendingASpan, _expectedDelayAfterErrorSendingASpan }).AsCollection, "The expected delay sequence did not match")
            );
        }

        [TestCase(StatusCode.OK)]
        public void GrpcOkDuringTrySendDataCreatesNewStreamImmediately(StatusCode statusCode)
        {
            var actualCountGrpcErrors = 0;
            var actualCountGeneralErrors = 0;
            var actualCountSpansSent = 0L;

            var expectedCountGrpcErrors = 3;
            var expectedCountGeneralErrors = 3;
            var expectedCountSpansSent = 1;

            var signalIsDone = new ManualResetEventSlim();

            var actualDelays = new List<int>();

            Mock.Arrange(() => _delayer.Delay(Arg.IsAny<int>(), Arg.IsAny<CancellationToken>()))
                .DoInstead<int, CancellationToken>((delay, token) =>
                {
                    actualDelays.Add(delay);
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
                .DoInstead<string>((sc) =>
                {
                    actualCountGrpcErrors++;
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
                .DoInstead(() =>
                {
                    actualCountGeneralErrors++;
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanEventsSent(Arg.IsAny<long>()))
                            .DoInstead<long>((cnt) =>
                            {
                                actualCountSpansSent += cnt;
                                signalIsDone.Set();
                            });


            var invocationId = 0;
            _grpcWrapper.WithTrySendDataImpl = (stream, item, timeoutMs, token) =>
            {
                var localInvocationId = Interlocked.Increment(ref invocationId);

                if (localInvocationId < 2 || localInvocationId == 3)
                {
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.Unknown, "Test gRPC Exception");
                    return false;
                }

                if (localInvocationId == 2)
                {
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(statusCode, "Test gRPC Exception");
                    return false;
                }

                return true;
            };

            var queue = new PartitionedBlockingCollection<TRequest>(1000, 3);
            queue.TryAdd(GetRequestModel());

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);
            _streamingSvc.StartConsumingCollection(queue);

            NrAssert.Multiple
            (
                () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
                () => Assert.That(actualCountGrpcErrors, Is.EqualTo(expectedCountGrpcErrors), "gRPC Error Count"),
                () => Assert.That(actualCountGeneralErrors, Is.EqualTo(expectedCountGeneralErrors), "General Error Count"),
                () => Assert.That(actualCountSpansSent, Is.EqualTo(expectedCountSpansSent), "Span Sent Events"),
                () => Assert.That(actualDelays, Is.EqualTo(new[] { _expectedDelayAfterErrorSendingASpan, 0, _expectedDelayAfterErrorSendingASpan }).AsCollection, "The expected delay sequence did not match")
            );
        }

        [Test]
        public void GrpcOkDuringConnectIsTreatedAsASuccessfulConnection()
        {
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);

            var actualCountGrpcErrors = 0;
            var actualCountGeneralErrors = 0;
            var actualDelays = new List<int>();

            var expectedCountGrpcErrors = 1;
            var expectedCountGeneralErrors = 1;

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
                .DoInstead<string>((sc) =>
                {
                    actualCountGrpcErrors++;
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
                .DoInstead(() =>
                {
                    actualCountGeneralErrors++;
                });

            Mock.Arrange(() => _delayer.Delay(Arg.IsAny<int>(), Arg.IsAny<CancellationToken>()))
                .DoInstead<int, CancellationToken>((delay, token) =>
                {
                    actualDelays.Add(delay);
                });

            _grpcWrapper.WithCreateChannelImpl = (host, port, ssl, headers, token) =>
            {
                MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.OK, "Test gRPC Exception");
                return false;
            };

            var sourceCollection = new PartitionedBlockingCollection<TRequest>(1000, 3);
            sourceCollection.TryAdd(GetRequestModel());

            var waitForConsumptionTask = Task.Run(() =>
            {
                while (sourceCollection.Count > 0)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(250));
                }
            });

            _streamingSvc.StartConsumingCollection(sourceCollection);

            NrAssert.Multiple
            (
                () => Assert.That(waitForConsumptionTask.Wait(TimeSpan.FromSeconds(10)), Is.True, "Task didn't complete"),
                () => Assert.That(actualCountGrpcErrors, Is.EqualTo(expectedCountGrpcErrors), "gRPC Error Count"),
                () => Assert.That(actualCountGeneralErrors, Is.EqualTo(expectedCountGeneralErrors), "General Error Count"),
                () => Assert.That(actualDelays, Is.Empty, "There should be no delays")
            );
        }

        [Test]
        public void GrpcOkDuringCreateStreamRetriesImmediately()
        {
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);

            var actualDelays = new List<int>();

            Mock.Arrange(() => _delayer.Delay(Arg.IsAny<int>(), Arg.IsAny<CancellationToken>()))
                .DoInstead<int, CancellationToken>((delay, token) =>
                {
                    actualDelays.Add(delay);
                });

            var actualCountGrpcErrors = 0;
            var actualCountGeneralErrors = 0;

            var expectedCountGrpcErrors = 5;
            var expectedCountGeneralErrors = 5;

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcError(Arg.IsAny<string>()))
                .DoInstead<string>((sc) =>
                {
                    actualCountGrpcErrors++;
                });

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanResponseError())
                .DoInstead(() =>
                {
                    actualCountGeneralErrors++;
                });

            var countCreateStreamsCalls = 0;
            var signalIsDone = new ManualResetEventSlim();
            _grpcWrapper.WithCreateStreamsImpl = (metadata, CancellationToken) =>
            {
                countCreateStreamsCalls++;

                if (countCreateStreamsCalls < 4 || countCreateStreamsCalls == 5)
                {
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.Unknown, "Test gRPC Exception");
                    return null;
                }

                if (countCreateStreamsCalls == 4)
                {
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.OK, "Test gRPC Exception");
                    return null;
                }

                signalIsDone.Set();
                return MockGrpcWrapper<TRequestBatch, TResponse>.CreateStreams();
            };

            var sourceCollection = new PartitionedBlockingCollection<TRequest>(1000, 3);

            _streamingSvc.StartConsumingCollection(sourceCollection);

            var expectedDelays = new List<int>();
            expectedDelays.AddRange(_expectedDelaySequenceConnect.Take(3));
            expectedDelays.AddRange(new[] { 0, _expectedDelaySequenceConnect[0] });

            NrAssert.Multiple
            (
                () => Assert.That(signalIsDone.Wait(TimeSpan.FromSeconds(10)), Is.True, "Signal didn't fire"),
                () => Assert.That(actualCountGrpcErrors, Is.EqualTo(expectedCountGrpcErrors), "gRPC Error Count"),
                () => Assert.That(actualCountGeneralErrors, Is.EqualTo(expectedCountGeneralErrors), "General Error Count"),
                () => Assert.That(actualDelays, Is.EqualTo(expectedDelays).AsCollection, "The expected delay sequence did not match")
            );
        }

        [TestCase(-1, false)]
        [TestCase(0, false)]
        [TestCase(10, true)]
        [TestCase(1000, true)]
        public void ConfigSettingsValidTimeoutConnect(int configValue, bool expectedIsConfigValid)
        {
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceTimeoutMsConnect)
                .Returns(configValue);

            var svc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);

            var actualIsConfigValid = svc.ReadAndValidateConfiguration();

            Assert.That(actualIsConfigValid, Is.EqualTo(expectedIsConfigValid), "Is Config Valid");
        }

        [TestCase(-1, false)]
        [TestCase(0, false)]
        [TestCase(10, true)]
        [TestCase(1000, true)]
        public void ConfigSettingsValidTimeoutSendData(int configValue, bool expectedIsConfigValid)
        {
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceTimeoutMsSendData)
                .Returns(configValue);

            var svc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);

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
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingBatchSizeSpans)
                .Returns(configValue);

            var svc = GetService(_delayer, _grpcWrapper, _configSvc, _agentHealthReporter);

            var actualIsConfigValid = svc.ReadAndValidateConfiguration();

            Assert.That(actualIsConfigValid, Is.EqualTo(expectedIsConfigValid), "Is Config Valid");
        }


    }
}
