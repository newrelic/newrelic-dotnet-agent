using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.MongoDb26
{
	public class MongoQueryProviderImplWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;

			var canWrap = method.MatchesAny(assemblyName: "MongoDB.Driver", typeName: "MongoDB.Driver.Linq.MongoQueryProviderImpl`1",
				methodNames: new[]
				{
					"ExecuteModel",
					"ExecuteModelAsync"
				});

			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, [NotNull] IAgent agent, [CanBeNull] ITransaction transaction)
		{
			var operation = instrumentedMethodCall.MethodCall.Method.MethodName;
			operation = operation.EndsWith("Async") ? "LinqQueryAsync" : "LinqQuery";

			var caller = instrumentedMethodCall.MethodCall.InvocationTarget;

			var collection = MongoDbHelper.GetCollectionFieldFromGeneric(caller);
			var database = MongoDbHelper.GetDatabaseFromGeneric(collection);

			ConnectionInfo connectionInfo = MongoDbHelper.GetConnectionInfoFromDatabase(database);
			var collectionNamespace = MongoDbHelper.GetCollectionNamespacePropertyFromGeneric(caller);
			var model = MongoDbHelper.GetCollectionName(collectionNamespace);

			var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall,
				new ParsedSqlStatement(DatastoreVendor.MongoDB, model, operation), isLeaf: true, connectionInfo: connectionInfo);

			return Delegates.GetDelegateFor(segment);
		}
	}
}
