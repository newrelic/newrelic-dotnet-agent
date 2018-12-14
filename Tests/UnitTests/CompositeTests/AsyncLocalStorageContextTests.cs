using System;
using System.Linq;
using System.Threading;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace CompositeTests
{
	[TestFixture]
	public class AsyncLocalStorageContextTests
	{
		private static CompositeTestAgent _compositeTestAgent;
		private IAgentWrapperApi _agentWrapperApi;
		
		[SetUp]
		public void SetUp()
		{
			_compositeTestAgent = new CompositeTestAgent(false, true);
			_agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();
		}

		[TearDown]
		public static void TearDown()
		{
			_compositeTestAgent.Dispose();
		}

		[Test]
		public void SimpleTransaction_EndedMultipleTimes()
		{
			_compositeTestAgent.LocalConfiguration.service.completeTransactionsOnThread = true;
			_compositeTestAgent.PushConfiguration();

			var doBackgroundJob = new AutoResetEvent(false);
			var completedForegroundExternal = new AutoResetEvent(false);
			var completedBackgroundExternal = new AutoResetEvent(false);

			bool? transactionFlowedToBackgroundThread = null;

			InstrumentationThatStartsATransaction();
			HttpClientInstrumentation("foregroundExternal", completedForegroundExternal);
			System.Threading.Tasks.Task.Run((Action)BackgroundJob);

			completedForegroundExternal.WaitOne();

			InstrumentationThatEndsTheTransaction();

			doBackgroundJob.Set();

			completedBackgroundExternal.WaitOne();
			_compositeTestAgent.Harvest();

			var transactionEvents = _compositeTestAgent.TransactionEvents;
			var metrics = _compositeTestAgent.Metrics;
			var errors = _compositeTestAgent.ErrorEvents;

			Assert.AreEqual(true, transactionFlowedToBackgroundThread);
			Assert.AreEqual(1, transactionEvents.Count);
			Assert.AreEqual("foregroundExternal", transactionEvents.First().AgentAttributes["request.uri"]);
			CollectionAssert.IsEmpty(errors);
			CollectionAssert.IsEmpty(metrics.Where(x => x.MetricName.Name.Contains("backgroundExternal")));
			CollectionAssert.IsNotEmpty(metrics.Where(x => x.MetricName.Name.Contains("foregroundExternal")));

			void InstrumentationThatStartsATransaction()
			{
				_agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
				_agentWrapperApi.CurrentTransactionWrapperApi.AttachToAsync();
				_agentWrapperApi.CurrentTransactionWrapperApi.DetachFromPrimary();
			}

			void InstrumentationThatEndsTheTransaction()
			{
				_agentWrapperApi.CurrentTransactionWrapperApi.End();
			}

			void BackgroundJob()
			{
				transactionFlowedToBackgroundThread = _agentWrapperApi.CurrentTransactionWrapperApi.IsValid;
				doBackgroundJob.WaitOne();
				HttpClientInstrumentation("backgroundExternal", completedBackgroundExternal);
				_agentWrapperApi.CurrentTransactionWrapperApi.NoticeError(new Exception());
			};

			System.Threading.Tasks.Task HttpClientInstrumentation(string segmentName, AutoResetEvent autoResetEvent)
			{
				var transactionWrapperApi = _agentWrapperApi.CurrentTransactionWrapperApi;

				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow(segmentName);

				_agentWrapperApi.CurrentTransactionWrapperApi.SetUri(segmentName);

				segment.RemoveSegmentFromCallStack();
				transactionWrapperApi.Hold();

				return System.Threading.Tasks.Task.Delay(1000).ContinueWith(task =>
				{
					segment.End();
					transactionWrapperApi.Release();
					autoResetEvent.Set();
				});
			};
		}
	}
}
