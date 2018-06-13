using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;


namespace NewRelic.Providers.Wrapper.MongoDb26
{
	public class MongoIndexManagerWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;

			var canWrap = method.MatchesAny(assemblyName: "MongoDB.Driver", typeName: "MongoDB.Driver.MongoCollectionImpl`1+MongoIndexManager",
				methodNames: new[]
				{
					"CreateMany",
					"DropAll",
					"DropOne",
					"List",

					"CreateManyAsync",
					"DropAllAsync",
					"DropOneAsync",
					"ListAsync"
				});

			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			var operation = instrumentedMethodCall.MethodCall.Method.MethodName;
			dynamic caller = instrumentedMethodCall.MethodCall.InvocationTarget;

			var model = caller.CollectionNamespace.CollectionName;
			
			dynamic collection = MongoDbHelper.GetCollectionFieldFromGeneric(caller);
			ConnectionInfo connectionInfo = MongoDbHelper.GetConnectionInfoFromDatabase(collection.Database);

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

				task.ContinueWith(responseTask => agentWrapperApi.HandleExceptions(() =>
				{
					segment.End();
					transaction.Release();
				}));
			}
		}
	}
}
