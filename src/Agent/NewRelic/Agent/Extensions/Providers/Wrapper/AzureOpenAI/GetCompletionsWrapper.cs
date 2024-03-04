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
    public class GetCompletionsWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        private const string WrapperName = "AzureOpenAIGetCompletions";

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

            var completionOptions = (CompletionsOptions)instrumentedMethodCall.MethodCall.MethodArguments[0];
            var segment = transaction.StartCustomSegment(
                instrumentedMethodCall.MethodCall,
                "Llm/completion/OpenAI/" + instrumentedMethodCall.MethodCall.Method.MethodName
            );

            var version = VersionHelpers.GetLibraryVersion(instrumentedMethodCall.MethodCall.Method.Type.Assembly.ManifestModule.Assembly.FullName);
            agent.RecordSupportabilityMetric("DotNet/ML/OpenAI/" + version);

            if (instrumentedMethodCall.IsAsync)
            {
                return Delegates.GetAsyncDelegateFor<Task<Response<Completions>>>(
                    agent,
                    segment,
                    false,
                    InvokeTryProcessResponseAsync,
                    TaskContinuationOptions.RunContinuationsAsynchronously
                );
            }
            else
            {
                return Delegates.GetDelegateFor<Response<Completions>>(
                    onSuccess: InvokeTryProcessResponse,
                    onFailure: HandleError
                );
            }

            void InvokeTryProcessResponseAsync(Task<Response<Completions>> responseTask)
            {
                if (responseTask.IsFaulted)
                {
                    segment.End();
                    _ = EventHelper.CreateChatCompletionEvent(
                        agent,
                        segment,
                        null,
                        completionOptions.Temperature.HasValue ? completionOptions.Temperature.Value : 1.0f,
                        completionOptions.MaxTokens.HasValue ? completionOptions.MaxTokens.Value : int.MaxValue,
                        completionOptions.DeploymentName,
                        completionOptions.DeploymentName,
                        completionOptions.Prompts.Count,
                        null,
                        "openai",
                        null,
                        responseTask.Exception
                    );
                    return;
                }

                InvokeTryProcessResponse(responseTask.Result);
            }

            void InvokeTryProcessResponse(Response<Completions> responseCompletions)
            {
                // We need the duration so we end the segment before creating the events.
                segment.End();

                ProcessInvokeModel(segment, completionOptions, responseCompletions, agent);
            }

            void HandleError(Exception exception)
            {
                _ = EventHelper.CreateChatCompletionEvent(
                    agent,
                    segment,
                    null,
                    completionOptions.Temperature.HasValue ? completionOptions.Temperature.Value : 1.0f,
                    completionOptions.MaxTokens.HasValue ? completionOptions.MaxTokens.Value : int.MaxValue,
                    completionOptions.DeploymentName,
                    completionOptions.DeploymentName,
                    completionOptions.Prompts.Count,
                    null,
                    "openai",
                    null,
                    exception
                );
            }
        }

        private void ProcessInvokeModel(ISegment segment, CompletionsOptions completionOptions, Response<Completions> responseCompletions, IAgent agent)
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
                completionOptions.Temperature.HasValue ? completionOptions.Temperature.Value : 1.0f,
                completionOptions.MaxTokens.HasValue ? completionOptions.MaxTokens.Value : int.MaxValue,
                completionOptions.DeploymentName,
                completionOptions.DeploymentName,
                completionOptions.Prompts.Count + responseCompletions.Value.Choices.Count,
                responseCompletions.Value.Choices[0].FinishReason.ToString(),  // Overridden in struct to return value.
                "openai",
                headers,
                null
            );

            // Prompts
            for (int i = 0; i < completionOptions.Prompts.Count; i++)
            {
                EventHelper.CreateChatMessageEvent(
                    agent,
                    segment,
                    rawResponse.ClientRequestId,
                    completionOptions.DeploymentName,
                    completionOptions.Prompts[i],
                    string.Empty,
                    i + 1,
                    completionId,
                    -1,
                    false
                );
            }

            // Responses
            for (int i = 0; i < responseCompletions.Value.Choices.Count; i++)
            {
                EventHelper.CreateChatMessageEvent(
                    agent,
                    segment,
                    rawResponse.ClientRequestId,
                    completionOptions.DeploymentName,
                    responseCompletions.Value.Choices[i].Text,
                    string.Empty,
                    i + 1 + completionOptions.Prompts.Count,
                    completionId,
                    -1,
                    true
                );
            }
        }
    }
}
