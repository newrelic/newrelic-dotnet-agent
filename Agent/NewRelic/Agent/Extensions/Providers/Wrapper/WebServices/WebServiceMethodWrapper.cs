using System;
using System.Web;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.WebServices
{
	public class WebServiceMethodWrapper : IWrapper
	{

		public Func<Object, String> GetMethodInfo => _getMethodInfo ?? (_getMethodInfo = VisibilityBypasser.Instance.GenerateFieldReadAccessor<String>("System.Web.Extensions", "System.Web.Script.Services.WebServiceMethodData", "_methodName"));
		public Func<Object, Object> GetMethodOwner => _getMethodOwner ?? (_getMethodOwner = VisibilityBypasser.Instance.GeneratePropertyAccessor<Object>("System.Web.Extensions", "System.Web.Script.Services.WebServiceMethodData", "Owner"));
		public Func<Object, Object> GetMethodTypeData => _getMethodTypeData ?? (_getMethodTypeData = VisibilityBypasser.Instance.GeneratePropertyAccessor<Object>("System.Web.Extensions", "System.Web.Script.Services.WebServiceData", "TypeData"));
		public Func<Object, Type> GetMethodType => _getMethodType ?? (_getMethodType = VisibilityBypasser.Instance.GeneratePropertyAccessor<Type>("System.Web.Extensions", "System.Web.Script.Services.WebServiceTypeData", "Type"));


		public bool IsTransactionRequired => true;

		[CanBeNull]
		private Func<Object, String> _getMethodInfo;
		private Func<Object, Object> _getMethodOwner;
		private Func<Object, Object> _getMethodTypeData;
		private Func<Object, Type> _getMethodType;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "System.Web.Extensions", typeName: "System.Web.Script.Services.WebServiceMethodData", methodName: "CallMethod");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall,
			IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			var methodName = GetMethodInfo.Invoke(instrumentedMethodCall.MethodCall.InvocationTarget);
			if (methodName == null)
				throw new NullReferenceException("Could not retrieve a value from _methodName field on the invocation target");

			var service = instrumentedMethodCall.MethodCall.MethodArguments[0];
			var serviceType = "";
			if (service == null)
			{
				var methodOwner = GetMethodOwner.Invoke(instrumentedMethodCall.MethodCall.InvocationTarget);
				var methodTypeData = GetMethodTypeData.Invoke(methodOwner);
				var methodType = GetMethodType.Invoke(methodTypeData);
				serviceType = methodType.Name;
			}
			else
			{
				serviceType = service.GetType().Name;
			}

			var transactionName = serviceType + "/" + methodName;

			transactionWrapperApi.SetWebTransactionName(WebTransactionType.WebService, transactionName, TransactionNamePriority.FrameworkLow);
			var segment = transactionWrapperApi.StartMethodSegment(instrumentedMethodCall.MethodCall,
				instrumentedMethodCall.MethodCall.Method.Type.ToString(), instrumentedMethodCall.MethodCall.Method.MethodName);
			return Delegates.GetDelegateFor(segment);
		}
	}
}

