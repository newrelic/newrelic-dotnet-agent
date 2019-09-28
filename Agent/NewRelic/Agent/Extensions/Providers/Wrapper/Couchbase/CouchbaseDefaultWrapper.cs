using System;
using System.Collections;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.Couchbase
{
	public class CouchbaseDefaultWrapper : IWrapper
	{
		private Func<Object, String> _getMethodInfo;
		public Func<Object, String> GetMethodInfo => _getMethodInfo ?? (_getMethodInfo = VisibilityBypasser.Instance.GeneratePropertyAccessor<String>("Couchbase.NetClient", "Couchbase.CouchbaseBucket", "Name"));

		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny("Couchbase.NetClient", "Couchbase.CouchbaseBucket", new[]
				{
					"Append",
					"Decrement",
					"Exists",
					"Get",
					"GetAndLock",
					"GetAndTouch",
					"GetDocument",
					"GetFromReplica",
					"GetWithLock",
					"Increment",
					"Insert",
					"Invoke",
					"Observe",
					"Prepend",
					"Remove",
					"Replace",
					"Touch",
					"Unlock",
					"Upsert"
				});
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
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

			var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall,
				new ParsedSqlStatement(DatastoreVendor.Couchbase, model, operation));

			return Delegates.GetDelegateFor(segment);
		}
	}
}