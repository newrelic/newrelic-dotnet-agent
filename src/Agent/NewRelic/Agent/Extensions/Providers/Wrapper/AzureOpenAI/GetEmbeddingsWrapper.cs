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
    public class GetEmbeddingsWrapper : IWrapper
    {
        public bool IsTransactionRequired => true; // part of spec, only create events for transactions.

        private const string WrapperName = "AzureOpenAIGetEmbeddings";

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

            var embeddingsOptions = (EmbeddingsOptions)instrumentedMethodCall.MethodCall.MethodArguments[0];
            var segment = transaction.StartCustomSegment(
                instrumentedMethodCall.MethodCall,
                "Llm/embedding/OpenAI/" + instrumentedMethodCall.MethodCall.Method.MethodName
            );

            // required per spec
            var version = VersionHelpers.GetLibraryVersion(instrumentedMethodCall.MethodCall.Method.Type.Assembly.ManifestModule.Assembly.FullName);
            agent.RecordSupportabilityMetric("DotNet/ML/OpenAI/" + version);

            if (instrumentedMethodCall.IsAsync)
            {
                return Delegates.GetAsyncDelegateFor<Task<Response<Embeddings>>>(
                    agent,
                    segment,
                    false,
                    InvokeTryProcessResponseAsync,
                    TaskContinuationOptions.RunContinuationsAsynchronously
                );
            }
            else
            {
                return Delegates.GetDelegateFor<Response<Embeddings>>(
                    onSuccess: InvokeTryProcessResponse,
                    onFailure: HandleError
                );
            }

            void InvokeTryProcessResponseAsync(Task<Response<Embeddings>> responseTask)
            {
                if (responseTask.IsFaulted)
                {
                    segment.End();
                    foreach (var input in embeddingsOptions.Input)
                    {
                        _ = EventHelper.CreateEmbeddingEvent(
                            agent,
                            segment,
                            null,
                            input,
                            embeddingsOptions.DeploymentName,
                            embeddingsOptions.DeploymentName,
                            "openai",
                            null,
                            null,
                            responseTask.Exception
                        );
                    }
                    return;
                }

                InvokeTryProcessResponse(responseTask.Result);
            }

            void InvokeTryProcessResponse(Response<Embeddings> responseEmbeddings)
            {
                // We need the duration so we end the segment before creating the events.
                segment.End();

                ProcessInvokeModel(segment, embeddingsOptions, responseEmbeddings, agent);
            }

            void HandleError(Exception exception)
            {
                segment.End();
                foreach (var input in embeddingsOptions.Input)
                {
                    _ = EventHelper.CreateEmbeddingEvent(
                        agent,
                        segment,
                        null,
                        input,
                        embeddingsOptions.DeploymentName,
                        embeddingsOptions.DeploymentName,
                        "openai",
                        null,
                        null,
                        exception
                    );
                }
            }
        }

        private void ProcessInvokeModel(ISegment segment, EmbeddingsOptions embeddingsOptions, Response<Embeddings> responseEmbeddings, IAgent agent)
        {
            var rawResponse = responseEmbeddings.GetRawResponse();
            var headers = new Dictionary<string, string>();
            foreach (var header in rawResponse.Headers)
            {
                headers.Add(header.Name, header.Value);
            }

            foreach (var input in embeddingsOptions.Input)
            {
                _ = EventHelper.CreateEmbeddingEvent(
                    agent,
                    segment,
                    rawResponse.ClientRequestId,
                    input,
                    embeddingsOptions.DeploymentName,
                    embeddingsOptions.DeploymentName,
                    "openai",
                    responseEmbeddings.Value.Usage.TotalTokens,
                    headers,
                    null
                );
            }
        }
    }
}
