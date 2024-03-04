// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.Llm;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AzureOpenAI
{
    public class GetChatCompletionsWrapper : IWrapper
    {
        public bool IsTransactionRequired => true; // part of spec, only create events for transactions.

        private const string WrapperName = "AzureOpenAIGetChatCompletions";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
                transaction.DetachFromPrimary(); //Remove from thread-local type storage
            }

            var chatCompletionOptions = (ChatCompletionsOptions)instrumentedMethodCall.MethodCall.MethodArguments[0];
            var segment = transaction.StartCustomSegment(
                instrumentedMethodCall.MethodCall,
                "Llm/completion/OpenAI/" + instrumentedMethodCall.MethodCall.Method.MethodName
            );

            // required per spec
            var version = VersionHelpers.GetLibraryVersion(instrumentedMethodCall.MethodCall.Method.Type.Assembly.ManifestModule.Assembly.FullName);
            agent.RecordSupportabilityMetric("DotNet/ML/OpenAI/" + version);

            if (instrumentedMethodCall.IsAsync)
            {
                return Delegates.GetAsyncDelegateFor<Task<Response<ChatCompletions>>>(
                    agent,
                    segment,
                    false,
                    InvokeTryProcessResponseAsync,
                    TaskContinuationOptions.RunContinuationsAsynchronously
                );
            }
            else
            {
                return Delegates.GetDelegateFor<Response<ChatCompletions>>(
                    onSuccess: InvokeTryProcessResponse,
                    onFailure: HandleError
                );
            }

            void InvokeTryProcessResponseAsync(Task<Response<ChatCompletions>> responseTask)
            {
                if (responseTask.IsFaulted)
                {
                    segment.End();
                    _ = EventHelper.CreateChatCompletionEvent(
                        agent,
                        segment,
                        null,
                        chatCompletionOptions.Temperature.HasValue ? chatCompletionOptions.Temperature.Value : 1.0f,
                        chatCompletionOptions.MaxTokens.HasValue ? chatCompletionOptions.MaxTokens.Value : int.MaxValue,
                        chatCompletionOptions.DeploymentName,
                        chatCompletionOptions.DeploymentName,
                        chatCompletionOptions.Messages.Count,
                        null,
                        "openai",
                        null,
                        responseTask.Exception
                    );
                    return;
                }

                InvokeTryProcessResponse(responseTask.Result);
            }

            void InvokeTryProcessResponse(Response<ChatCompletions> responseCompletions)
            {
                // We need the duration so we end the segment before creating the events.
                segment.End();

                ProcessInvokeModel(segment, chatCompletionOptions, responseCompletions, agent);
            }

            void HandleError(Exception exception)
            {
                segment.End();
                _ = EventHelper.CreateChatCompletionEvent(
                    agent,
                    segment,
                    null,
                    chatCompletionOptions.Temperature.HasValue ? chatCompletionOptions.Temperature.Value : 1.0f,
                    chatCompletionOptions.MaxTokens.HasValue ? chatCompletionOptions.MaxTokens.Value : int.MaxValue,
                    chatCompletionOptions.DeploymentName,
                    chatCompletionOptions.DeploymentName,
                    chatCompletionOptions.Messages.Count,
                    null,
                    "openai",
                    null,
                    exception
                );
            }
        }

        private void ProcessInvokeModel(ISegment segment, ChatCompletionsOptions chatCompletionOptions, Response<ChatCompletions> responseCompletions, IAgent agent)
        {
            var rawResponse = responseCompletions.GetRawResponse();
            var headers = new Dictionary<string, string>();
            foreach (var header in rawResponse.Headers)
            {
                headers.Add(header.Name, header.Value);
            }

            var completionId = EventHelper.CreateChatCompletionEvent(
                agent,
                segment,
                rawResponse.ClientRequestId,
                chatCompletionOptions.Temperature.HasValue ? chatCompletionOptions.Temperature.Value : 1.0f,
                chatCompletionOptions.MaxTokens.HasValue ? chatCompletionOptions.MaxTokens.Value : int.MaxValue,
                chatCompletionOptions.DeploymentName,
                chatCompletionOptions.DeploymentName,
                chatCompletionOptions.Messages.Count + responseCompletions.Value.Choices.Count,
                responseCompletions.Value.Choices[0].FinishReason.Value.ToString(), // Overridden in struct to return value.
                "openai",
                headers,
                null
            );

            // Prompts and System messages
            for (int i = 0; i < chatCompletionOptions.Messages.Count; i++)
            {
                var message = (dynamic)chatCompletionOptions.Messages[i];
                EventHelper.CreateChatMessageEvent(
                    agent,
                    segment,
                    rawResponse.ClientRequestId,
                    chatCompletionOptions.DeploymentName,
                    (string)message.Content,
                    string.Empty,
                    i + 1,
                    completionId,
                    null,
                    false
                );
            }

            // Responses
            for (var i = 0; i < responseCompletions.Value.Choices.Count; i++)
            {
                EventHelper.CreateChatMessageEvent(
                    agent,
                    segment,
                    rawResponse.ClientRequestId,
                    chatCompletionOptions.DeploymentName,
                    responseCompletions.Value.Choices[i].Message.Content,
                    responseCompletions.Value.Choices[i].Message.Role.ToString(),
                    i + 1 + chatCompletionOptions.Messages.Count,
                    completionId,
                    null,
                    true
                );
            }
        }
    }
}
