using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using NewRelic.SystemExtensions;
using NewRelic.Agent.Extensions.Parsing;
using System.ServiceModel.Channels;

namespace NewRelic.Providers.Wrapper.Wcf3
{
	public class MethodInvokerWrapper : IWrapper
	{
		// these must be lazily instatiated when the wrapper is actually used, not when the wrapper is first instantiated, so they sit in a nested class
		private static class Statics
		{
			public static readonly Func<object, MethodInfo> GetSyncMethodInfo = VisibilityBypasser.Instance.GenerateFieldReadAccessor<MethodInfo>(AssemblyName, SyncTypeName, "method");

			public static Func<object, MethodInfo> GetAsyncBeginMethodInfo = VisibilityBypasser.Instance.GenerateFieldReadAccessor<MethodInfo>(AssemblyName, AsyncTypeName, "beginMethod");

			public static Func<object, MethodInfo> GetAsyncEndMethodInfo = VisibilityBypasser.Instance.GenerateFieldReadAccessor<MethodInfo>(AssemblyName, AsyncTypeName, "endMethod");
		}

		private const string AssemblyName = "System.ServiceModel";
		private const string SyncTypeName = "System.ServiceModel.Dispatcher.SyncMethodInvoker";
		private const string AsyncTypeName = "System.ServiceModel.Dispatcher.AsyncMethodInvoker";
		private const string SyncMethodName = "Invoke";
		private const string AsyncBeginMethodName = "InvokeBegin";
		private const string AsyncEndMethodName = "InvokeEnd";

		public bool IsTransactionRequired => false;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny
			(
				assemblyNames: new[] { AssemblyName },
				typeNames: new[] { SyncTypeName, AsyncTypeName },
				methodNames: new[] { SyncMethodName, AsyncBeginMethodName, AsyncEndMethodName }
			);
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			var methodInfo = TryGetMethodInfo(instrumentedMethodCall.MethodCall.Method.MethodName, instrumentedMethodCall.MethodCall.InvocationTarget);
			if (methodInfo == null)
				throw new NullReferenceException("methodInfo");

			var parameters = GetParameters(instrumentedMethodCall.MethodCall, methodInfo, instrumentedMethodCall.MethodCall.MethodArguments, agent);

			var uri = OperationContext.Current?.IncomingMessageHeaders?.To;

			var name = GetTransactionName(agent, uri, methodInfo);

			transaction = agent.CreateWebTransaction(WebTransactionType.WCF, "Windows Communication Foundation", false);

			var absoluteUri = uri?.AbsoluteUri;

			if (!string.IsNullOrEmpty(absoluteUri))
			{
				transaction.SetUri(absoluteUri);
			}

			transaction.SetWebTransactionName(WebTransactionType.WCF, name, TransactionNamePriority.FrameworkHigh);
			transaction.SetRequestParameters(parameters);
			var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, name);

			var messageProperties = OperationContext.Current?.IncomingMessageProperties;
			if (messageProperties != null && messageProperties.TryGetValue(HttpRequestMessageProperty.Name, out var httpRequestMessageObject))
			{
				var httpRequestMessage = httpRequestMessageObject as HttpRequestMessageProperty;
				var retrievedHeaders = new List<KeyValuePair<string, string>>();

				foreach (var headerName in httpRequestMessage.Headers.AllKeys)
				{
					retrievedHeaders.Add(new KeyValuePair<string, string>(headerName, httpRequestMessage.Headers[headerName]));
				}

				agent.ProcessInboundRequest(retrievedHeaders, TransportType.HTTP);
			}

			return Delegates.GetDelegateFor(
				onFailure: exception =>
				{
					transaction.NoticeError(exception);
				},
				onComplete: () =>
				{
					var headersToAttach = transaction.GetResponseMetadata();
					foreach (var header in headersToAttach)
					{
						OperationContext.Current?.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(header.Key, "", header.Value));

						AddHeaderToHttpResponsePropertyForOutgoingMessage(header);
					}

					segment.End();
					transaction.End();
				});
		}

		private void AddHeaderToHttpResponsePropertyForOutgoingMessage(KeyValuePair<string, string> header)
		{
			if (OperationContext.Current == null)
			{
				return;
			}
			if (OperationContext.Current.OutgoingMessageProperties.TryGetValue(HttpResponseMessageProperty.Name, out var httpResponseMessagePropertyObject))
			{
				var httpResponseMessageProperty = httpResponseMessagePropertyObject as HttpResponseMessageProperty;
				httpResponseMessageProperty.Headers.Add(header.Key, header.Value);
			}
			else
			{
				var httpResponseMessageProperty = new HttpResponseMessageProperty();
				httpResponseMessageProperty.Headers.Add(header.Key, header.Value);
				OperationContext.Current.OutgoingMessageProperties.Add(HttpResponseMessageProperty.Name, httpResponseMessageProperty);
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

		private MethodInfo TryGetMethodInfo(string methodName, object invocationTarget)
		{
			if (methodName == SyncMethodName)
				return Statics.GetSyncMethodInfo(invocationTarget);
			else if (methodName == AsyncBeginMethodName)
				return Statics.GetAsyncBeginMethodInfo(invocationTarget);
			else if (methodName == AsyncEndMethodName)
				return Statics.GetAsyncEndMethodInfo(invocationTarget);

			throw new Exception("Unexpected instrumented method in wrapper: " + methodName);
		}

		private string GetTypeName( MethodInfo methodInfo)
		{
			var type = methodInfo.DeclaringType;
			if (type == null)
				throw new NullReferenceException("type");

			var name = type.FullName;
			if (name == null)
				throw new NullReferenceException("name");

			return name;
		}

		private string GetMethodName( MethodInfo methodInfo)
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
				&& methodCall.Method.MethodName != AsyncBeginMethodName)
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
