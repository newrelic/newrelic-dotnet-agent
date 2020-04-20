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

namespace NewRelic.Agent.Core.Spans.Tests
{
	public class MockResponseStream<TResponse> : IAsyncStreamReader<TResponse>
		where TResponse:class
	{
		private BlockingCollection<TResponse> _responses = new BlockingCollection<TResponse>();

		public TResponse Current { get; private set; }

		public void AddResponse(TResponse response)
		{
			_responses.Add(response);
		}

		public Task<bool> MoveNext(CancellationToken cancellationToken)
		{
			if(_responses.TryTake(out var responseItem))
			{
				Current = responseItem;
				return Task.FromResult(true);
			}

			Current = null;
			return Task.FromResult(false);
		}
	}

	public class MockGrpcWrapper<TRequest, TResponse> : IGrpcWrapper<TRequest, TResponse>
		where TResponse:class
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
	
		public void ChangeState(ChannelState newState)
		{
			if(CurrentState != newState)
			{
				CurrentState = newState;
				_channelStateChangeSignal.Set();
			}
		}

		public System.Func<bool> 
			WithIsConnectedImpl { get; set; } = () => true;

		public System.Func<string, int, int, CancellationToken, int, bool>
			WithCreateChannelImpl { get; set; } = (host, port, timeoutMs, cancellationToken, attemptid) => true;

		public System.Func<Metadata, CancellationToken, Tuple<IClientStreamWriter<TRequest>, MockResponseStream<TResponse>>>
			WithCreateStreamsImpl { get; set; } = (metadata, CancellationToken) => 
			{
				return CreateStreams();
			};

		public System.Action 
			WithShutdownImpl { get; set; } = () => { };

		public System.Func<IClientStreamWriter<TRequest>, TRequest, int, CancellationToken, int, bool> WithTrySendDataImpl { get; set; }
			= (requestStream, request, timeoutMs, CancellationToken, attemptId) => true;

		public Action<CancellationToken, IAsyncStreamReader<TResponse>, Action<TResponse>> 
			WithManageResponseStreamImpl { get; set; } = (cancellationToken, responseStream, responseDelegate) =>
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

		public ChannelState? CurrentState { get; private set; }

		public bool CreateChannel(string host, int port, int timeoutMs, CancellationToken cancellationToken, int attemptId)
		{
			var connected = WithCreateChannelImpl?.Invoke(host, port, timeoutMs, cancellationToken, attemptId) ?? false;
			CurrentState = connected ? ChannelState.Ready : ChannelState.Shutdown;
			return connected;
		}

		public IClientStreamWriter<TRequest> CreateStreams(Metadata headers, CancellationToken cancellationToken, Action<TResponse> responseDelegate)
		{
			var streams = WithCreateStreamsImpl?.Invoke(headers, cancellationToken);

			ManageResponseStream(cancellationToken, streams.Item2, responseDelegate);

			return streams.Item1;
		}

		public void Shutdown()
		{
			WithShutdownImpl?.Invoke();
		}

		private AutoResetEvent _channelStateChangeSignal = new AutoResetEvent(false);


		public ChannelState OnChannelStateChanged(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				if (_channelStateChangeSignal.WaitOne(TimeSpan.FromMilliseconds(1000)))
				{
					return CurrentState.GetValueOrDefault(ChannelState.Shutdown);
				}
			}
			
