using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.ServiceModel;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Helpers;
using NewRelic.Reflection;
using NewRelic.SystemExtensions;
using NewRelic.SystemExtensions.Collections;

namespace NewRelic.Providers.Wrapper.Wcf3
{
	public class ServiceChannelProxyWrapper : IWrapper
	{
		private const string ServiceModelAssembly = "System.ServiceModel";
		private const string ServiceModelInternalsAssembly = "System.ServiceModel.Internals";
		private const string ServiceChannelProxyType = "System.ServiceModel.Channels.ServiceChannelProxy";
		private const string MethodDataType = "System.ServiceModel.Channels.ServiceChannelProxy+MethodData";
		private const string AsyncResultType = "System.Runtime.AsyncResult";

		private const string InvokeMethod = "Invoke";

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

		//used to get the AsyncCallback exception so that we can log it.
		private Func<object, Exception> _getAsyncCallbackException;
		private Func<object, Exception> GetAsyncCallbackException() { return _getAsyncCallbackException ?? (_getAsyncCallbackException = VisibilityBypasser.Instance.GenerateFieldReadAccessor<Exception>(ServiceModelInternalsAssembly, AsyncResultType, "exception")); }

		// used to allow us to call System.ServiceModel.Channels.ServiceChannelProxy.GetMethodData to determine what type of call we are dealing with
		private Func<object, System.Runtime.Remoting.Messaging.IMethodCallMessage, object> _getMethodDataMethod;
		private Func<object, System.Runtime.Remoting.Messaging.IMethodCallMessage, object> GetMethodDataMethod() { return _getMethodDataMethod ?? (_getMethodDataMethod = VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<System.Runtime.Remoting.Messaging.IMethodCallMessage, object>(ServiceModelAssembly, ServiceChannelProxyType, "GetMethodData")); }

		//Used as getter to get MethodType
		private static MethodInfo _methodTypeMethodInfo;

		private static object _serviceEnum;
		private static object _beginServiceEnum;
		private static object _taskServiceEnum;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny
			(
				assemblyName: ServiceModelAssembly,
				typeName: ServiceChannelProxyType,
				methodSignatures: new[]
				{
					new MethodSignature(InvokeMethod)
				}
			);
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			var serviceChannelProxy = instrumentedMethodCall.MethodCall.InvocationTarget;

			var message = instrumentedMethodCall.MethodCall.MethodArguments[0] as System.Runtime.Remoting.Messaging.IMethodCallMessage;

			var methodData = GetMethodDataMethod()(serviceChannelProxy, message);

			if (_methodTypeMethodInfo == null)
			{
				_methodTypeMethodInfo = methodData.GetType().GetProperty("MethodType").GetMethod;
			}

			var methodType = _methodTypeMethodInfo.Invoke(methodData, null);

			if (_serviceEnum == null || _beginServiceEnum == null || _taskServiceEnum == null)
			{
				var type = methodType.GetType();
				_serviceEnum = Enum.Parse(type, "Service");
				_beginServiceEnum = Enum.Parse(type, "BeginService");
				_taskServiceEnum = Enum.Parse(type, "TaskService");
			}

			if (!methodType.Equals(_serviceEnum) && !methodType.Equals(_beginServiceEnum) && !methodType.Equals(_taskServiceEnum))
			{
				return Delegates.NoOp;
			}

			var name = GetName(instrumentedMethodCall.MethodCall);
			var uri = GetUri(instrumentedMethodCall);

			var segment = transaction.StartExternalRequestSegment(instrumentedMethodCall.MethodCall, uri, name, isLeaf: true);

			return Delegates.GetDelegateFor<System.Runtime.Remoting.Messaging.IMethodReturnMessage>(
				onSuccess: OnSuccess,
				onFailure: OnFailure
			);

			void OnSuccess(System.Runtime.Remoting.Messaging.IMethodReturnMessage methodReturnMessage)
			{
				if (methodType.Equals(_serviceEnum))
				{
					if (methodReturnMessage.Exception != null)
					{
						HandleException(methodReturnMessage.Exception);
					}
					segment.End();
				}
				else if (methodType.Equals(_taskServiceEnum))
				{
					segment.RemoveSegmentFromCallStack();
					var task = (Task)methodReturnMessage.ReturnValue;
					task.ContinueWith(ContinueWork);

					void ContinueWork(Task t)
					{
						var aggregateException = t.Exception as AggregateException;
						var protocolException = aggregateException?.InnerExceptions.FirstOrDefault(IsProtocolException);
						if (protocolException != null)
						{
							HandleException(t.Exception);
						}
						segment.End();
					}
				}
				else if (methodType.Equals(_beginServiceEnum))
				{
					segment.RemoveSegmentFromCallStack();
					var originalCallback = GetAsyncCallbackReadAccessor().Invoke(methodReturnMessage.ReturnValue);
					SetAsyncCallbackWriteAccessor().Invoke(methodReturnMessage.ReturnValue, (AsyncCallback)WrappedAsyncCallback);

					void WrappedAsyncCallback(IAsyncResult asyncResult)
					{
						var exception = GetAsyncCallbackException().Invoke(asyncResult);
						if (exception != null)
						{
							HandleException(exception);
						}
						segment.End();
						originalCallback?.Invoke(asyncResult);
					}
				}
			}

			void OnFailure(Exception exception)
			{
				HandleException(exception);
				segment.End();
			}

			void HandleException(Exception exception)
			{
				var protocolException = exception as ProtocolException;

				if (protocolException == null)
				{
					var aggregateException = exception as AggregateException;
					protocolException = aggregateException?.InnerExceptions.FirstOrDefault(IsProtocolException) as ProtocolException;
				}

				var webException = protocolException?.InnerException as WebException;
				if (webException != null)
				{
					// This is needed because the message inspector which normally handles CAT/DT headers doesn't run if there is a protocol exception
					transaction.ProcessInboundResponse(webException.Response?.Headers?.ToDictionary(), segment);
				}
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

		private static bool IsProtocolException(Exception e)
		{
			return e is ProtocolException;
		}
	}
}
