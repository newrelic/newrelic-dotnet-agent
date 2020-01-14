using System;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
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

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			var operation = instrumentedMethodCall.MethodCall.Method.MethodName;
			var caller = instrumentedMethodCall.MethodCall.InvocationTarget;

			var collectionNamespace = MongoDbHelper.GetCollectionNamespacePropertyFromGeneric(caller);
			var model = MongoDbHelper.GetCollectionName(collectionNamespace);

			var collection = MongoDbHelper.GetCollectionFieldFromGeneric(caller);
			var database = MongoDbHelper.GetDatabaseFromGeneric(collection);

			ConnectionInfo connectionInfo = MongoDbHelper.GetConnectionInfoFromDatabase(database);

			var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall,
				new ParsedSqlStatement(DatastoreVendor.MongoDB, model, operation), isLeaf: true, connectionInfo: connectionInfo);

			if (!operation.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
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
