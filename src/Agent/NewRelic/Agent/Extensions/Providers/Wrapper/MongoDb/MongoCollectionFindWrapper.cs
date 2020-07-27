using System;
using System.Linq;
using JetBrains.Annotations;
using MongoDB.Driver;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.MongoDb
{
    public class MongoCollectionFindWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "MongoDB.Driver", typeName: "MongoDB.Driver.MongoCollection", methodNames: new[] { "FindAs", "FindOneAs" });
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            var operation = GetOperationName(instrumentedMethodCall.MethodCall);
            var model = MongoDBHelper.GetCollectionModelName(instrumentedMethodCall.MethodCall);
            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, operation, DatastoreVendor.MongoDB, model);

            return Delegates.GetDelegateFor(segment);
        }

        [NotNull]
        private String GetOperationName(MethodCall methodCall)
        {
            // Haven't seen any instance of MethodArguments being empty, but in case it could happen.
            if (!methodCall.MethodArguments.Any())
                return "Find";

            var firstArg = methodCall.MethodArguments[0];

            // FindAll passes a null IMongoQuery argument.
            if (firstArg == null)
                return "FindAll";

            if (firstArg is FindOneArgs)
                return "FindOne";

            return "Find";
        }
    }
}
