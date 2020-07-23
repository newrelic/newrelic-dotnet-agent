using System;
using System.Threading;
using System.Threading.Tasks;
#if NET40
using System.Configuration;
using System.Runtime.Versioning;
using System.Web.Hosting;
#endif
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.WrapperUtilities
{
    public class WrapperUtils
    {
        private static String AsyncTransactionsMissingSupportUrl =
            "https://docs.newrelic.com/docs/agents/net-agent/troubleshooting/missing-async-metrics";

        public static String LegacyAspPipelineNotSupportedMessage(String assemblyName, String typeName, String methodName)
        {
            return $"The method {methodName} in class {typeName} from assembly {assemblyName} will not be instrumented.  Some async instrumentation is not supported on .NET 4.5 and greater unless you change your application configuration to use the new ASP pipeline. For details see: {AsyncTransactionsMissingSupportUrl}";
        }

        public static Boolean LegacyAspPipelineIsPresent()
        {

#if NETSTANDARD2_0
			return false;
#else
            // first check that the application is even running under ASP.NET
            if (!HostingEnvironment.IsHosted)
            {
                return false;
            }

            // This will return true if the web.config includes <httpRuntime targetFramework="4.5">
            var targetFrameworkName = AppDomain.CurrentDomain.GetData("ASPNET_TARGETFRAMEWORK") as FrameworkName;
            if (targetFrameworkName?.Version >= new Version(4, 5))
            {
                return false;
            }

            // This will return true if the web.config includes <add key="aspnet:UseTaskFriendlySynchronizationContext" value="true" />
            Boolean isTaskFriendlySyncContextEnabled;
            var appSettingValue = ConfigurationManager.AppSettings["aspnet:UseTaskFriendlySynchronizationContext"];
            Boolean.TryParse(appSettingValue, out isTaskFriendlySyncContextEnabled);

            return !isTaskFriendlySyncContextEnabled;
#endif
        }

        public static AfterWrappedMethodDelegate GetAsyncDelegateFor(IAgentWrapperApi agentWrapperApi, ISegment segment)
        {
            return Delegates.GetDelegateFor<Task>(
                onFailure: segment.End,
                onSuccess: task =>
                {
                    segment.RemoveSegmentFromCallStack();

                    if (task == null)
                        return;

                    var context = SynchronizationContext.Current;
                    if (context != null)
                    {
                        task.ContinueWith(responseTask => agentWrapperApi.HandleExceptions(() =>
                        {
                            segment.End();
                        }), TaskScheduler.FromCurrentSynchronizationContext());
                    }
                    else
                    {
                        task.ContinueWith(responseTask => agentWrapperApi.HandleExceptions(() =>
                        {
                            segment.End();
                        }), TaskContinuationOptions.ExecuteSynchronously);
                    }
                });
        }

    }
}
