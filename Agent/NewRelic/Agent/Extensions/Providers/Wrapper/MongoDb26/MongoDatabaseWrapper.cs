using System.Collections.Generic;
using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;


namespace NewRelic.Providers.Wrapper.MongoDb26
{
	public class MongoDatabaseWrapper:IWrapper
	{
		public bool IsTransactionRequired => true;
		private static readonly HashSet<string> CanExtractModelNameMethods = new HashSet<string>() { "CreateCollection", "CreateCollectionAsync", "DropCollection", "DropCollectionAsync" };

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;

			var canWrap = method.MatchesAny(assemblyName: "MongoDB.Driver", typeName: "MongoDB.Driver.MongoDatabaseImpl",
				methodNames: new[]
				{
					"CreateCollection",
					"CreateView",
					"DropCollection",
					"ListCollections",
					"RenameCollection",
					"RunCommand",
					
					"CreateCollectionAsync",
					"CreateViewAsync",
					"DropCollectionAsync",
					"ListCollectionsAsync",
					"RenameCollectionAsync",
					"RunCommandAsync"
				});

			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			var operation = instrumentedMethodCall.MethodCall.Method.MethodName;
			var model = TryGetModelName(instrumentedMethodCall);

			var caller = instrumentedMethodCall.MethodCall.InvocationTarget;
			ConnectionInfo connectionInfo = MongoDbHelper.GetConnectionInfoFromDatabase(caller);

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

		private string TryGetModelName(InstrumentedMethodCall instrumentedMethodCall)
		{
			var methodName = instrumentedMethodCall.MethodCall.Method.MethodName;

			if (CanExtractModelNameMethods.Contains(methodName))
			{
				if(instrumentedMethodCall.MethodCall.MethodArguments[0] is string)
				{
					return instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(0);
				}
				return instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(1);
			}
			
			return null;
		}
	}
}
