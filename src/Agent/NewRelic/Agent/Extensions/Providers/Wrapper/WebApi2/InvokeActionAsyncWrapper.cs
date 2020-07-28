using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Providers.Wrapper.WrapperUtilities;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.WebApi2
{
    public class AsyncApiControllerActionInvoker : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;

            var version = method.Type.Assembly.GetName().Version;
            if (version == null)
                return new CanWrapResponse(false);

            var canWrap = method.MatchesAny(assemblyName: "System.Web.Http", typeName: "System.Web.Http.Controllers.ApiControllerActionInvoker", methodName: "InvokeActionAsync") &&
                version.Major >= 5; // WebApi v2 == System.Web.Http v5

            if (canWrap)
            {
                return WrapperUtils.LegacyAspPipelineIsPresent()
                    ? new CanWrapResponse(false, WrapperUtils.LegacyAspPipelineNotSupportedMessage("System.Web.Http", "System.Web.Http.Controllers.ApiControllerActionInvoker", method.MethodName))
                    : new CanWrapResponse(true);

            }

            return new CanWrapResponse(false);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            var httpActionContext = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<HttpActionContext>(0);
            var controllerName = TryGetControllerName(httpActionContext) ?? "Unknown Controller";
            var actionName = TryGetActionName(httpActionContext) ?? "Unknown Action";

            var transactionName = string.Format("{0}/{1}", controllerName, actionName);
            transaction.SetWebTransactionName(WebTransactionType.WebAPI, transactionName, 6);

            var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, controllerName, actionName);

            return Delegates.GetDelegateFor<Task<HttpResponseMessage>>(
                onFailure: segment.End,
                onSuccess: task =>
                {
                    segment.RemoveSegmentFromCallStack();

                    if (task == null)
                        return;

                    var context = SynchronizationContext.Current;
                    if (context != null)
                    {
                        task.ContinueWith(_ => agentWrapperApi.HandleExceptions(segment.End),
                            TaskScheduler.FromCurrentSynchronizationContext());
                    }
                    else
                    {
                        task.ContinueWith(_ => agentWrapperApi.HandleExceptions(segment.End),
                            TaskContinuationOptions.ExecuteSynchronously);
                    }
                });
        }
        private static string TryGetControllerName(HttpActionContext httpActionContext)
        {
            var controllerContext = httpActionContext.ControllerContext;
            if (controllerContext == null)
                return null;

            var controllerDescriptor = controllerContext.ControllerDescriptor;
            if (controllerDescriptor == null)
                return null;

            return controllerDescriptor.ControllerName;
        }
        private static string TryGetActionName(HttpActionContext httpActionContext)
        {
            var actionDescriptor = httpActionContext.ActionDescriptor;
            if (actionDescriptor == null)
                return null;

            return actionDescriptor.ActionName;
        }
    }
}
