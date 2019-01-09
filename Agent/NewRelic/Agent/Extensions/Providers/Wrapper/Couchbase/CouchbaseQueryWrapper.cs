using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Parsing;

namespace NewRelic.Providers.Wrapper.Couchbase
{
	public class CouchbaseQueryWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny("Couchbase.NetClient", "Couchbase.CouchbaseBucket", "Query");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			var operation = instrumentedMethodCall.MethodCall.Method.MethodName;

			var model = CouchbaseHelper.GetBucketName(instrumentedMethodCall.MethodCall.InvocationTarget);

			var parameterTypeName = instrumentedMethodCall.InstrumentedMethodInfo.Method.ParameterTypeNames;

			var parm = instrumentedMethodCall.MethodCall.MethodArguments[0];
			var commandText = CouchbaseHelper.GetStatement(parm, parameterTypeName);

			var segment = transactionWrapperApi.StartDatastoreSegment(
				instrumentedMethodCall.MethodCall,
				new ParsedSqlStatement(DatastoreVendor.Couchbase, model, operation),
				null,
				commandText);

			return Delegates.GetDelegateFor(segment);
		}
	}
}
