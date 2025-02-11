// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
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

    public bool IsTransactionRequired => true; // part of spec, only create events for transactions.

    private static ConcurrentDictionary<Type, Func<object, object>> _getResultFromGenericTask = new();
    private static ConcurrentDictionary<string, string> _libraryVersions = new();
    private const string WrapperName = "OpenAiChat";
    private const string VendorName = "openai";

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

        _modelFieldAccessor ??= VisibilityBypasser.Instance.GenerateFieldReadAccessor<string>(instrumentedMethodCall.MethodCall.InvocationTarget.GetType(), "_model");

        if (instrumentedMethodCall.IsAsync)
        {
            transaction.AttachToAsync();
        }

        Array chatMessages = instrumentedMethodCall.MethodCall.MethodArguments[0] as Array;
        
        if (chatMessages == null || chatMessages.Length == 0)
        {
            agent.Logger.Debug("Ignoring chat completion: No chat messages found");
            return Delegates.NoOp;
        }

        var lastMessage = ((dynamic)chatMessages.GetValue(chatMessages.Length - 1));
        if (lastMessage.Content == null ||
            lastMessage.Content.Count == 0)
        {
            agent.Logger.Debug("Ignoring chat completion: No content found in chat messages");
            return Delegates.NoOp;
        }

        // we only support text completions. Possible values are Text, Image and Refusal
        var completionType = lastMessage.Content[0].Kind.ToString();
        if (completionType != "Text")
        {
            agent.Logger.Debug($"Ignoring chat completion: Only text completions are supported, but got {completionType}");
            return Delegates.NoOp;
        }

        dynamic chatCompletionOptions = instrumentedMethodCall.MethodCall.MethodArguments[1]; // may be null

        var operationType = "completion";
        var methodMethodName = $"Llm/{operationType}/{VendorName}/{instrumentedMethodCall.MethodCall.Method.MethodName}";
        var segment = transaction.StartCustomSegment(instrumentedMethodCall.MethodCall, methodMethodName);

        // required per spec
        var version = GetOrAddLibraryVersion(instrumentedMethodCall.MethodCall.Method.Type.Assembly.ManifestModule.Assembly.FullName);
        agent.RecordSupportabilityMetric($"DotNet/ML/{VendorName}/{version}");

        string model = _modelFieldAccessor(instrumentedMethodCall.MethodCall.InvocationTarget);

        if (instrumentedMethodCall.IsAsync) // TODO: do we need to check the method name for an "Async" suffix?
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


    private void ProcessResponse(ISegment segment, string model, dynamic chatMessages, dynamic clientResult, dynamic chatCompletionOptions, IAgent agent)
    {
        dynamic chatCompletionResponse = clientResult.Value;
        string finishReason = chatCompletionResponse.FinishReason.ToString();

        HandleSuccess(segment, model, chatMessages, clientResult, chatCompletionOptions, agent, chatCompletionResponse, finishReason);
    }

    private static void HandleSuccess(ISegment segment, string requestModel, dynamic chatMessages, dynamic clientResult, dynamic chatCompletionOptions, IAgent agent, dynamic chatCompletionResponse, string finishReason)
    {
        _rolePropertyAccessor ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("OpenAI", "OpenAI.Chat.UserChatMessage", "Role");
        _responseFieldGetter ??= VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>("System.ClientModel", "System.ClientModel.ClientResult", "_response");

        // typically there is only a single message in the outbound chat message list, but in a conversation, there can be multiple prompt and response message.
        // The last message is the most recent prompt
        // There can also be a refusal, which means there won't be a response message
        dynamic lastChatMessage = chatMessages[chatMessages.Length - 1];

        string refusal = chatCompletionResponse.Refusal;
        string requestPrompt = refusal ?? lastChatMessage.Content[0].Text;

        // roles need to be lowercase, but we're pulling the enum members by name so we need to lowercase them
        string requestRole = _rolePropertyAccessor(lastChatMessage).ToString().ToLower();
        string responseRole = chatCompletionResponse.Role.ToString().ToLower();

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

        int numMessages = 1;

        // if finishReason = "Stop", then there is a response message
        // otherwise, there won't be any response message
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
            VendorName,
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
            responseModel, // TODO: not sure why we send response model instead of request model here, but that's what the spec says
            requestPrompt,
            requestRole,
            0,
            completionId,
            false,
            VendorName,
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
                VendorName,
                outputTokenCount);
        }
    }

    private static Dictionary<string, string> GetResponseHeaders(dynamic clientResult)
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
        Func<object, object> exceptionResponseFieldGetter =
            VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>(innerException.GetType(), "_response");
        var exceptionResponseField = exceptionResponseFieldGetter(innerException);
        var headers = exceptionResponseField.Headers;
        string requestId = null;
        foreach (var header in headers)
        {
            if (header.Key == "X-Request-ID")
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
            VendorName,
            true,
            null,
            errorData);
    }
}
