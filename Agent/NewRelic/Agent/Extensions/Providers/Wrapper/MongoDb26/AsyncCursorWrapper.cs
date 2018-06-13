using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing.ConnectionString;


namespace NewRelic.Providers.Wrapper.MongoDb26
{
	public class AsyncCursorWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;

			var canWrap = method.MatchesAny(assemblyName: "MongoDB.Driver.Core", typeName: "MongoDB.Driver.Core.Operations.AsyncCursor`1",
				methodSignatures: new[]
				{
					new MethodSignature("GetNextBatch", "System.Threading.CancellationToken"),
					new MethodSignature("GetNextBatchAsync", "System.Threading.CancellationToken"),
				});

			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			var operation = instrumentedMethodCall.MethodCall.Method.MethodName;

			var caller = instrumentedMethodCall.MethodCall.InvocationTarget;
			var collectionNamespace = MongoDbHelper.GetCollectionNamespaceFieldFromGeneric(caller);
			var model = MongoDbHelper.GetCollectionName(collectionNamespace);

			var connectionInfo = MongoDbHelper.GetConnectionInfoFromCursor(caller, collectionNamespace);

			var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall,
				new ParsedSqlStatement(DatastoreVendor.MongoDB, model, operation), isLeaf: true,
				connectionInfo: connectionInfo);

			if (!instrumentedMethodCall.IsAsync)
			{
				return Delegates.GetDelegateFor(segment);
			}

			return Delegates.GetDelegateFor<Task>(
				onFailure: segment.End,
				onSuccess: AfterWrapped
			);

			void AfterWrapped(Task task)
			{
				segment.RemoveSegmentFromCallStack();

				transaction.Hold();

				if (task == null)
				{
					return;
				}

				task.ContinueWith(responseTask => agentWrapperApi.HandleExceptions(() =>
				{
					segment.End();
					transaction.Release();
				}));
			}
		}
	}
}
