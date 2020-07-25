using System;
using System.Collections;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Providers.Wrapper.WrapperUtilities;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Couchbase
{
    public class CouchbaseDefaultWrapperAsync : IWrapper
    {
        private Func<Object, String> _getMethodInfo;
        public Func<Object, String> GetMethodInfo => _getMethodInfo ?? (_getMethodInfo = VisibilityBypasser.Instance.GeneratePropertyAccessor<String>("Couchbase.NetClient", "Couchbase.CouchbaseBucket", "Name"));

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny("Couchbase.NetClient", "Couchbase.CouchbaseBucket", new[]
                {
                    "AppendAsync",
                    "DecrementAsync",
                    "ExistsAsync",
                    "GetAndLockAsync",
                    "GetAsync",
                    "GetAndTouchAsync",
                    "GetFromReplicaAsync",
                    "GetWithLockAsync",
                    "IncrementAsync",
                    "InsertAsync",
                    "InvokeAsync",
                    "ObserveAsync",
                    "PrependAsync",
                    "RemoveAsync",
                    "ReplaceAsync",
                    "TouchAsync",
                    "UnlockAsync",
                    "UpsertAsync"
                });

            if (canWrap)
            {
                return WrapperUtils.LegacyAspPipelineIsPresent()
                    ? new CanWrapResponse(false, WrapperUtils.LegacyAspPipelineNotSupportedMessage("Couchbase.NetClient", "Couchbase.CouchbaseBucket", methodInfo.Method.MethodName))
                    : new CanWrapResponse(true);
            }

            return new CanWrapResponse(false);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            var operation = instrumentedMethodCall.MethodCall.Method.MethodName;

            if (operation.StartsWith("Get"))
            {
                operation = "Get";
            }

            var parm = instrumentedMethodCall.MethodCall.MethodArguments[0];
            if (parm is IList || parm is IDictionary)
            {
                operation += "Multiple";
            }

            var model = GetMethodInfo.Invoke(instrumentedMethodCall.MethodCall.InvocationTarget);

            var segment = transaction.StartDatastoreSegment(
                instrumentedMethodCall.MethodCall,
                operation,
                DatastoreVendor.Couchbase,
                model);

            return WrapperUtils.GetAsyncDelegateFor(agentWrapperApi, segment);
        }

    }
}
