// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.Llm;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Bedrock;

public class ConverseAsyncWrapper : IWrapper
{
    public bool IsTransactionRequired => true; // part of spec, only create events for transactions.

    private static ConcurrentDictionary<Type, Func<object, object>> _getResultFromGenericTask = new();
    private static ConcurrentDictionary<string, string> _libraryVersions = new();
    private const string WrapperName = "BedrockConverseAsync";
    private const string VendorName = "Bedrock";

    public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
    {
        return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        // Don't do anything, including sending the version Supportability metric, if we're disabled
        if (!agent.Configuration.AiMonitoringEnabled)
        {
            return Delegates.NoOp;
        }

        if (instrumentedMethodCall.IsAsync)
        {
            transaction.AttachToAsync();
        }

        dynamic converseRequest = instrumentedMethodCall.MethodCall.MethodArguments[0];
        string modelId = converseRequest.ModelId.ToLower();
        SupportabilityHelpers.CreateModelIdSupportabilityMetricsForBedrock(modelId, agent);
        var operationType = "completion"; // Converse doesn't support embedding
        var segment = transaction.StartCustomSegment(instrumentedMethodCall.MethodCall, $"Llm/{operationType}/{VendorName}/{instrumentedMethodCall.MethodCall.Method.MethodName}");

        // required per spec
        var version = GetOrAddLibraryVersion(instrumentedMethodCall.MethodCall.Method.Type.Assembly.ManifestModule.Assembly.FullName);
        agent.RecordSupportabilityMetric($"DotNet/ML/{VendorName}/{version}");

        return Delegates.GetAsyncDelegateFor<Task>(
            agent,
            segment,
            false,
            TryProcessConverseResponse,
            TaskContinuationOptions.ExecuteSynchronously
        );

        void TryProcessConverseResponse(Task responseTask)
        {
            // We need the duration, so we end the segment before creating the events.
            segment.End();

            if (responseTask.IsFaulted)
            {
                HandleError(segment, converseRequest, responseTask, agent, modelId);
                return;
            }

            dynamic converseResponse = GetTaskResult(responseTask);
            if (converseResponse == null || converseResponse.HttpStatusCode >= HttpStatusCode.MultipleChoices)
            {
                agent.Logger.Warn($"Error processing Converse response for model {modelId}: Response {(converseResponse == null ? "is null" : $"has non-success HttpStatusCode: {converseResponse.HttpStatusCode}")}");
                return;
            }

            ProcessConverseResponse(segment, converseRequest, converseResponse, agent, modelId);
        }
    }

    private void ProcessConverseResponse(ISegment segment, dynamic converseRequest, dynamic converseResponse, IAgent agent, string requestModelId)
    {
        // if request message content doesn't have a non-null Text property, we can't support instrumentation
        // last message is the current prompt
        var requestMessage = converseRequest?.Messages?[converseRequest.Messages.Count - 1];
        if (converseRequest == null || requestMessage == null || requestMessage.Content == null || requestMessage.Content.Count == 0 || requestMessage.Content[0].Text == null)
        {
            agent.Logger.Info($"Unable to process Converse response for model {requestModelId}: request was null or message content was not Text");
            return;
        }

        if (converseResponse == null)
        {
            agent.Logger.Warn($"Error processing Converse response for model {requestModelId}: response was null");
            return;
        }

        // if response message content doesn't have a non-null Text property, we can't support instrumentation
        var responseMessage = converseResponse.Output?.Message;
        if (responseMessage == null || responseMessage.Content == null || responseMessage.Content.Count == 0 || responseMessage.Content[0].Text == null)
        {
            agent.Logger.Info($"Unable to process Converse response for model {requestModelId}: response was null or message content was not Text");
            return;
        }

        string requestRole = requestMessage.Role?.Value ?? "user";
        string promptText = requestMessage.Content?[0]?.Text ?? "";

        string responseRole = responseMessage.Role?.Value ?? "assistant";
        string responseText = responseMessage.Content?[0]?.Text ?? "";
        string stopReason = converseResponse.StopReason?.Value;

        string requestId = converseResponse.ResponseMetadata?.RequestId;
        int? requestMaxTokens = converseRequest.InferenceConfig?.MaxTokens;
        float? requestTemperature = converseRequest.InferenceConfig?.Temperature;

        int? inputTokens = converseResponse.Usage?.InputTokens;
        int? outputTokens = converseResponse.Usage?.OutputTokens;

        var completionId = EventHelper.CreateChatCompletionEvent(
            agent,
            segment,
            requestId,
            requestTemperature,
            requestMaxTokens,
            requestModelId,
            requestModelId,
            2, // one request, one response
            stopReason,
            VendorName,
            false,
            null,  // not available in AWS
            null
        );

        // Prompt
        EventHelper.CreateChatMessageEvent(
                agent,
                segment,
                requestId,
                null,
                requestModelId,
                promptText,
                requestRole,
                0,
                completionId,
                false,
                VendorName,
                inputTokens);

        // Response
        EventHelper.CreateChatMessageEvent(
            agent,
            segment,
            requestId,
            null,
            requestModelId,
            responseText,
            responseRole,
            1,
            completionId,
            true,
            VendorName,
            outputTokens);
    }

    private void HandleError(ISegment segment, dynamic converseRequest, Task responseTask, IAgent agent, string modelId)
    {
        agent.Logger.Info($"Error processing Converse response for model {modelId}: {responseTask.Exception!.Message}");

        dynamic bedrockException = responseTask.Exception!.InnerException;
        if (bedrockException == null)
        {
            agent.Logger.Warn($"Error processing Converse response for model {modelId}: Task faulted but there was no inner exception");
            return;
        }

        var requestMessage = converseRequest?.Messages?[converseRequest.Messages.Count - 1];

        if (converseRequest == null || requestMessage == null)
        {
            agent.Logger.Warn($"Error processing Converse response for model {modelId}: request Message was null");
            return;
        }

        HttpStatusCode statusCode = bedrockException.StatusCode;
        string errorCode = bedrockException.ErrorCode;
        string errorMessage = bedrockException.Message;
        string requestId = bedrockException.RequestId;

        var errorData = new LlmErrorData
        {
            HttpStatusCode = ((int)statusCode).ToString(),
            ErrorCode = errorCode,
            ErrorParam = null, // not available in AWS
            ErrorMessage = errorMessage
        };

        string requestRole = requestMessage.Role?.Value ?? "user";
        string promptText = requestMessage.Content?[0]?.Text ?? "";
        int? requestMaxTokens = converseRequest.InferenceConfig?.MaxTokens;
        float? requestTemperature = converseRequest.InferenceConfig?.Temperature;


        var completionId = EventHelper.CreateChatCompletionEvent(
            agent,
            segment,
            requestId,
            requestTemperature,
            requestMaxTokens,
            converseRequest.ModelId,
            null,
            0,
            null,
            VendorName,
            true,
            null,
            errorData);

        // Prompt
        EventHelper.CreateChatMessageEvent(
                agent,
                segment,
                requestId,
                null,
                converseRequest.ModelId,
                promptText,
                requestRole,
                0,
                completionId,
                false,
                VendorName);
    }


    private string GetOrAddLibraryVersion(string assemblyFullName)
    {
        return _libraryVersions.GetOrAdd(assemblyFullName, VersionHelpers.GetLibraryVersion(assemblyFullName));
    }

    private static object GetTaskResult(object task)
    {
        if (((Task)task).IsFaulted)
        {
            return null;
        }

        var getResponse = _getResultFromGenericTask.GetOrAdd(task.GetType(), t => VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Result"));
        return getResponse(task);
    }
}
