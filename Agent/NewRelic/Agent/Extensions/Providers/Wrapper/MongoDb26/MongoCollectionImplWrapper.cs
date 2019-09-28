using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Api;

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

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			var operation = instrumentedMethodCall.MethodCall.Method.MethodName;
			var caller = instrumentedMethodCall.MethodCall.InvocationTarget;

			var collectionNamespace = MongoDbHelper.GetCollectionNamespacePropertyFromGeneric(caller);
			var model = MongoDbHelper.GetCollectionName(collectionNamespace);

			var database = MongoDbHelper.GetDatabaseFromGeneric(caller);

			ConnectionInfo connectionInfo = MongoDbHelper.GetConnectionInfoFromDatabase(database);

			var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall,
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

				transaction.Hold();

				if (task == null)
				{
					return;
				}

				task.ContinueWith(responseTask => agent.HandleExceptions(() =>
				{
					segment.End();
					transaction.Release();
				}));
			}
		}
	}
}
