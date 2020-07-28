using System;
using System.IO;
using System.Web;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Asp35.Shared
{
    public class FilterWrapper : IWrapper
    {
        public bool IsTransactionRequired => false;

        private static class Statics
        {
            public static readonly Func<HttpWriter, Boolean> IgnoringFurtherWrites = VisibilityBypasser.Instance.GeneratePropertyAccessor<HttpWriter, Boolean>("IgnoringFurtherWrites");
        }

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "System.Web", typeName: "System.Web.HttpWriter", methodNames: new[] { "Filter", "FilterIntegrated" });
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            var httpWriter = (HttpWriter)instrumentedMethodCall.MethodCall.InvocationTarget;
            if (httpWriter == null)
                throw new NullReferenceException("httpWriter");

            var httpContext = HttpContext.Current;
            // we have seen httpContext == null in the wild, so don't throw an exception
            if (httpContext == null)
                return Delegates.NoOp;

            if (httpContext.Response.StatusCode >= 400)
                return Delegates.NoOp;

            if (Statics.IgnoringFurtherWrites(httpWriter))
                return Delegates.NoOp;

            var newFilter = TryGetStreamInjector(agentWrapperApi, httpContext);
            if (newFilter != null)
                httpContext.Response.Filter = newFilter;

            return Delegates.NoOp;
        }
        private static Stream TryGetStreamInjector(IAgentWrapperApi agentWrapperApi, HttpContext httpContext)
        {
            var currentFilter = httpContext.Response.Filter;
            var contentEncoding = httpContext.Response.ContentEncoding;
            var contentType = httpContext.Response.ContentType;
            var requestPath = httpContext.Request.Path;

            // NOTE: We need to be very careful if we decide to move where TryGetStreamInjector is called from. The agent assumes that this call will happen fairly late in the pipeline as it has a side-effect of freezing the transaction name and capturing all of the currently recorded transaction attributes.
            return agentWrapperApi.TryGetStreamInjector(currentFilter, contentEncoding, contentType, requestPath);
        }
    }
}
