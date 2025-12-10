// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.Llm;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.OpenAI;

public class OpenAiChatWrapper : IWrapper
{
    private static Func<object, object> _rolePropertyAccessor;
    private static Func<object, string> _modelFieldAccessor;
    private static Func<object, object> _responseFieldGetter;
    private static Func<object, object> _exceptionResponseFieldGetter;

    private static ConcurrentDictionary<object, string> _roleEnumStringCache = new();

    public bool IsTransactionRequired => true; // part of spec, only create events for transactions.

    private static ConcurrentDictionary<Type, Func<object, object>> _getResultFromGenericTask = new();
    private static ConcurrentDictionary<string, string> _libraryVersions = new();
    private const string WrapperName = "OpenAiChat";
    private const string OpenAIVendorName = "openai";
    private const string AzureOpenAIVendorName = "azureopenai";
    private bool _isAzureOpenAI = false;

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

        var invocationTargetType = instrumentedMethodCall.MethodCall.InvocationTarget.GetType();
        // Azure.OpenAI.ChatClient inherits from OpenAI.Chat.ChatClient, so we need to access the _model property from the base class
        if (invocationTargetType.BaseType != null && invocationTargetType.BaseType.FullName == "OpenAI.Chat.ChatClient")
        {
            agent.Logger.Debug("Instrumenting Azure.OpenAI.AzureChatClient.");
            invocationTargetType = invocationTargetType.BaseType;
            _isAzureOpenAI = true;
        }

        _modelFieldAccessor ??= VisibilityBypasser.Instance.GenerateFieldReadAccessor<string>(invocationTargetType, "_model");

        var isAsync = instrumentedMethodCall.IsAsync || instrumentedMethodCall.InstrumentedMethodInfo.Method.MethodName.EndsWith("Async");

        if (isAsync)
        {
            transaction.AttachToAsync();
        }

        string model = _modelFieldAccessor(instrumentedMethodCall.MethodCall.InvocationTarget);

        // capture metrics prior to validation so we can track usage even if we don't create events
        RecordLlmMetrics(instrumentedMethodCall, agent, model);

        // get the chat messages from the first argument and validate
        if (!GetAndValidateChatMessages(instrumentedMethodCall, agent, out var chatMessages))
        {
            return Delegates.NoOp;
        }

        dynamic chatCompletionOptions = instrumentedMethodCall.MethodCall.MethodArguments[1]; // may be null

        var operationType = "completion";
        var methodMethodName = $"Llm/{operationType}/{GetVendorName()}/{instrumentedMethodCall.MethodCall.Method.MethodName}";
        var segment = transaction.StartCustomSegment(instrumentedMethodCall.MethodCall, methodMethodName);

        if (isAsync)
        {
            return Delegates.GetAsyncDelegateFor<Task>(
                agent,
                segment,
                false,
                TryProcessAsyncResponse,
                TaskContinuationOptions.ExecuteSynchronously
            );
        }

        return Delegates.GetDelegateFor<dynamic>(onSuccess: (clientResult) =>
        {
            segment.End();
            ProcessResponse(segment, model, chatMessages, clientResult, chatCompletionOptions, agent);
        });

        void TryProcessAsyncResponse(Task responseTask)
        {
            segment.End();

            if (responseTask.IsFaulted)
            {
                HandleError(segment, model, responseTask, agent);
                return;
            }

            dynamic clientResult = GetTaskResult(responseTask);
            if (clientResult == null)
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error processing response: Response payload is null");
                return;
            }

