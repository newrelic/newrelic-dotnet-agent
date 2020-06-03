using System;
using System.Web;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Asp35.Shared
{
    public class CallHandlerWrapper : IWrapper
    {
        public const string WrapperName = "Asp35.CallHandlerTracer";

        public Func<object, HttpApplication> GetHttpApplication { get { return _getHttpApplication ?? (_getHttpApplication = VisibilityBypasser.Instance.GenerateFieldReadAccessor<HttpApplication>("System.Web", "System.Web.HttpApplication+CallHandlerExecutionStep", "_application")); } }

        public bool IsTransactionRequired => true;

        private Func<object, HttpApplication> _getHttpApplication;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var canWrap = methodInfo.RequestedWrapperName.Equals(WrapperName, StringComparison.OrdinalIgnoreCase);
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var httpApplication = GetHttpApplication(instrumentedMethodCall.MethodCall.InvocationTarget);
            if (httpApplication == null)
                throw new NullReferenceException("httpApplication");

            var httpContext = httpApplication.Context;
            if (httpContext == null)
                throw new NullReferenceException("httpContext");

            var httpHandler = httpContext.Handler;
            if (httpHandler == null)
                return Delegates.NoOp;

            var httpHandlerName = httpHandler.GetType().Name;
            transaction.SetWebTransactionName(WebTransactionType.ASP, httpHandlerName, TransactionNamePriority.Handler);

            return Delegates.NoOp;
        }
    }
}
