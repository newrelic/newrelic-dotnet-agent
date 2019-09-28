using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.Couchbase
{
	public class CouchbaseQueryWrapperAsync : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny("Couchbase.NetClient", "Couchbase.CouchbaseBucket", "QueryAsync");

			if (canWrap)
			{
				return TaskFriendlySyncContextValidator.CanWrapAsyncMethod("Couchbase.NetClient", "Couchbase.CouchbaseBucket", "QueryAsync");
			}

			return new CanWrapResponse(false);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			if (instrumentedMethodCall.IsAsync)
			{
				transaction.AttachToAsync();
			}

			var operation = instrumentedMethodCall.MethodCall.Method.MethodName;

			var model = CouchbaseHelper.GetBucketName(instrumentedMethodCall.MethodCall.InvocationTarget);

			var parameterTypeName = instrumentedMethodCall.InstrumentedMethodInfo.Method.ParameterTypeNames;

			var parm = instrumentedMethodCall.MethodCall.MethodArguments[0];
			var commandText = CouchbaseHelper.GetStatement(parm, parameterTypeName);

			var segment = transaction.StartDatastoreSegment(
				instrumentedMethodCall.MethodCall,
				new ParsedSqlStatement(DatastoreVendor.Couchbase, model, operation),
				null,
				commandText);

			return Delegates.GetAsyncDelegateFor(agent, segment);
		}
	}
}
