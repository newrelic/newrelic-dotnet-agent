using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NewRelic.Providers.Wrapper.Sql
{

    public class DataReaderAsyncWrapper : DataReaderWrapperBase
    {
        private static readonly string[] _tracerNames =
        {
            "DataReaderTracerAsync",
            "DataReaderWrapperAsync"
        };

        public override string[] WrapperNames => _tracerNames;

        public override bool ExecuteAsAsync => true;
    }


    public class DataReaderWrapper : DataReaderWrapperBase
    {
        private static readonly string[] _tracerNames =
        {
            "DataReaderTracer",
            "DataReaderWrapper"
        };

        public override string[] WrapperNames => _tracerNames;

        public override bool ExecuteAsAsync => false;
    }


    public abstract class DataReaderWrapperBase : IWrapper
    {
        public abstract string[] WrapperNames { get; }

        /// <summary>
        /// Sometimes, the methods that are being instrumented appear to be async in that they return a Task, but they are not actually
        /// decorated with the async decorator.  When this happens, the InstrumentedMethodCall.IsAsync cannot be relied upon to determine 
        /// whether or not to attach the AfterWrappedMethod as a continuation or to just run it.
        /// 
        /// Here is an example of the suble difference:
        /// public async Task<int> ExecuteScalar(...)
        /// public Task<int> ExecuteScalar(...)
        /// </summary>
        public abstract bool ExecuteAsAsync { get; }

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var canWrap = WrapperNames.Contains(methodInfo.RequestedWrapperName, StringComparer.OrdinalIgnoreCase);

            if (canWrap && ExecuteAsAsync)
            {
                var method = methodInfo.Method;
                return TaskFriendlySyncContextValidator.CanWrapAsyncMethod(method.Type.Assembly.GetName().Name, method.Type.FullName, method.MethodName);
            }

            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            //This only happens if we are in an async context.  Regardless if we are adding the after delegate as a contination.
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, "DatabaseResult/Iterate");
            segment.MakeCombinable();

            return ExecuteAsAsync
                ? Delegates.GetAsyncDelegateFor<Task>(agent, segment)
                : Delegates.GetDelegateFor(segment);
        }
    }
}
