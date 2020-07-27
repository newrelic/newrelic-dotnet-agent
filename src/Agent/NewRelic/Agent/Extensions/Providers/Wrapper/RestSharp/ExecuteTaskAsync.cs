using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.RestSharp
{
    public class ExecuteTaskAsync : IWrapper
    {
        private const string AssemblyName = "RestSharp";
        private const string TypeName = "RestSharp.RestClient";

        private Func<object, Enum> _getMethod;
        private Func<object, object, Uri> _buildUri;

        public Func<object, Enum> GetMethod => _getMethod ?? (_getMethod = VisibilityBypasser.Instance.GeneratePropertyAccessor<Enum>(AssemblyName, "RestSharp.RestRequest", "Method"));

        //RestSharp is not strongly signed so type load fails if reference directly for .NET Framework applications
        public Func<object, object, Uri> BuildUri => _buildUri ?? (_buildUri = VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<Uri>(AssemblyName, TypeName, "BuildUri", "RestSharp.IRestRequest"));

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            //Because this does not leverage the sync context, it does not need to check for the legacy pipepline
            return new CanWrapResponse("NewRelic.Providers.Wrapper.RestSharp.ExecuteTaskAsync".Equals(instrumentedMethodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            var restClient = instrumentedMethodCall.MethodCall.InvocationTarget;
            var restRequest = instrumentedMethodCall.MethodCall.MethodArguments[0];

            Uri uri;

            try
            {
                uri = BuildUri(restClient, restRequest);
            }
            catch (Exception)
            {
                //BuildUri will throw an exception in RestSharp if the user does not have BaseUrl set.
                //Since the request will never execute, we will just NoOp.
                return Delegates.NoOp;
            }

            var method = GetMethod(restRequest).ToString();

            var segment = agentWrapperApi.CurrentTransaction.StartExternalRequestSegment(instrumentedMethodCall.MethodCall, uri, method);

            //Outbound CAT headers are added via AppendHeaders instrumentation.

            return Delegates.GetDelegateFor<Task>(
                onFailure: segment.End,
                onSuccess: AfterWrapped
            );

            void AfterWrapped(Task task)
            {
                segment.RemoveSegmentFromCallStack();

                if (task == null)
                {
                    return;
                }

                //It is very likely that the response from the external call will come back after the 
                //transaction ends. This line of code prevents the transaction from ending early. 
                transaction.Hold();

                //Do not want to post to the sync context as this library is commonly used with the
                //blocking TPL pattern of .Wait() or .Result. Posting to the sync context will result
                //in recording time waiting for the current unit of work on the sync context to finish.

                task.ContinueWith(responseTask => agentWrapperApi.HandleExceptions(() =>
                {
                    TryProcessResponse(agentWrapperApi, responseTask, transaction, segment);
                    segment.End();
                    transaction.Release();

                }));
            }
        }

        private static void TryProcessResponse(IAgentWrapperApi agentWrapperApi, Task responseTask, ITransaction transaction, ISegment segment)
        {
            try
            {
                if (!ValidTaskResponse(responseTask) || (segment == null))
                {
                    return;
                }

                //Cannot use RestSharp types because it is not strong named.
                var restResponse = ((dynamic)responseTask).Result;

                var headers = restResponse.Headers;
                if (headers == null)
                {
                    return;
                }

                var formattedHeaders = GetFormattedHeaders(headers);

                transaction.ProcessInboundResponse(formattedHeaders, segment);
            }
            catch (Exception ex)
            {
                agentWrapperApi.HandleWrapperException(ex);
            }
        }

        private static bool ValidTaskResponse(Task response)
        {
            return (response?.Status == TaskStatus.RanToCompletion);
        }

        private static IEnumerable<KeyValuePair<string, string>> GetFormattedHeaders(IEnumerable<dynamic> headers)
        {
            var processedHeaders = headers.Select(parameter =>
                new KeyValuePair<string, string>(parameter.Name, parameter.Value));

            return processedHeaders;
        }
    }
}
