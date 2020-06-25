/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Segments;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Telerik.JustMock;
using Grpc.Core;
using System.Threading;
using NewRelic.Testing.Assertions;
using System;
using System.Collections.Concurrent;
using NewRelic.Agent.Configuration;
using System.Threading.Tasks;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Core.Utilities;

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
            if (_responses.TryTake(out var responseItem))
            {
                Current = responseItem;
                return Task.FromResult(true);
            }

            Current = null;
            return Task.FromResult(false);
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

        public System.Func<bool>
            WithIsConnectedImpl
        { get; set; } = () => true;

        public System.Func<string, int, bool, Metadata, CancellationToken, bool>
            WithCreateChannelImpl
        { get; set; } = (host, port, ssl, headers, cancellationToken) => true;

        public System.Func<Metadata, CancellationToken, Tuple<IClientStreamWriter<TRequest>, MockResponseStream<TResponse>>>
            WithCreateStreamsImpl
        { get; set; } = (metadata, CancellationToken) =>
{
    return CreateStreams();
};

        public System.Action
            WithShutdownImpl
        { get; set; } = () => { };

        public System.Func<IClientStreamWriter<TRequest>, TRequest, int, CancellationToken, bool> WithTrySendDataImpl { get; set; }
            = (requestStream, request, timeoutMs, CancellationToken) => true;

        public Action<CancellationToken, IAsyncStreamReader<TResponse>, Action<TResponse>>
            WithManageResponseStreamImpl
        { get; set; } = (cancellationToken, responseStream, responseDelegate) =>
{
    while (responseStream.MoveNext(cancellationToken).Result)
    {
        var response = responseStream.Current;

        if (response != null)
        {
            responseDelegate(response);
        }
    }
};

        public bool IsConnected => WithIsConnectedImpl?.Invoke() ?? false;

        public bool CreateChannel(string host, int port, bool ssl, Metadata headers, CancellationToken cancellationToken)
        {
            var connected = WithCreateChannelImpl?.Invoke(host, port, ssl, headers, cancellationToken) ?? false;
            return connected;
        }

        public IClientStreamWriter<TRequest> CreateStreams(Metadata headers, CancellationToken cancellationToken, Action<TResponse> responseDelegate)
        {
            var streams = WithCreateStreamsImpl?.Invoke(headers, cancellationToken);

            if (streams == null)
            {
                return null;
            }

            ManageResponseStream(cancellationToken, streams.Item2, responseDelegate);

            return streams.Item1;
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

        public void ManageResponseStream(CancellationToken cancellationToken, IAsyncStreamReader<TResponse> responseStream, Action<TResponse> responseDelegate)
        {
            try
            {
                while (responseStream.MoveNext(cancellationToken).Result)
                {
                    var response = responseStream.Current;

                    if (response != null)
                    {
                        responseDelegate(response);
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }

    internal class SpanStreamingServiceTests : DataStreamingServiceTests<SpanStreamingService, Span, RecordStatus>
    {
        public override IConfiguration GetDefaultConfiguration()
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

            return config;
        }

        protected override SpanStreamingService GetService(IDelayer delayer, IGrpcWrapper<Span, RecordStatus> grpcWrapper, IConfigurationService configSvc)
        {
            return new SpanStreamingService(grpcWrapper, delayer, configSvc, _agentHealthReporter, _agentTimerService);
        }

        protected override Span GetRequestModel()
        {
            return new Span();
        }

        protected override RecordStatus GetResponseModel(ulong messagesSeen)
        {
            return new RecordStatus { MessagesSeen = messagesSeen };
        }
    }

    [TestFixture]
    internal abstract class DataStreamingServiceTests<TService, TRequest, TResponse>
        where TService : IDataStreamingService<TRequest, TResponse>
        where TRequest : class, IStreamingModel
        where TResponse : class
    {
        protected const string _validHost = "infiniteTracing.net";
        protected const string _validPort = "443";
        protected const string DefaultLicenseKey = "defaultlicensekey";
        protected const string DefaultAgentRunToken = "defaultagentruntoken";

        public abstract IConfiguration GetDefaultConfiguration();
        protected abstract TService GetService(IDelayer delayer, IGrpcWrapper<TRequest, TResponse> grpcWrapper, IConfigurationService configSvc);
        protected abstract TRequest GetRequestModel();
        protected abstract TResponse GetResponseModel(ulong messagesSeen);

        protected float? TestFlakyValue;
        protected int? TestDelayValue;
        protected Dictionary<string, string> TestRequestHeadersMap;

        private MockGrpcWrapper<TRequest, TResponse> _grpcWrapper;
        private IDelayer _delayer;
        private IConfigurationService _configSvc;
        protected IConfiguration _currentConfiguration => _configSvc?.Configuration;
        private TService _streamingSvc;
        protected IAgentHealthReporter _agentHealthReporter;
        protected IAgentTimerService _agentTimerService;

        private StatusCode[] _grpcErrorStatusCodes;

        public DataStreamingServiceTests()
        {
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

            _grpcWrapper = new MockGrpcWrapper<TRequest, TResponse>();
            _delayer = Mock.Create<IDelayer>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            _agentTimerService = Mock.Create<IAgentTimerService>();
            _configSvc = Mock.Create<IConfigurationService>();

            var defaultConfig = GetDefaultConfiguration();
            Mock.Arrange(() => _configSvc.Configuration).Returns(defaultConfig);
        }

        [TearDown]
        public void Teardown()
        {
            _streamingSvc?.Shutdown();
            _agentHealthReporter = null;
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void IsServiceEnabledTests(bool isServiceEnabled, bool isChannelConnected, bool expectedIsServiceAvailable)
        {
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceObserverHost).Returns(isServiceEnabled ? _validHost : null as string);

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);

            _grpcWrapper.WithIsConnectedImpl = () => isChannelConnected;

            Assert.AreEqual(expectedIsServiceAvailable, _streamingSvc.IsServiceAvailable, $"If IsServiceEnabled={isServiceEnabled} and IsGrpcChannelConnected={isChannelConnected}, IsServiceAvailable should be {expectedIsServiceAvailable}");
        }

        [TestCase(false, false, false)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        [TestCase(true, true, true)]
        public void ShouldHaveCorrectConnectionMetadata(bool includeFlaky, bool includeDelay, bool includeRequestHeadersMap)
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

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);

            Dictionary<string,string> actualConnectionMetadata = new Dictionary<string, string>();
            _grpcWrapper.WithCreateChannelImpl = (host, port, ssl, headers, token) =>
            {
                actualConnectionMetadata = headers.ToDictionary(k => k.Key, v => v.Value);

                //Throw the unimplemented exception so that the service will shutdown
                MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.Unimplemented, "Test gRPC Exception");
                return false;
            };

            var countShutdowns = 0;
            var signalIsDone = new ManualResetEventSlim();
            // When starting the service, a shutdown is issued as a means to restart.
            // When starting the channel, this also occurs.
            // Ignore these first two shutdowns in our determination of svc.stop
            _grpcWrapper.WithShutdownImpl = () =>
            {
                countShutdowns++;

                if (countShutdowns > 2)
                {
                    signalIsDone.Set();
                }
            };

            var sourceCollection = new BlockingCollection<TRequest>();

            _streamingSvc.StartConsumingCollection(sourceCollection);

            NrAssert.Multiple
            (
                () => Assert.IsTrue(signalIsDone.Wait(TimeSpan.FromSeconds(10)), "Signal didn't fire"),
                () => CollectionAssert.AreEquivalent(expectedMetadata, actualConnectionMetadata, "connection metadata did not match")
            );
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
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);

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

            var sourceCollection = new BlockingCollection<TRequest>();

            _streamingSvc.StartConsumingCollection(sourceCollection);

            NrAssert.Multiple
            (
                () => Assert.IsTrue(signalIsDone.Wait(TimeSpan.FromSeconds(10)), "Signal Didn't Fire"),
                () => Assert.GreaterOrEqual(_expectedDelaySequenceConnect.Count(), succeedOnAttempt),
                () => CollectionAssert.AreEqual(expectedDelays, actualDelays, $"After {succeedOnAttempt} attempt(s), delays should have been {string.Join(",", expectedDelays)}, but were {string.Join(",", actualDelays)}")
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
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);

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
                    return MockGrpcWrapper<TRequest, TResponse>.CreateStreams();
                }

                if (throwExceptionDuringCreateStream)
                {
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.Unknown, "Test gRPC Exception");
                }

                return null;
            };

            var sourceCollection = new BlockingCollection<TRequest>();

            _streamingSvc.StartConsumingCollection(sourceCollection);

            NrAssert.Multiple
            (
                () => Assert.IsTrue(signalIsDone.Wait(TimeSpan.FromSeconds(10)), "Signal Didn't Fire"),
                () => Assert.GreaterOrEqual(_expectedDelaySequenceConnect.Count(), succeedOnAttempt),
                () => CollectionAssert.AreEqual(expectedDelays, actualDelays, $"After {succeedOnAttempt} attempt(s), delays should have been {string.Join(",", expectedDelays)}, but were {string.Join(",", actualDelays)}")
            );
        }

        [Test]
        public void FailedItemsReturnedToQueue()
        {
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);

            var actualAttempts = new List<TRequest>();
            var haveProcessedFailure = false;

            var item1 = GetRequestModel();
            var item2 = GetRequestModel();
            var item3 = GetRequestModel();
            var item4 = GetRequestModel();
            var item5 = GetRequestModel();

            _grpcWrapper.WithTrySendDataImpl = (stream, request, timeout, token) =>
                {
                    actualAttempts.Add(request);

                    if (request == item3 && !haveProcessedFailure)
                    {
                        haveProcessedFailure = true;
                        return false;
                    }

                    return true;
                };

            var queue = new ConcurrentQueue<TRequest>();
            var sourceCollection = new BlockingCollection<TRequest>(queue);

            sourceCollection.Add(item1);
            sourceCollection.Add(item2);
            sourceCollection.Add(item3);
            sourceCollection.Add(item4);
            sourceCollection.Add(item5);

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
                () => Assert.IsTrue(waitForConsumptionTask.Wait(TimeSpan.FromSeconds(10)), "Task didn't complete"),
                () => CollectionAssert.AreEqual(expectedAttempts, actualAttempts)
            );
        }

        [Test]
        public void DelayCallingRecordSpanAfterAnErrorStreamingASpan()
        {
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);

            var actualDelays = new List<int>();

            Mock.Arrange(() => _delayer.Delay(Arg.IsAny<int>(), Arg.IsAny<CancellationToken>()))
                .DoInstead<int, CancellationToken>((delay, token) =>
                {
                    actualDelays.Add(delay);
                });

            var actualAttempts = new List<TRequest>();
            var haveProcessedFailure = false;

            var item1 = GetRequestModel();

            _grpcWrapper.WithTrySendDataImpl = (stream, request, timeout, token) =>
            {
                actualAttempts.Add(request);

                if (request == item1 && !haveProcessedFailure)
                {
                    haveProcessedFailure = true;
                    return false;
                }

                return true;
            };

            var queue = new ConcurrentQueue<TRequest>();
            var sourceCollection = new BlockingCollection<TRequest>(queue);

            sourceCollection.Add(item1);

            var waitForConsumptionTask = Task.Run(() =>
            {
                while (sourceCollection.Count > 0)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(250));
                }
            });

            _streamingSvc.StartConsumingCollection(sourceCollection);

            var expectedAttempts = new[] { item1, item1 };

            NrAssert.Multiple
            (
                () => Assert.IsTrue(waitForConsumptionTask.Wait(TimeSpan.FromSeconds(10)), "Task didn't complete"),
                () => CollectionAssert.AreEqual(expectedAttempts, actualAttempts),
                () => CollectionAssert.AreEqual(new[] { _expectedDelayAfterErrorSendingASpan }, actualDelays)
            );
        }

        [Test]
        public void MultpleConsumersItemsSentOnlyOnce()
        {
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceCountConsumers).Returns(3);
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);

            var actualItems = new List<TRequest>();
            var expectedItems = new ConcurrentBag<TRequest>();
            for (var i = 0; i < 100; i++)
            {
                expectedItems.Add(GetRequestModel());
            }

            _grpcWrapper.WithTrySendDataImpl = (stream, request, timeout, token) =>
                {
                    actualItems.Add(request);
                    return true;
                };

            var sourceCollection = new BlockingCollection<TRequest>(new ConcurrentQueue<TRequest>(expectedItems));

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
                () => Assert.IsTrue(waitForConsumptionTask.Wait(TimeSpan.FromSeconds(10)), "Task didn't complete"),
                () => CollectionAssert.AreEquivalent(expectedItems, actualItems.ToList())
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
                return MockGrpcWrapper<TRequest, TResponse>.CreateStreams();
            };


            var queue = new BlockingCollection<TRequest>();
            queue.Add(GetRequestModel());

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);
            _streamingSvc.StartConsumingCollection(queue);

            NrAssert.Multiple
            (
                () => Assert.IsTrue(signalIsDone.Wait(TimeSpan.FromSeconds(10)), "Signal didn't fire"),
                () => Assert.AreEqual(2, streamCancellationTokens.Count, "Did not see enough streams created"),
                () => Assert.IsTrue(streamCancellationTokens[0].IsCancellationRequested, "The first stream cancellation token was not triggered."),
                () => Assert.IsFalse(streamCancellationTokens[1].IsCancellationRequested, "The second stream cancellation token was triggered.")
            );
        }

        [Test]
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
                    signalIsDone.Set();
                    _streamingSvc.Shutdown();
                    return true;
                }

                return false;
            };

            _grpcWrapper.WithCreateStreamsImpl = (metadata, cancellationToken) =>
            {
                streamCancellationTokens.Add(cancellationToken);
                return MockGrpcWrapper<TRequest, TResponse>.CreateStreams();
            };


            var queue = new BlockingCollection<TRequest>();
            queue.Add(GetRequestModel());

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);
            _streamingSvc.StartConsumingCollection(queue);

            NrAssert.Multiple
            (
                () => Assert.IsTrue(signalIsDone.Wait(TimeSpan.FromSeconds(10)), "Signal didn't fire"),
                () => Assert.AreEqual(2, streamCancellationTokens.Count, "Did not see enough streams created"),
                () => Assert.IsTrue(streamCancellationTokens[0].IsCancellationRequested, "The first stream cancellation token was not triggered."),
                () => Assert.IsTrue(streamCancellationTokens[1].IsCancellationRequested, "The second stream cancellation token was not triggered.")
            );
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
                    var streams = MockGrpcWrapper<TRequest, TResponse>.CreateStreams();
                    streams.Item2.AddResponse(GetResponseModel(5));

                    return streams;
                };

                Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanEventsReceived(Arg.IsAny<ulong>()))
                    .DoInstead<ulong>(count =>
                    {
                        responseMessagesSeen += (long)count;
                        gotResponseReceivedEvent.Set();
                    });

                _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);

                _streamingSvc.StartConsumingCollection(new BlockingCollection<TRequest>());

                Assert.IsTrue(gotResponseReceivedEvent.WaitOne(TimeSpan.FromSeconds(10)), "Trigger Didn't Fire");
                Assert.AreEqual(5, responseMessagesSeen);
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

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);
            _streamingSvc.StartConsumingCollection(new BlockingCollection<TRequest>());


            NrAssert.Multiple
            (
                () => Assert.IsTrue(signalIsDone.Wait(TimeSpan.FromSeconds(10)), "Signal didn't fire"),
                () => CollectionAssert.AreEqual(expectedCountGrpcErrors, actualCountGrpcErrors, "gRPC Error Count"),
                () => Assert.AreEqual(expectedCountGeneralErrors, actualCountGeneralErrors, "General Error Count")
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


            var queue = new BlockingCollection<TRequest>();
            queue.Add(GetRequestModel());

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);
            _streamingSvc.StartConsumingCollection(queue);

            NrAssert.Multiple
            (
                () => Assert.IsTrue(signalIsDone.Wait(TimeSpan.FromSeconds(10)), "Signal didn't fire"),
                () => Assert.AreEqual(1, actualCountTimeouts, "gRPC Timeout Count")
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

            var queue = new BlockingCollection<TRequest>();
            queue.Add(GetRequestModel());

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);
            _streamingSvc.StartConsumingCollection(queue);

            NrAssert.Multiple
            (
                () => Assert.IsTrue(signalIsDone.Wait(TimeSpan.FromSeconds(10)), "Signal didn't fire"),
                () => CollectionAssert.AreEqual(expectedCountGrpcErrors, actualCountGrpcErrors, "gRPC Error Count"),
                () => Assert.AreEqual(expectedCountGeneralErrors, actualCountGeneralErrors, "General Error Count")
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

            var streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);
            var actualIsValidConfig = streamingSvc.ReadAndValidateConfiguration();
            var actualHost = streamingSvc.EndpointHost;
            var actualPort = streamingSvc.EndpointPort;
            var actualSsl = streamingSvc.EndpointSsl;


            NrAssert.Multiple
            (
                () => Assert.AreEqual(expectedIsValidConfig, actualIsValidConfig, "Configuration is valid")
            );

            if (!expectedIsValidConfig)
            {
                NrAssert.Multiple
                (
                    () => Assert.IsNull(actualHost, "Invalid config shouldn't have host"),
                    () => Assert.AreEqual(-1, actualPort, "Invalid config should have port -1"),
                    () => Assert.AreEqual(true, actualSsl, "Invalid config should have SSL true")
                );
            }
            else
            {
                NrAssert.Multiple
                (
                    () => Assert.AreEqual(testHost, actualHost, "Host"),
                    () => Assert.AreEqual(expectedPort, actualPort, "Port"),
                    () => Assert.AreEqual(expectedSsl, actualSsl, "SSL")
                );
            }
        }

        [Test]
        public void GrpcUnimplementedDuringTrySendDataShutsDownService()
        {
            var actualCountGrpcErrors = 0;
            var actualCountGeneralErrors = 0;
            var actualCountSpansSent = 0;

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

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanEventsSent())
                .DoInstead(() =>
                {
                    actualCountSpansSent++;
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

            // When starting the service, a shutdown is issued as a means to restart.
            // When starting the channel, this also occurs.
            // Ignore these first two shutdowns in our determination of svc.stop
            _grpcWrapper.WithShutdownImpl = () =>
            {
                countShutdowns++;

                if (countShutdowns > 2)
                {
                    signalIsDone.Set();
                }
            };

            var queue = new BlockingCollection<TRequest>();
            queue.Add(GetRequestModel());
            queue.Add(GetRequestModel());
            queue.Add(GetRequestModel());
            queue.Add(GetRequestModel());
            queue.Add(GetRequestModel());

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);
            _streamingSvc.StartConsumingCollection(queue);

            NrAssert.Multiple
            (
                () => Assert.IsTrue(signalIsDone.Wait(TimeSpan.FromSeconds(10)), "Signal didn't fire"),
                () => Assert.AreEqual(expectedCountGrpcErrors, actualCountGrpcErrors, "gRPC Error Count"),
                () => Assert.AreEqual(expectedCountGeneralErrors, actualCountGeneralErrors, "General Error Count"),
                () => Assert.AreEqual(expectedCountSpansSent, actualCountSpansSent, "Span Sent Events")
            );
        }

        [Test]
        public void GrpcUnimplementedDuringConnectShutsDownService()
        {
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);

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
            // When starting the service, a shutdown is issued as a means to restart.
            // When starting the channel, this also occurs.
            // Ignore these first two shutdowns in our determination of svc.stop
            _grpcWrapper.WithShutdownImpl = () =>
            {
                countShutdowns++;

                if (countShutdowns > 2)
                {
                    signalIsDone.Set();
                }
            };

            var sourceCollection = new BlockingCollection<TRequest>();

            _streamingSvc.StartConsumingCollection(sourceCollection);

            NrAssert.Multiple
            (
                () => Assert.IsTrue(signalIsDone.Wait(TimeSpan.FromSeconds(10)), "Signal didn't fire"),
                () => Assert.AreEqual(expectedCountGrpcErrors, actualCountGrpcErrors, "gRPC Error Count"),
                () => Assert.AreEqual(expectedCountGeneralErrors, actualCountGeneralErrors, "General Error Count")
            );
        }

        [Test]
        public void GrpcUnimplementedDuringCreateStreamShutsDownService()
        {
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);

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
            // When starting the service, a shutdown is issued as a means to restart.
            // When starting the channel, this also occurs.
            // Ignore these first two shutdowns in our determination of svc.stop
            _grpcWrapper.WithShutdownImpl = () =>
            {
                countShutdowns++;

                if (countShutdowns > 2)
                {
                    signalIsDone.Set();
                }
            };

            var sourceCollection = new BlockingCollection<TRequest>();

            _streamingSvc.StartConsumingCollection(sourceCollection);

            NrAssert.Multiple
            (
                () => Assert.IsTrue(signalIsDone.Wait(TimeSpan.FromSeconds(10)), "Signal didn't fire"),
                () => Assert.AreEqual(expectedCountGrpcErrors, actualCountGrpcErrors, "gRPC Error Count"),
                () => Assert.AreEqual(expectedCountGeneralErrors, actualCountGeneralErrors, "General Error Count")
            );
        }

        [Test]
        public void GrpcOkDuringTrySendDataCreatesNewStreamImmediately()
        {
            var actualCountGrpcErrors = 0;
            var actualCountGeneralErrors = 0;
            var actualCountSpansSent = 0;

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

            Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanEventsSent())
                .DoInstead(() =>
                {
                    actualCountSpansSent++;
                    signalIsDone.Set();
                });


            var invocationId = 0;
            _grpcWrapper.WithTrySendDataImpl = (stream, item, timeoutMs, token) =>
            {
                var localInvocationId = Interlocked.Increment(ref invocationId);

                if (localInvocationId < 2 || localInvocationId == 3)
                {
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.Internal, "Test gRPC Exception");
                    return false;
                }

                if (localInvocationId == 2)
                {
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.OK, "Test gRPC Exception");
                    return false;
                }

                return true;
            };

            var queue = new BlockingCollection<TRequest>();
            queue.Add(GetRequestModel());

            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);
            _streamingSvc.StartConsumingCollection(queue);

            NrAssert.Multiple
            (
                () => Assert.IsTrue(signalIsDone.Wait(TimeSpan.FromSeconds(10)), "Signal didn't fire"),
                () => Assert.AreEqual(expectedCountGrpcErrors, actualCountGrpcErrors, "gRPC Error Count"),
                () => Assert.AreEqual(expectedCountGeneralErrors, actualCountGeneralErrors, "General Error Count"),
                () => Assert.AreEqual(expectedCountSpansSent, actualCountSpansSent, "Span Sent Events"),
                () => CollectionAssert.AreEqual(new[] { _expectedDelayAfterErrorSendingASpan, 0, _expectedDelayAfterErrorSendingASpan }, actualDelays, "The expected delay sequence did not match")
            );
        }

        [Test]
        public void GrpcOkDuringConnectIsTreatedAsASuccessfulConnection()
        {
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);

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
                MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.OK, "Test gRPC Exception");
                return false;
            };

            var sourceCollection = new BlockingCollection<TRequest>();
            sourceCollection.Add(GetRequestModel());

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
                () => Assert.IsTrue(waitForConsumptionTask.Wait(TimeSpan.FromSeconds(10)), "Task didn't complete"),
                () => Assert.AreEqual(expectedCountGrpcErrors, actualCountGrpcErrors, "gRPC Error Count"),
                () => Assert.AreEqual(expectedCountGeneralErrors, actualCountGeneralErrors, "General Error Count")
            );
        }

        [Test]
        public void GrpcOkDuringCreateStreamRetriesImmediately()
        {
            _streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);

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
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.Internal, "Test gRPC Exception");
                    return null;
                }

                if (countCreateStreamsCalls == 4)
                {
                    MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException(StatusCode.OK, "Test gRPC Exception");
                    return null;
                }

                signalIsDone.Set();
                return MockGrpcWrapper<TRequest, TResponse>.CreateStreams();
            };

            var sourceCollection = new BlockingCollection<TRequest>();

            _streamingSvc.StartConsumingCollection(sourceCollection);

            var expectedDelays = new List<int>();
            expectedDelays.AddRange(_expectedDelaySequenceConnect.Take(3));
            expectedDelays.AddRange(new[] { 0, _expectedDelaySequenceConnect[0] });

            NrAssert.Multiple
            (
                () => Assert.IsTrue(signalIsDone.Wait(TimeSpan.FromSeconds(10)), "Signal didn't fire"),
                () => Assert.AreEqual(expectedCountGrpcErrors, actualCountGrpcErrors, "gRPC Error Count"),
                () => Assert.AreEqual(expectedCountGeneralErrors, actualCountGeneralErrors, "General Error Count"),
                () => CollectionAssert.AreEqual(expectedDelays, actualDelays, "The expected delay sequence did not match")
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

            var svc = GetService(_delayer, _grpcWrapper, _configSvc);

            var actualIsConfigValid = svc.ReadAndValidateConfiguration();

            Assert.AreEqual(expectedIsConfigValid, actualIsConfigValid, "Is Config Valid");
        }

        [TestCase(-1, false)]
        [TestCase(0, false)]
        [TestCase(10, true)]
        [TestCase(1000, true)]
        public void ConfigSettingsValidTimeoutSendData(int configValue, bool expectedIsConfigValid)
        {
            Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceTimeoutMsSendData)
                .Returns(configValue);

            var svc = GetService(_delayer, _grpcWrapper, _configSvc);

            var actualIsConfigValid = svc.ReadAndValidateConfiguration();

            Assert.AreEqual(expectedIsConfigValid, actualIsConfigValid, "Is Config Valid");
        }
    }
}
