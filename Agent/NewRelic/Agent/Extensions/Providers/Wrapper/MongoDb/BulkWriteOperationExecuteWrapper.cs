using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Parsing;

namespace NewRelic.Providers.Wrapper.MongoDb
{
	public class BulkWriteOperationExecuteWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "MongoDB.Driver", typeName: "MongoDB.Driver.BulkWriteOperation", methodNames: new [] { "ExecuteHelper", "Insert" });
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			var operation = GetOperationName(instrumentedMethodCall.MethodCall);
			var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, ParsedSqlStatement.FromOperation(DatastoreVendor.MongoDB, operation));

			return Delegates.GetDelegateFor(segment);
		}

		private String GetOperationName(MethodCall methodCall)
		{
			if (methodCall.Method.MethodName == "Insert")
				return "BulkWriteOperation Insert";

			if (methodCall.Method.MethodName == "ExecuteHelper")
				return "BulkWriteOperation Execute";

			throw new Exception(string.Format("Method passed to BeforeWrappedMethod was unexpected. {0}", methodCall.Method.MethodName));
		}

	}
}
