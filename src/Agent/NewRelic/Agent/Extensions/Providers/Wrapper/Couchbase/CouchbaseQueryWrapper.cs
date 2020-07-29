using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Couchbase
{
    public class CouchbaseQueryWrapper : IWrapper
    {

        private Func<object, string> _getMethodInfo;
        public Func<object, string> GetMethodInfo => _getMethodInfo ?? (_getMethodInfo = VisibilityBypasser.Instance.GeneratePropertyAccessor<string>("Couchbase.NetClient", "Couchbase.CouchbaseBucket", "Name"));

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny("Couchbase.NetClient", "Couchbase.CouchbaseBucket", "Query");
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            var operation = instrumentedMethodCall.MethodCall.Method.MethodName;

            var model = GetMethodInfo.Invoke(instrumentedMethodCall.MethodCall.InvocationTarget);

            var parm = instrumentedMethodCall.MethodCall.MethodArguments[0];
            string commandText = null;

            try
            {
                commandText = parm is string ? (string)parm : ((dynamic)parm)._statement;
            }
            catch { }

            var segment = transaction.StartDatastoreSegment(
                instrumentedMethodCall.MethodCall,
                operation,
                DatastoreVendor.Couchbase,
                model,
                commandText);

            return Delegates.GetDelegateFor(segment);
        }
    }
}
