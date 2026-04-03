// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Hangfire;

/// <summary>
/// Wrapper for Hangfire.Server.CoreBackgroundJobPerformer.Perform() instrumentation.
/// Captures all background job execution with transaction and segment tracking.
/// </summary>
public class HangfirePerformerWrapper : IWrapper
{
    private const string WrapperName = "HangfirePerformerWrapper";

    public bool IsTransactionRequired => false;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
    {
        return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        // Extract PerformContext parameter
        var performContext = instrumentedMethodCall.MethodCall.MethodArguments[0];
        if (performContext == null)
        {
            return Delegates.NoOp; // No params, no way to name this.
        }

        // Extract job information from context
        var backgroundJob = HangfireHelper.GetBackgroundJob(performContext);
        var jobId = HangfireHelper.GetJobId(backgroundJob);
        var job = HangfireHelper.GetJob(backgroundJob);
        var jobClassName = HangfireHelper.GetJobClassName(job);
        var jobMethodName = HangfireHelper.GetJobMethodName(job);
        var queueName = HangfireHelper.GetQueueName(job);
        var serverId = HangfireHelper.GetServerId(performContext);

        // Build transaction name
        var taskName = jobClassName + "." + jobMethodName;

        // Create background transaction following Ruby formatting for ActiveJob
        transaction = agent.CreateTransaction(
            isWeb: false,
            category: "Hangfire", 
            transactionDisplayName: taskName + "/execute",
            doNotTrackAsUnitOfWork: false);

        // Required so that we can ensure that the transaction is active during the entire job execution, including any async continuations
        transaction.AttachToAsync();

        // If this is in a queue, we want to create a metric more like a message queue.  Based on how Ruby handles ActiveJob.
        var segmentName = string.IsNullOrWhiteSpace(queueName) ? "Hangfire/" + taskName : "Hangfire/Queue/Consume/Named/" + queueName + "/" + taskName;
        var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, segmentName);

        segment.AddAgentAttribute("workflow.platform.name", "hangfire");
        segment.AddAgentAttribute("workflow.task.name", taskName);
        segment.AddAgentAttribute("workflow.task.id", jobId);
        segment.AddAgentAttribute("workflow.task.queue", queueName);
        segment.AddAgentAttribute("workflow.task.server", serverId);

        return Delegates.GetDelegateFor<object>(
            onSuccess: o =>
            {
                segment.AddAgentAttribute("workflow.execution.result", "success");
                segment.End();
            },
            onFailure: exception =>
            {
                segment.AddAgentAttribute("workflow.execution.result", "failure");
                segment.AddAgentAttribute("error.type", exception.GetType().Name);
                segment.End(exception);
            },
            onComplete: () =>
            {
                transaction.End();
            });
    }
}
