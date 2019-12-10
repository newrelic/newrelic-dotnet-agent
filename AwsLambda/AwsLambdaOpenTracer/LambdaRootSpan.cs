using NewRelic.OpenTracing.AmazonLambda.State;
using NewRelic.OpenTracing.AmazonLambda.Util;
using System;
using System.Collections.Generic;

namespace NewRelic.OpenTracing.AmazonLambda
{
	internal class LambdaRootSpan : LambdaSpan
	{
		public LambdaRootSpan(string operationName, DateTimeOffset timestamp, IDictionary<string, object> tags, string guid, DataCollector dataCollector, TransactionState transactionState, PrioritySamplingState prioritySamplingState, DistributedTracingState distributedTracingState) : base(operationName, timestamp, tags, parentSpan:null, guid)
		{
			Collector = dataCollector;
			TransactionState = transactionState;
			PrioritySamplingState = prioritySamplingState;
			DistributedTracingState = distributedTracingState;
		}

		public DataCollector Collector { get; }
		public TransactionState TransactionState { get; }
		public PrioritySamplingState PrioritySamplingState { get; }
		public DistributedTracingState DistributedTracingState { get; }

		public void ApplyLambdaPayloadContext(LambdaPayloadContext payloadContext)
		{
			DistributedTracingState.SetInboundDistributedTracePayload(payloadContext.GetPayload());
			DistributedTracingState.SetTransportDurationInMillis(payloadContext.GetTransportDurationInMillis());
			PrioritySamplingState.Priority = (DistributedTracingState.InboundPayload.Priority.HasValue)
														? DistributedTracingState.InboundPayload.Priority.Value
														: LambdaTracer.TracePriorityManager.Create();

			if (DistributedTracingState.InboundPayload.Sampled.HasValue)
			{
				PrioritySamplingState.Sampled = DistributedTracingState.InboundPayload.Sampled.Value;
			}

			//TODO: scopeManager.DistributedTracingState.setBaggage(payloadContext.getBaggage());
		}

		public void ApplyAdaptiveSampling(AdaptiveSampler adaptiveSampler)
		{
			adaptiveSampler.RequestStarted();
			PrioritySamplingState.SetSampledAndGeneratePriority(adaptiveSampler.ComputeSampled());
		}

		protected override IDictionary<string, object> BuildIntrinsics()
		{
			var intrinsics = base.BuildIntrinsics();
			intrinsics.Add("nr.entryPoint", true);
			return intrinsics;
		}

		protected override bool FinishInternal(DateTimeOffset finishTimestamp)
		{
			var didFinish = base.FinishInternal(finishTimestamp);

			if (didFinish)
			{
				RecordTransactionInfo();
				Collector.TransformSpans(this);
			}

			return didFinish;
		}

		private void RecordTransactionInfo()
		{
			TransactionState.Duration = _duration;

			string transactionType = "Other";

			object eventSourceArnTag = GetTag("aws.lambda.eventSource.arn");
			var s = eventSourceArnTag as string;
			string eventSourceArn = s != null ? s : "";
			if (eventSourceArn.StartsWith("arn:aws:iam") || eventSourceArn.StartsWith("arn:aws:elasticloadbalancing"))
			{
				transactionType = "WebTransaction";
			}

			string arn = (string)GetTag("aws.arn");
			if (arn != null && arn.Contains(":"))
			{
				TransactionState.SetTransactionName(transactionType, arn.Substring(arn.LastIndexOf(":", StringComparison.CurrentCulture) + 1));
			}
		}
	}
}
