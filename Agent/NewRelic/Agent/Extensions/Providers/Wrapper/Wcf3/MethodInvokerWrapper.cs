using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using NewRelic.SystemExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace NewRelic.Providers.Wrapper.Wcf3
{
	public class MethodInvokerWrapper : IWrapper
	{
		private static readonly object _wrapperToken = new object();

		// these must be lazily instatiated when the wrapper is actually used, not when the wrapper is first instantiated, so they sit in a nested class
		private static class Statics
		{
			//Supporting synchronous invoke
			public static readonly Func<object, MethodInfo> GetSyncMethodInfo = VisibilityBypasser.Instance.GenerateFieldReadAccessor<MethodInfo>(AssemblyName, SyncTypeName, "method");

			//Supporting BeginInvoke/EndInvoke async Style
			public static Func<object, MethodInfo> GetAsyncBeginMethodInfo = VisibilityBypasser.Instance.GenerateFieldReadAccessor<MethodInfo>(AssemblyName, AsyncTypeName, "beginMethod");
			public static Func<object, MethodInfo> GetAsyncEndMethodInfo = VisibilityBypasser.Instance.GenerateFieldReadAccessor<MethodInfo>(AssemblyName, AsyncTypeName, "endMethod");

			//Supporting Task based Async style
			public static Func<object, MethodInfo> GetTAPAsyncTaskMethodInfo = VisibilityBypasser.Instance.GenerateFieldReadAccessor<MethodInfo>(AssemblyName, TAPTypeName, "taskMethod");
		}

		private const string AssemblyName = "System.ServiceModel";
		private const string SyncTypeName = "System.ServiceModel.Dispatcher.SyncMethodInvoker";
		private const string AsyncTypeName = "System.ServiceModel.Dispatcher.AsyncMethodInvoker";
		private const string TAPTypeName = "System.ServiceModel.Dispatcher.TaskMethodInvoker";
		private const string TAPTypeNameShort = "TaskMethodInvoker";

		private const string SyncMethodName = "Invoke";
		private const string InvokeBeginMethodName = "InvokeBegin";
		private const string InvokeEndMethodName = "InvokeEnd";
		private const string InvokeAsyncMethodName = "InvokeAsync";

		/// <summary>
		/// Translates the method name to the type of invocation
		/// </summary>
		private readonly Dictionary<string, string> _methodNameInvocationTypesDic = new Dictionary<string, string>()
		{
			{ SyncMethodName, "Sync" },
			{ InvokeBeginMethodName, "APM" },
			{ InvokeAsyncMethodName, "TAP" }
		};

		private readonly List<string> _rptSupMetric_InvocType = new List<string>();
		private readonly object _rptSupMetric_InvocType_Lock = new object();


		public bool IsTransactionRequired => false;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny
			(
				assemblyNames: new[] { AssemblyName },
				typeNames: new[] { SyncTypeName, AsyncTypeName, TAPTypeName },
				methodNames: new[] { SyncMethodName, InvokeBeginMethodName, InvokeEndMethodName, InvokeAsyncMethodName }
			);
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			var transactionAlreadyExists = transaction.IsValid;
			var methodInfo = TryGetMethodInfo(instrumentedMethodCall);
			if (methodInfo == null)
			{
				throw new NullReferenceException("methodInfo");
			}

			//Identify if we need to end the transaction in the after delegate.
			//If it is synchronous or if it is EndInvoke, we need to end the transaction.
			var instrumentedMethodName = instrumentedMethodCall.MethodCall.Method.MethodName;
			var parameters = GetParameters(instrumentedMethodCall.MethodCall, methodInfo, instrumentedMethodCall.MethodCall.MethodArguments, agent);

			var uri = OperationContext.Current?.IncomingMessageHeaders?.To;

			var transactionName = GetTransactionName(agent, uri, methodInfo);

			if (!transactionAlreadyExists)
			{
				transaction = agent.CreateTransaction(
					isWeb: true,
					category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.WCF),
					transactionDisplayName: "Windows Communication Foundation",
					doNotTrackAsUnitOfWork: false);

				transaction.GetExperimentalApi().SetWrapperToken(_wrapperToken);
			}

			var requestPath = uri?.AbsolutePath;

			ReportSupportabilityMetric_InvocationMethod(agent, instrumentedMethodName);

			if (!string.IsNullOrEmpty(requestPath))
			{
				transaction.SetUri(requestPath);
			}

			//Don't set transaction name when InvokeEnd is called. Only on the Invoke, InvokeAsync or InvokeBegin should we name the transaction
			if (!instrumentedMethodName.Equals(InvokeEndMethodName, StringComparison.OrdinalIgnoreCase))
			{
				transaction.SetWebTransactionName(WebTransactionType.WCF, transactionName, TransactionNamePriority.FrameworkHigh);
				transaction.SetRequestParameters(parameters);

				var messageProperties = OperationContext.Current?.IncomingMessageProperties;
				if (!transactionAlreadyExists && messageProperties != null && messageProperties.TryGetValue(HttpRequestMessageProperty.Name, out var httpRequestMessageObject))
				{
					var httpRequestMessage = httpRequestMessageObject as HttpRequestMessageProperty;
					var retrievedHeaders = new List<KeyValuePair<string, string>>();

					foreach (var headerName in httpRequestMessage.Headers.AllKeys)
					{
						retrievedHeaders.Add(new KeyValuePair<string, string>(headerName, httpRequestMessage.Headers[headerName]));
					}

					agent.ProcessInboundRequest(retrievedHeaders, TransportType.HTTP);
				}
			}

			var isTAP = instrumentedMethodCall.InstrumentedMethodInfo.Method.Type.Name == TAPTypeNameShort;
			var isInstrumentingTAPInvokeEndCall = instrumentedMethodName.Equals(InvokeEndMethodName, StringComparison.OrdinalIgnoreCase) && isTAP;
			var isInstrumentingTAPInvokeAsyncCall = instrumentedMethodCall.MethodCall.Method.MethodName == InvokeAsyncMethodName;

			var segmentName = transactionName;

			//don't create segment when TaskMethodInvoker.InvokeEnd() is called. 
			var segment = isInstrumentingTAPInvokeEndCall ? null : transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, segmentName);

			var encounteredException = false;

			return Delegates.GetDelegateFor(
				
				onFailure: exception =>
				{
					transaction.NoticeError(exception);
					encounteredException = true;
				},

				onSuccess: (System.Threading.Tasks.Task result) =>
				{
					result.ContinueWith(ContinueWork);

					void ContinueWork(System.Threading.Tasks.Task t)
					{
						//This only apply for TAP call. Segment will be ended in the task continuation.
						if (instrumentedMethodCall.MethodCall.Method.MethodName == InvokeAsyncMethodName)
						{
							segment.End();
						}
					}
				},

				onComplete: () =>
				{

					//For TAP call, segment will be ended in the task continuation. For other calls, segment will be ended here.
					if (!isTAP)
					{
						segment.End();
					}

					//Don't end transaction yet when InvokeBegin and InvokeAsync are called. Unless an exception was encountered.
					var allowToEndTransaction = !instrumentedMethodName.Equals(InvokeBeginMethodName, StringComparison.OrdinalIgnoreCase) &&
												!instrumentedMethodName.Equals(InvokeAsyncMethodName, StringComparison.OrdinalIgnoreCase) ||
												encounteredException;

					if (allowToEndTransaction)
					{
						EndTransaction(transaction);
					}

					//Process response if the agent is not instrumenting BeginInvoke and InvokeAsync.
					var allowToProcessResponse = !instrumentedMethodName.Equals(InvokeBeginMethodName, StringComparison.OrdinalIgnoreCase) && !isInstrumentingTAPInvokeAsyncCall || encounteredException;

					if (allowToProcessResponse)
					{
						ProcessResponse(transaction, OperationContext.Current);
					}
				});
		}


		/// <summary>
		/// Records supportability metric for the type of invocation.
		/// Only need to do this once per wrapper.
		/// </summary>
		private void ReportSupportabilityMetric_InvocationMethod(IAgent agent, string methodName)
		{

			//Since we share EndInvoke for both TAP and Begin/End Async, it is not 
			//contained in the dictionary
			if (!_methodNameInvocationTypesDic.TryGetValue(methodName, out string invocationTypeName))
			{
				return;
			}

			var shouldRecordMetric = false;
			lock (_rptSupMetric_InvocType_Lock)
			{
				shouldRecordMetric = !_rptSupMetric_InvocType.Contains(invocationTypeName);

				if(shouldRecordMetric)
				{
					_rptSupMetric_InvocType.Add(invocationTypeName);
				}
			}

			if (shouldRecordMetric)
			{
				agent.GetExperimentalApi().RecordSupportabilityMetric($"WCFService/InvocationStyle/{invocationTypeName}");
			}
		}


		//End transaction only if this wrapper is the one created it.
		private void EndTransaction(ITransaction transaction)
		{
			var wcfStartedTransaction = transaction.GetExperimentalApi().GetWrapperToken() == _wrapperToken;
			if (wcfStartedTransaction)
			{
				transaction.End();
			}
		}

		private void ProcessResponse(ITransaction transaction, OperationContext context)
		{
			var wcfStartedTransaction = transaction.GetExperimentalApi().GetWrapperToken() == _wrapperToken;
			if (wcfStartedTransaction)
			{
				var headersToAttach = transaction.GetResponseMetadata();
				foreach (var header in headersToAttach)
				{
					var outgoingMessageHeaders = context.OutgoingMessageHeaders;
					if (outgoingMessageHeaders != null && outgoingMessageHeaders.MessageVersion.Envelope != EnvelopeVersion.None)
					{
						context.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(header.Key, "", header.Value));
					}

					AddHeaderToHttpResponsePropertyForOutgoingMessage(header, context);
				}
			}
		}


		private void AddHeaderToHttpResponsePropertyForOutgoingMessage(KeyValuePair<string, string> header, OperationContext context)
		{
			if (context == null)
			{
				return;
			}
			if (context.OutgoingMessageProperties.TryGetValue(HttpResponseMessageProperty.Name, out var httpResponseMessagePropertyObject))
			{
				var httpResponseMessageProperty = httpResponseMessagePropertyObject as HttpResponseMessageProperty;
				httpResponseMessageProperty.Headers.Add(header.Key, header.Value);
			}
			else
			{
				var httpResponseMessageProperty = new HttpResponseMessageProperty();
				httpResponseMessageProperty.Headers.Add(header.Key, header.Value);
				context.OutgoingMessageProperties.Add(HttpResponseMessageProperty.Name, httpResponseMessageProperty);
			}
		}

		private string GetTransactionName(IAgent agent, Uri uri, MethodInfo methodInfo)
		{
			if (agent.Configuration.UseResourceBasedNamingForWCFEnabled)
			{
				if (uri != null)
				{
					return UriHelpers.GetTransactionNameFromPath(uri.AbsolutePath);
				}
			}

			var typeName = GetTypeName(methodInfo);
			var methodName = GetMethodName(methodInfo);

			return $"{typeName}.{methodName}";
		}

		private MethodInfo TryGetMethodInfo(InstrumentedMethodCall instrumentedMethodCall)
		{
			var methodName = instrumentedMethodCall.MethodCall.Method.MethodName;
			var invocationTarget = instrumentedMethodCall.MethodCall.InvocationTarget;
			var isTAP = instrumentedMethodCall.InstrumentedMethodInfo.Method.Type.Name == TAPTypeNameShort;

			if (methodName == SyncMethodName)
			{
				return Statics.GetSyncMethodInfo(invocationTarget);
			}
			else if (methodName == InvokeBeginMethodName)
			{
				return isTAP ? Statics.GetTAPAsyncTaskMethodInfo(invocationTarget) : Statics.GetAsyncBeginMethodInfo(invocationTarget);
			}
			else if (methodName == InvokeEndMethodName)
			{
				return isTAP ? Statics.GetTAPAsyncTaskMethodInfo(invocationTarget) : Statics.GetAsyncEndMethodInfo(invocationTarget);
			}
			else if (methodName == InvokeAsyncMethodName)
			{
				return Statics.GetTAPAsyncTaskMethodInfo(invocationTarget);
			}

			throw new Exception($"Unexpected instrumented method in wrapper: {instrumentedMethodCall.MethodCall.Method.MethodName}");
		}

		private string GetTypeName(MethodInfo methodInfo)
		{
			var type = methodInfo.DeclaringType;
			if (type == null)
				throw new NullReferenceException("type");

			var name = type.FullName;
			if (name == null)
				throw new NullReferenceException("name");

			return name;
		}

		private string GetMethodName(MethodInfo methodInfo)
		{
			var name = methodInfo.Name;
			if (name == null)
				throw new NullReferenceException("name");

			return name;
		}

		private IEnumerable<KeyValuePair<string, string>> GetParameters( MethodCall methodCall,  MethodInfo methodInfo, object[] arguments,  IAgent agent)
		{
			// only the begin methods will have parameters, end won't
			if (methodCall.Method.MethodName != SyncMethodName
				&& methodCall.Method.MethodName != InvokeBeginMethodName)
				return Enumerable.Empty<KeyValuePair<string, string>>();

			var parameters = arguments.ExtractNotNullAs<object[]>(1);

			var parameterInfos = methodInfo.GetParameters();
			if (parameterInfos == null)
				throw new Exception("MethodInfo did not contain parameters!");

			// if this occurs their app will throw an exception as well, which we will hopefully notice
			if (parameters.Length > parameterInfos.Length)
				return Enumerable.Empty<KeyValuePair<string, string>>();

			var result = new Dictionary<string, string>();
			for (var i = 0; i < parameters.Length; ++i)
			{
				var parameterInfo = parameterInfos[i];
				if (parameterInfo == null)
					throw new Exception("There was a null parameterInfo in the parameter infos array at index " + i);
				if (parameterInfo.Name == null)
					throw new Exception("A parameterInfo at index " + i + " did not have a name.");
				var keyString = parameterInfo.Name;

				var value = parameters[i];
				var valueString = (value == null) ? "null" : value.ToString();
				result.Add(keyString, valueString);
			}

			return result;
		}

	}
}
