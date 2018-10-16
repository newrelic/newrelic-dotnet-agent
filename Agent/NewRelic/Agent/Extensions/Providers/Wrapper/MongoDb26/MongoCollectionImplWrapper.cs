using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Parsing;


namespace NewRelic.Providers.Wrapper.MongoDb26
{
	public class MongoCollectionImplWrapper:IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;

			var canWrap = method.MatchesAny(assemblyName: "MongoDB.Driver", typeName: "MongoDB.Driver.MongoCollectionImpl`1",
				methodNames: new[]
				{
					"Aggregate",
					"BulkWrite",
					"Count",
					"Distinct",
					"FindSync",
					"FindOneAndDelete",
					"FindOneAndReplace",
					"FindOneAndUpdate",
					"MapReduce",
					"Watch",

					"AggregateAsync",
					"BulkWriteAsync",
					"CountAsync",
					"DistinctAsync",
					"FindAsync",
					"FindOneAndDeleteAsync",
					"FindOneAndReplaceAsync",
					"FindOneAndUpdateAsync",
					"MapReduceAsync",
					"WatchAsync"
				});

			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			var operation = instrumentedMethodCall.MethodCall.Method.MethodName;
			dynamic caller = instrumentedMethodCall.MethodCall.InvocationTarget;

			var model = caller.CollectionNamespace.CollectionName;

			ConnectionInfo connectionInfo = MongoDbHelper.GetConnectionInfoFromDatabase(caller.Database);

			var segment = transactionWrapperApi.StartDatastoreSegment(instrumentedMethodCall.MethodCall,
				new ParsedSqlStatement(DatastoreVendor.MongoDB, model, operation), isLeaf: true, connectionInfo: connectionInfo);

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

				transactionWrapperApi.Hold();

				if (task == null)
				{
					return;
				}

				task.ContinueWith(responseTask => agentWrapperApi.HandleExceptions(() =>
				{
					segment.End();
					transactionWrapperApi.Release();
				}));
			}
		}
	}
}
