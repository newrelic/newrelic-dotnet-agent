using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Providers.Wrapper.WrapperUtilities;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Couchbase
{
	public class CouchbaseQueryWrapperAsync : IWrapper
	{
		private Func<Object, String> _getMethodInfo;
		public Func<Object, String> GetMethodInfo => _getMethodInfo ?? (_getMethodInfo = VisibilityBypasser.Instance.GeneratePropertyAccessor<String>("Couchbase.NetClient", "Couchbase.CouchbaseBucket", "Name"));

		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny("Couchbase.NetClient", "Couchbase.CouchbaseBucket", "QueryAsync");

			if (canWrap)
			{
				return WrapperUtils.LegacyAspPipelineIsPresent()
					? new CanWrapResponse(false, WrapperUtils.LegacyAspPipelineNotSupportedMessage("Couchbase.NetClient", "Couchbase.CouchbaseBucket", "QueryAsync"))
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

			var model = GetMethodInfo.Invoke(instrumentedMethodCall.MethodCall.InvocationTarget);

			var parm = instrumentedMethodCall.MethodCall.MethodArguments[0];
			String commandText = null;

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

			return WrapperUtils.GetAsyncDelegateFor(agentWrapperApi, segment);
		}
	}
}