			return ChannelState.Shutdown;
		}

		public void TryCloseRequestStream(IClientStreamWriter<TRequest> requestStream)
		{
		}

		public bool TrySendData(IClientStreamWriter<TRequest> stream, TRequest item, int timeoutWindowMs, CancellationToken cancellationToken, int attemptId)
		{
			return WithTrySendDataImpl(stream, item, timeoutWindowMs, cancellationToken, attemptId);
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

			return config;
		}

		protected override SpanStreamingService GetService(IDelayer delayer, IGrpcWrapper<Span, RecordStatus> grpcWrapper, IConfigurationService configSvc)
		{
			return new SpanStreamingService(grpcWrapper, delayer, configSvc, _agentHealthReporter);
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
		where TResponse: class
	{
		protected static string _validHost = "infiniteTracing.net";
		protected static string _validPort = "443";
		private const int _backoffSendDataMs = 15000;

		public abstract IConfiguration GetDefaultConfiguration();
		protected abstract TService GetService(IDelayer delayer, IGrpcWrapper<TRequest, TResponse> grpcWrapper, IConfigurationService configSvc);
		protected abstract TRequest GetRequestModel();
		protected abstract TResponse GetResponseModel(ulong messagesSeen);

		private MockGrpcWrapper<TRequest, TResponse> _grpcWrapper;
		private IDelayer _delayer;
		private IConfigurationService _configSvc;
		protected IConfiguration _currentConfiguration => _configSvc?.Configuration;
		private TService _streamingSvc;
		protected IAgentHealthReporter _agentHealthReporter;

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
			_grpcWrapper = new MockGrpcWrapper<TRequest, TResponse>();
			_delayer = Mock.Create<IDelayer>();
			_agentHealthReporter = Mock.Create<IAgentHealthReporter>();
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

		[TestCase(false, false, false)][TestCase(false, true, false)]
		[TestCase(true, false, false)][TestCase(true, true, true)]
		public void IsServiceEnabledTests(bool isServiceEnabled, bool isChannelConnected, bool expectedIsServiceAvailable)
		{
			Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceObserverHost).Returns(isServiceEnabled ? _validHost : null as string);
			
			_streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);

			_grpcWrapper.WithIsConnectedImpl = () => isChannelConnected;

			Assert.AreEqual(expectedIsServiceAvailable, _streamingSvc.IsServiceAvailable, $"If IsServiceEnabled={isServiceEnabled} and IsGrpcChannelConnected={isChannelConnected}, IsServiceAvailable should be {expectedIsServiceAvailable}");
		}


		[TestCase(0, true)][TestCase(1, true)][TestCase(2, false)][TestCase(3, true)]
		public void BackoffRetry_SendData_DelaySequence(int succeedOnAttempt, bool throwException)
		{
			_streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);

			var actualDelays = new List<int>();
			var expectedDelays = new List<int>();

			for (var i = 0; i < succeedOnAttempt; i++)
			{
				expectedDelays.Add(_backoffSendDataMs);
			}

			//Arrange
			Mock.Arrange(() => _delayer.Delay(Arg.IsAny<int>(), Arg.IsAny<CancellationToken>()))
				.DoInstead<int, CancellationToken>((delay, token) => { actualDelays.Add(delay); });

			var signalIsDone = new ManualResetEventSlim();

			_grpcWrapper.WithTrySendDataImpl = (stream, request, timeout, token, attemptId) =>
				{
					if (attemptId == succeedOnAttempt)
					{
						signalIsDone.Set();
						return true;
					}

					if (throwException)
					{
						MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException("test Exception");
					}

					return false;
				};

			//Act
			var sourceCollection = new BlockingCollection<TRequest>();
			sourceCollection.Add(GetRequestModel());

			_streamingSvc.StartConsumingCollection(sourceCollection);

			//Assert
			NrAssert.Multiple
			(
				() => Assert.IsTrue(signalIsDone.Wait(TimeSpan.FromSeconds(10)), "Signal didn't fire"),
				() => CollectionAssert.AreEqual(expectedDelays, actualDelays, $"After {succeedOnAttempt} attempt(s), delays should have been {string.Join(",",expectedDelays)}, but were {string.Join(",", actualDelays)}")
			);
		}


		private readonly int[] _expectedDelaySequenceConnect = new[] { 15000, 15000, 30000, 60000, 120000, 300000, 300000, 300000, 300000, 300000, 300000, 300000, 300000, 300000 };

		[TestCase(0, false)][TestCase(1, true)][TestCase(2, false)][TestCase(3, true)]
		[TestCase(4, false)][TestCase(5, true)][TestCase(6, false)][TestCase(7, true)]
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
			_grpcWrapper.WithCreateChannelImpl = (uri, ssl, timeout, token, attempt) =>
				{
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
				() => Assert.IsTrue(signalIsDone.Wait(TimeSpan.FromSeconds(10)),"Signal Didn't Fire"),
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

			_grpcWrapper.WithTrySendDataImpl = (stream, request, timeout, token, attemptId) =>
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
		public void MultpleConsumersItemsSentOnlyOnce()
		{
			Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceCountConsumers).Returns(3);
			_streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);

			var actualItems = new List<TRequest>();
			var expectedItems = new ConcurrentBag<TRequest>();
			for(var i = 0; i < 100; i++)
			{
				expectedItems.Add(GetRequestModel());
			}

			_grpcWrapper.WithTrySendDataImpl = (stream, request, timeout, token, attemptId) =>
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

				Assert.IsTrue(gotResponseReceivedEvent.WaitOne(TimeSpan.FromSeconds(10)),"Trigger Didn't Fire");
				Assert.AreEqual(5, responseMessagesSeen);
			}
		}

		[Test]
		public void SupportabilityMetrics_Errors_OnConnect()
		{

			var expectedCountGrpcErrors = _grpcErrorStatusCodes.ToDictionary(x => EnumNameCache<StatusCode>.GetNameToUpperSnakeCase(x), x => 2);
			var expectedCountGeneralErrors = (_grpcErrorStatusCodes.Length * 2) + 1;

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
			_grpcWrapper.WithCreateChannelImpl = (uri, ssl, timeout, token, attempt) =>
			{
				if (attempt > _grpcErrorStatusCodes.Length * 2)
				{
					signalIsDone.Set();
					return true;
				}


				if (attempt == _grpcErrorStatusCodes.Length * 2)
				{
					MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException("Test General Exception");
					return false;
				}

				var statusCodeToThrow = _grpcErrorStatusCodes[attempt % _grpcErrorStatusCodes.Length];

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

			Mock.Arrange(() => _agentHealthReporter.ReportInfiniteTracingSpanGrpcTimeout(Arg.IsAny<int>()))
				.DoInstead<int>((attemptId) => actualCountTimeouts++);

			_grpcWrapper.WithTrySendDataImpl = (stream, item, timeoutMs, token, attempt) =>
			{
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
				.Where(x=>x != StatusCode.Unimplemented)
				.ToDictionary(x => EnumNameCache<StatusCode>.GetNameToUpperSnakeCase(x), x => 2);

			var testGrpcErrorCodes = _grpcErrorStatusCodes
				.Where(x => x != StatusCode.Unimplemented)
				.ToArray();


			var expectedCountGeneralErrors = (testGrpcErrorCodes.Length * 2)    //Each status error thrown 2x
											+ 1;								//General Error by itself

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
			_grpcWrapper.WithTrySendDataImpl = (stream, item, timeoutMs, token, attempt ) =>
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
		[TestCase("infiniteTracing.com","443",			true,  443)]
		[TestCase("infiniteTracing.com", null,			true,  443)]
		[TestCase("infiniteTracing.com", "",			true,  443)]
		[TestCase("infiniteTracing.com", "abc",			false, -1)]
		[TestCase("my8Tdomain", "-1",					false, -1)]
		[TestCase("my8Tdomain", "80",					true,  80)]
		[TestCase("my8Tdomain", "20",					true,  20)]
		[TestCase("my8Tdomain", "8080",					true,  8080)]

		//No host means service always disabled
		[TestCase(null, "443",							false, -1)]
		[TestCase(null, "",								false, -1)]
		[TestCase(null, null,							false, -1)]
		[TestCase(null, "abc",							false, -1)]		//??

		//No host means service disabled
		[TestCase("", "443",							false, -1)]
		[TestCase("", "",								false, -1)]
		[TestCase("", null,								false, -1)]
		[TestCase("", "abc",							false, -1)]

		//Hosts should not have scheme or port
		[TestCase("http://infiniteTracing.com", "443",	false, -1)]
		[TestCase("http://infiniteTracing.com", null,	false, -1)]
		[TestCase("http://infiniteTracing.com", "",		false, -1)]
		[TestCase("http://infiniteTracing.com", "abc",	false, -1)]
		[TestCase("infiniteTracing.com:443", "443",		false, -1)]
		[TestCase("infiniteTracing.com:443", null,		false, -1)]

		//Various invalid hosts
		[TestCase("/relativeUrl", null,					false, -1)]
		[TestCase("//mydomain", null,					false, -1)]
		[TestCase("//mydomain/", null,					false, -1)]
		[TestCase("https:///", null,					false, -1)]
		[TestCase("my8Tdomain/some/path",null,			false, -1)]
		public void MalformedUriPreventsStartRecordSupportabilityMetric(string testHost, string testPort, bool expectedIsValidConfig, int expectedPort)
		{
			Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceObserverHost).Returns(testHost);
			Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceObserverPort).Returns(testPort);
		
			var streamingSvc = GetService(_delayer, _grpcWrapper, _configSvc);
			var actualIsValidConfig = streamingSvc.ReadAndValidateConfiguration();
			var actualHost = streamingSvc.EndpointHost;
			var actualPort = streamingSvc.EndpointPort;


			NrAssert.Multiple
			(
				() => Assert.AreEqual(expectedIsValidConfig, actualIsValidConfig, "Configuration is valid")
			);

			if (!expectedIsValidConfig)
			{
				NrAssert.Multiple
				(
					() => Assert.IsNull(actualHost, "Invalid config shouldn't have host"),
					() => Assert.AreEqual(-1, actualPort, "Invalid config should have port -1")
				);
			}
			else
			{
				NrAssert.Multiple
				(
					() => Assert.AreEqual(testHost, actualHost, "Host"),
					() => Assert.AreEqual(expectedPort, actualPort, "Port")
				);
			}
		}

		[Test]
		public void GrpcUnimplementedShutsDownService()
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
			_grpcWrapper.WithTrySendDataImpl = (stream, item, timeoutMs, token, attempt) =>
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
		public void GrpcServerStreamCloseRetryWithoutDelay()
		{
			var invocationId = 0;

			var actualDelays = new List<int>();
			var expectedDelays = new List<int>(new[] { _backoffSendDataMs });
			var actualCountCreateStreams = 0;
			
			var signalIsDone = new ManualResetEventSlim();

			Mock.Arrange(() => _currentConfiguration.InfiniteTracingTraceCountConsumers).Returns(1);

			Mock.Arrange(() => _delayer.Delay(Arg.IsAny<int>(), Arg.IsAny<CancellationToken>()))
				.DoInstead<int, CancellationToken>((delay, token) =>
				{
					actualDelays.Add(delay);
				});


			_grpcWrapper.WithTrySendDataImpl = (requestStream, request, timeoutMs, CancellationToken, attemptId) =>
			{
				invocationId++;
				if (invocationId == 1)
				{
					MockGrpcWrapper <TRequest, TResponse>.ThrowGrpcWrapperStreamsNotAvailableException("Test Request stream has already been completed.");
				}

				if (invocationId == 2)
				{
					MockGrpcWrapper<TRequest, TResponse>.ThrowGrpcWrapperException("Random Test Exception");
				}

				signalIsDone.Set();
				return true;
			};

			_grpcWrapper.WithCreateStreamsImpl = (metadata, token) =>
			{
				actualCountCreateStreams++;
				return MockGrpcWrapper<TRequest, TResponse>.CreateStreams();
			};

			var svc = GetService(_delayer, _grpcWrapper, _configSvc);

			var collection = new BlockingCollection<TRequest>();
			collection.Add(GetRequestModel());

			svc.StartConsumingCollection(collection);

			NrAssert.Multiple
			(
				() => Assert.IsTrue(signalIsDone.Wait(TimeSpan.FromSeconds(10)), "Signal didn't fire"),
				() => CollectionAssert.AreEqual(actualDelays, expectedDelays, "There should be 1 delay representing the error that is not stream closed"),
				() => Assert.AreEqual(3, actualCountCreateStreams, "Create Streams (1-first try, 1-retry, 1-retry")
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
