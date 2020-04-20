using System;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;
using NServiceBus.Unicast.Messages;

namespace NewRelic.Providers.Wrapper.NServiceBus
{
	/// <summary>
	/// Factory for NServiceBusSendMessage
	/// </summary>
	public class SendMessageWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "NServiceBus.Core", typeName: "NServiceBus.Unicast.UnicastBus", methodName: "SendMessage", parameterSignature: "NServiceBus.Unicast.SendOptions,NServiceBus.Unicast.Messages.LogicalMessage");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			var logicalMessage = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<LogicalMessage>(1);

			const string brokerVendorName = "NServiceBus";
			var queueName = TryGetQueueName(logicalMessage);
			var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Queue, MessageBrokerAction.Produce, brokerVendorName, queueName);

			CreateOutboundHeaders(agent, logicalMessage);
			return Delegates.GetDelegateFor(segment);
		}

		private static void CreateOutboundHeaders(IAgent agent, LogicalMessage logicalMessage)
		{
			if (logicalMessage.Headers == null)
				return;

			if (agent.Configuration.W3CEnabled)
			{
				var setHeaders = new Action<string, string>((key, value) =>
				{
					if (logicalMessage.Headers.ContainsKey(key)) 
					{
						logicalMessage.Headers.Remove(key);
					}

					logicalMessage.Headers.Add(key, value);
				});

				agent.CurrentTransaction.InsertDistributedTraceHeaders(setHeaders);
			}
			else
			{
				var headers = agent.CurrentTransaction
					.GetRequestMetadata()
					.Where(header => header.Value != null && header.Key != null);

				foreach (var header in headers)
				{
					logicalMessage.Headers[header.Key] = header.Value;
				}
			}
		}

		/// <summary>
		/// Returns a metric name based on the type of message. The source/destination queue isn't always known (depending on the circumstances) and in some cases isn't even relevant. The message type is always known and is always relevant.
		/// </summary>
		/// <param name="logicalMessage"></param>
		/// <returns></returns>
		private static string TryGetQueueName(LogicalMessage logicalMessage)
		{
			if (logicalMessage.MessageType == null)
				return null;

			return logicalMessage.MessageType.FullName;
		}
	}
}
