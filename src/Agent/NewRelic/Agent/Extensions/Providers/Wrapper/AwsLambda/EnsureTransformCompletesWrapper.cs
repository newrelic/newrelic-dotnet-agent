// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.AwsLambda;

public class EnsureTransformCompletesWrapper : IWrapper
{
    public bool IsTransactionRequired => false;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
    {
        return new CanWrapResponse("NewRelic.Providers.Wrapper.AwsLambda.EnsureTransformCompletesWrapper".Equals(methodInfo.RequestedWrapperName));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        // The instrumented method is only called once in the lifetime of a lambda instance so we can just create a handler wrapper each time.
        CreateAndSetWrappedHandlerMethod(agent, transaction, instrumentedMethodCall.MethodCall.InvocationTarget);

        return Delegates.NoOp;
    }

    private static void CreateAndSetWrappedHandlerMethod(IAgent agent, ITransaction transaction, object bootstrapper)
    {
        var bootstrapperType = bootstrapper.GetType();

        var handlerReadAccessor = VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>(bootstrapperType, "_handler");
        var originalHandler = handlerReadAccessor(bootstrapper);

        // replace the original handler with NewHandler, which calls the original handler and waits for the transaction to finish before returning a response to the lambda runtime.
        var handlerWriteAccessor = VisibilityBypasser.Instance.GenerateFieldWriteAccessor<LambdaBootstrapHandler>(bootstrapperType, "_handler");
        handlerWriteAccessor(bootstrapper, NewHandler);
        return;

        // Creates a _handler that will wait for the transaction to finish being transformed and harvested before allowing
        // the lambda runtime to send a response back. This prevents a race condition where async continuations can run in different orders
        // which sometimes causes the lambda runtime to return a response before the agent can generate a lambda payload.
        // ref: https://github.com/aws/aws-lambda-dotnet/blob/c28fcfaba68607f785662ff1d232eb9b26d0fa09/Libraries/src/Amazon.Lambda.RuntimeSupport/Bootstrap/LambdaBootstrap.cs#L369
        // and https://github.com/aws/aws-lambda-dotnet/blob/c28fcfaba68607f785662ff1d232eb9b26d0fa09/Libraries/src/Amazon.Lambda.RuntimeSupport/Bootstrap/LambdaBootstrap.cs#L387
        async Task<InvocationResponse> NewHandler(InvocationRequest request)
        {
            transaction = agent.CreateTransaction(isWeb: false, category: AwsLambdaWrapperExtensions.GetTransactionCategory(agent.Configuration), transactionDisplayName: "TempLambdaName", doNotTrackAsUnitOfWork: true);
            agent.Logger.Finest("EnsureTransformCompletesWrapper: Started transaction.");

            try
            {
                agent.Logger.Finest("EnsureTransformCompletesWrapper: Calling original handler.");
                var typedHandler = (LambdaBootstrapHandler)originalHandler;
                return await typedHandler(request);
            }
            finally
            {
                agent.Logger.Finest("EnsureTransformCompletesWrapper: Ending transaction.");
                transaction.End();
            }
        }
    }
}