            ProcessResponse(segment, model, chatMessages, clientResult, chatCompletionOptions, agent);
        }
    }

    private void RecordLlmMetrics(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, string model)
    {
        // required per spec
        var version = GetOrAddLibraryVersion(instrumentedMethodCall.MethodCall.Method.Type.Assembly.ManifestModule.Assembly.FullName);
        agent.RecordSupportabilityMetric($"DotNet/ML/{GetVendorName()}/{version}");

        SupportabilityHelpers.CreateModelIdSupportabilityMetricsForOpenAi(model, agent); // prepend vendor name to model id

        // useful for tracking LLM usage by vendor
        agent.RecordSupportabilityMetric($"DotNet/LLM/{GetVendorName()}-Chat");
    }

    private static bool GetAndValidateChatMessages(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, out List<dynamic> chatMessages)
    {
        var chatMessagesEnumerable = instrumentedMethodCall.MethodCall.MethodArguments[0] as System.Collections.IEnumerable;
        if (chatMessagesEnumerable == null)
        {
            agent.Logger.Debug("Ignoring chat completion: No chat messages found");
            chatMessages = null;
            return false;
        }

        // Materialize once to avoid multiple enumeration and enable indexing
        chatMessages = chatMessagesEnumerable.Cast<dynamic>().ToList();
        if (chatMessages.Count == 0)
        {
            agent.Logger.Debug("Ignoring chat completion: No chat messages found");
            return false;
        }

        var lastMessage = chatMessages.Last();
        if (lastMessage.Content == null || lastMessage.Content.Count == 0)
        {
            agent.Logger.Debug("Ignoring chat completion: No content found in chat messages");
            return false;
        }

        // we only support text completions. Possible values are Text, Image and Refusal
        var completionType = lastMessage.Content[0].Kind.ToString();
        if (completionType != "Text")
        {
            agent.Logger.Debug($"Ignoring chat completion: Only text completions are supported, but got {completionType}");
            return false;
        }

        return true;
    }

    private string GetVendorName() => _isAzureOpenAI ? AzureOpenAIVendorName : OpenAIVendorName;

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


    private void ProcessResponse(ISegment segment, string model, List<dynamic> chatMessages, dynamic clientResult, dynamic chatCompletionOptions, IAgent agent)
    {
        dynamic chatCompletionResponse = clientResult.Value;
        string finishReason = chatCompletionResponse.FinishReason.ToString();

        HandleSuccess(segment, model, chatMessages, clientResult, chatCompletionOptions, agent, chatCompletionResponse, finishReason);
    }

    private void HandleSuccess(ISegment segment, string requestModel, List<dynamic> chatMessages, dynamic clientResult, dynamic chatCompletionOptions, IAgent agent, dynamic chatCompletionResponse, string finishReason)
    {
        _rolePropertyAccessor ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("OpenAI", "OpenAI.Chat.UserChatMessage", "Role");
        _responseFieldGetter ??= VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>("System.ClientModel", "System.ClientModel.ClientResult", "_response");

        // typically there is only a single message in the outbound chat message list, but in a conversation, there can be multiple prompt and response message.
        // The last message is the most recent prompt
        // There can also be a refusal, which means there won't be a response message
        dynamic lastChatMessage = chatMessages.Last();

        string refusal = chatCompletionResponse.Refusal;
        string requestPrompt = refusal ?? lastChatMessage.Content[0].Text;

        string requestRole = GetRoleEnumString(_rolePropertyAccessor(lastChatMessage));
        string responseRole = GetRoleEnumString(chatCompletionResponse.Role);

        string responseId = chatCompletionResponse.Id;
        string responseModel = chatCompletionResponse.Model;

        Dictionary<string, string> headersDictionary = GetResponseHeaders(clientResult);
        var llmHeaders = headersDictionary.GetOpenAiHeaders();
        string organization = headersDictionary.TryGetOpenAiOrganization();
        string requestId = headersDictionary.TryGetRequestId();

        var inputTokenCount = chatCompletionResponse.Usage.InputTokenCount;
        var outputTokenCount = chatCompletionResponse.Usage.OutputTokenCount;

        var temperature = chatCompletionOptions != null ? (float?)chatCompletionOptions.Temperature : null;
        var maxOutputTokenCount = chatCompletionOptions != null ? (int?)chatCompletionOptions.MaxOutputTokenCount : null;


        // if finishReason = "Stop", then there is a response message
        // otherwise, there won't be any response message
        int numMessages = 1;
        string responseContent = null;
        if (finishReason == "Stop")
        {
            numMessages = 2;
            responseContent = chatCompletionResponse.Content[0].Text;
        }

        string completionId = EventHelper.CreateChatCompletionEvent(
            agent,
            segment,
            requestId,
            temperature,
            maxOutputTokenCount,
            requestModel,
            responseModel,
            numMessages,
            finishReason,
            GetVendorName(),
            false,
            llmHeaders,
            null,
            organization
        );

        // Prompt
        EventHelper.CreateChatMessageEvent(
            agent,
            segment,
            requestId,
            responseId,
            responseModel,
            requestPrompt,
            requestRole,
            0,
            completionId,
            false,
            GetVendorName(),
            inputTokenCount);

        // Response
        if (finishReason == "Stop")
        {
            EventHelper.CreateChatMessageEvent(
                agent,
                segment,
                requestId,
                responseId,
                responseModel,
                responseContent,
                responseRole,
                1,
                completionId,
                true,
                GetVendorName(),
                outputTokenCount);
        }
    }

    private string GetRoleEnumString(dynamic roleEnumVal)
    {
        // roles need to be lowercase
        return _roleEnumStringCache.GetOrAdd(roleEnumVal, roleEnumVal.ToString().ToLower());
    }

    private Dictionary<string, string> GetResponseHeaders(dynamic clientResult)
    {
        dynamic responseField = _responseFieldGetter(clientResult);
        var headers = responseField.Headers;

        var headersDictionary = new Dictionary<string, string>();
        foreach (var header in headers)
        {
            string headerKey = header.Key;
            var headerValue = header.Value as string;
            headersDictionary.Add(headerKey.ToLower(), headerValue);
        }

        return headersDictionary;
    }

    private void HandleError(ISegment segment, string model, Task responseTask, IAgent agent)
    {
        agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"Error invoking OpenAI model {model}: {responseTask.Exception!.Message}");
        agent.CurrentTransaction.NoticeError(responseTask.Exception);

        dynamic innerException = responseTask.Exception!.InnerException;
        if (innerException == null)
        {
            agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error invoking model {model}: Task faulted but there was no inner exception");
            return;
        }

        int statusCode = innerException.Status;
        string errorMessage = innerException.Message;

        // requestID is buried in the headers of the _response field
        _exceptionResponseFieldGetter ??= VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>(innerException.GetType(), "_response");
        var exceptionResponseField = _exceptionResponseFieldGetter(innerException);
        var headers = exceptionResponseField.Headers;
        string requestId = null;
        foreach (var header in headers)
        {
            if (((string)header.Key).ToLower() == "x-request-id")
            {
                requestId = header.Value;
                break;
            }
        }

        var errorData = new LlmErrorData
        {
            HttpStatusCode = statusCode.ToString(),
            ErrorCode = null,
            ErrorParam = null,
            ErrorMessage = errorMessage
        };

        EventHelper.CreateChatCompletionEvent(
            agent,
            segment,
            requestId,
            null,
            null,
            model,
            null,
            0,
            null,
            GetVendorName(),
            true,
            null,
            errorData);
    }
}
