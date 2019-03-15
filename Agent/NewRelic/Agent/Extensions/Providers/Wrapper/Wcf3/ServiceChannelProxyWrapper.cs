using System;
using System.ServiceModel;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Helpers;
using NewRelic.Reflection;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.Wcf3
{
	public class ServiceChannelProxyWrapper : IWrapper
	{
		private const string ServiceModelAssembly = "System.ServiceModel";
		private const string ServiceModelInternalsAssembly = "System.ServiceModel.Internals";
		private const string ServiceChannelProxyType = "System.ServiceModel.Channels.ServiceChannelProxy";
		private const string AsyncResultType = "System.Runtime.AsyncResult";
		private const string InvokeServiceMethod = "InvokeService";
		private const string InvokeBeginServiceMethod = "InvokeBeginService";

		public bool IsTransactionRequired => true;

		//Access to the channel to get the URI
		private Func<object, IServiceChannel> _getServiceChannel;
		private Func<object, IServiceChannel> GetServiceChannel() { return _getServiceChannel ?? (_getServiceChannel = VisibilityBypasser.Instance.GenerateFieldReadAccessor<IServiceChannel>(ServiceModelAssembly, ServiceChannelProxyType, "serviceChannel")); }

		//used to get the original AsyncCallback so that we can wrap it inside our own callback.
		private Func<object, AsyncCallback> _getAsyncCallbackReadAccessor;
		private Func<object, AsyncCallback> GetAsyncCallbackReadAccessor() { return _getAsyncCallbackReadAccessor ?? (_getAsyncCallbackReadAccessor = VisibilityBypasser.Instance.GenerateFieldReadAccessor<AsyncCallback>(ServiceModelInternalsAssembly, AsyncResultType, "callback")); }

		//Used to write our AsyncCallback wrapper back to the callback on the IAsyncResult.
		private Action<object, AsyncCallback> _setAsyncCallbackWriteAccessor;
		private Action<object, AsyncCallback> SetAsyncCallbackWriteAccessor() { return _setAsyncCallbackWriteAccessor ?? (_setAsyncCallbackWriteAccessor = VisibilityBypasser.Instance.GenerateFieldWriteAccessor<AsyncCallback>(ServiceModelInternalsAssembly, AsyncResultType, "callback")); }

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny
			(
				assemblyName: ServiceModelAssembly,
				typeName: ServiceChannelProxyType,
				methodSignatures: new[]
				{
					new MethodSignature(InvokeServiceMethod),
					new MethodSignature(InvokeBeginServiceMethod)
				}
			);
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			var name = GetName(instrumentedMethodCall.MethodCall);
			var uri = GetUri(instrumentedMethodCall);
			var segment = transactionWrapperApi.StartExternalRequestSegment(instrumentedMethodCall.MethodCall, uri, name, isLeaf: true);

			return Delegates.GetDelegateFor<System.Runtime.Remoting.Messaging.IMethodReturnMessage>(
				onSuccess: OnSuccess,
				onFailure: OnFailure
			);

			void OnSuccess(System.Runtime.Remoting.Messaging.IMethodReturnMessage methodReturnMessage)
			{
				if (instrumentedMethodCall.MethodCall.Method.MethodName == InvokeServiceMethod)
				{
					segment.End();
				}
				else
				{
					var originalCallback = GetAsyncCallbackReadAccessor().Invoke(methodReturnMessage.ReturnValue);
					SetAsyncCallbackWriteAccessor().Invoke(methodReturnMessage.ReturnValue, (AsyncCallback)WrappedAsyncCallback);

					void WrappedAsyncCallback(IAsyncResult asyncResult)
					{
						segment.End();
						originalCallback?.Invoke(asyncResult);
					}
				}
			}

			void OnFailure(Exception exception)
			{
				segment.End();
			}
		}

		private static string GetName(MethodCall methodCall)
		{
			var methodCallMessage = methodCall.MethodArguments.ExtractAs<System.Runtime.Remoting.Messaging.IMethodCallMessage>(0);
			if (methodCallMessage == null)
			{
				throw new NullReferenceException("methodCallMessage");
			}

			var typeName = methodCallMessage.TypeName;
			if (typeName == null)
			{
				throw new NullReferenceException("typeName");
			}

			// The type name is the full class name followed by a comma and the assembly info.  We need to cut off at the comma.
			typeName = typeName.TrimAfterAChar(StringSeparators.CommaChar);

			var methodName = methodCallMessage.MethodName;
			if (methodName == null)
			{
				throw new NullReferenceException("methodName");
			}

			return $"{typeName}.{methodName}";
		}

		private Uri GetUri(InstrumentedMethodCall instrumentedMethodCall)
		{
			var serviceChannel = GetServiceChannel().Invoke(instrumentedMethodCall.MethodCall.InvocationTarget);
			return serviceChannel.RemoteAddress.Uri;
		}
	}
}
