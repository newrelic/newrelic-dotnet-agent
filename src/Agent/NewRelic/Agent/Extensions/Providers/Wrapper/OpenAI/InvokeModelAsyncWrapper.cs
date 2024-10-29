// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.Llm;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using System.Net;
using System.IO;
using NewRelic.Agent.Extensions.JsonConverters;
using NewRelic.Agent.Extensions.JsonConverters.OpenAIPayloads;
using System.Linq;
using System.Reflection;

namespace NewRelic.Providers.Wrapper.OpenAI
{
    public class InvokeModelAsyncWrapper : IWrapper
    {
        public bool IsTransactionRequired => true; // part of spec, only create events for transactions.

        private static ConcurrentDictionary<Type, Func<object, object>> _getResultFromGenericTask = new();
        private static ConcurrentDictionary<string, string> _libraryVersions = new();
        private const string WrapperName = "OpenAIInvokeModelAsync";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            //agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"agent.Configuration.AiMonitoringEnabled {agent.Configuration.AiMonitoringEnabled}");

            // Don't do anything, including sending the version Supportability metric, if we're disabled
            if (!agent.Configuration.AiMonitoringEnabled)
            {
                return Delegates.NoOp;
            }

            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            /*agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"Invoking model {ObjectDumper.Dump(instrumentedMethodCall.MethodCall.MethodArguments)}");
            if (instrumentedMethodCall.MethodCall.MethodArguments.Length > 0)
            {
                for (int i = 0; i < instrumentedMethodCall.MethodCall.MethodArguments.Length; i++)
                {
                    agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"get model arguments {ObjectDumper.Dump(instrumentedMethodCall.MethodCall.MethodArguments[i])}");
                }
            }*/

            dynamic invokeModelRequest = instrumentedMethodCall.MethodCall.MethodArguments[0];
            var operationType = "completion";// invokeModelRequest.ModelId.Contains("embed") ? "embedding" : "completion";
            var segment = transaction.StartCustomSegment(
                instrumentedMethodCall.MethodCall,
                "Llm/" + operationType + "/OpenAI/" + instrumentedMethodCall.MethodCall.Method.MethodName
            );

            // required per spec
            var version = GetOrAddLibraryVersion(instrumentedMethodCall.MethodCall.Method.Type.Assembly.ManifestModule.Assembly.FullName);
            agent.RecordSupportabilityMetric("DotNet/ML/OpenAI/" + version);

            return Delegates.GetAsyncDelegateFor<Task>(
                agent,
                segment,
                false,
                InvokeTryProcessResponse,
                TaskContinuationOptions.ExecuteSynchronously
            );

            void InvokeTryProcessResponse(Task responseTask)
            {
                if (responseTask.IsFaulted)
                {
                    HandleError(segment, invokeModelRequest, responseTask, agent);
                    segment.End();
                    return;
                }

                // We need the duration, so we end the segment before creating the events.
                segment.End();

                dynamic invokeModelResponse = GetTaskResult(responseTask);
                if (invokeModelResponse == null)// || invokeModelResponse.HttpStatusCode >= HttpStatusCode.MultipleChoices)
                {
                    //agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error invoking model {invokeModelRequest.ModelId}: Response payload {(invokeModelResponse == null ? "is null" : $"has non-success HttpStatusCode: {invokeModelResponse.HttpStatusCode}")}");
                    agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error invoking model 'completion': Response payload {ObjectDumper.Dump(invokeModelResponse)}");
                    return;
                }

                ProcessInvokeModel(segment, invokeModelRequest, invokeModelResponse, agent);
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


        private void ProcessInvokeModel(ISegment segment, dynamic invokeModelRequest, dynamic invokeModelResponse, IAgent agent)
        {
            //agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"invokeModelRequest {invokeModelRequest}");
            //agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"invokeModelResponse {invokeModelResponse}");

            //agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"invokeModelResponse {invokeModelResponse}");

            //if (invokeModelRequest.ToString() == "System.ClientModel.ClientResult`1[OpenAI.Chat.ChatCompletion]")
            //{
            //    agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"requestPayload {ObjectDumper.Dump(invokeModelResponse.Content)}");
            //}

            var requestPayload = GetRequestPayload(invokeModelRequest, agent);
            if (requestPayload == null)
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error invoking model {invokeModelRequest}: Could not deserialize request payload");
                return;
            }
            //agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"requestPayload {ObjectDumper.Dump(requestPayload)}");

            //if (invokeModelResponse.ToString().Contains("OpenAI.Chat.ChatCompletion"))
            //{
            //    agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"invokeModelResponse- {ObjectDumper.Dump(invokeModelResponse)}");
            //}
            //var responsePayload = GetResponsePayload(invokeModelRequest.ModelId, invokeModelResponse);
            var responsePayload = GetResponsePayload("completion", invokeModelResponse, agent);
            if (responsePayload == null)
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error invoking model {invokeModelResponse}: Could not deserialize response payload");
                return;
            }
            //agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"responsePayload {ObjectDumper.Dump(responsePayload)}");

            // Embedding - does not create the other events
            /*if (((string)invokeModelRequest.ModelId).FromModelId() == LlmModelType.TitanEmbedded)
            {
                EventHelper.CreateEmbeddingEvent(
                    agent,
                    segment,
                    invokeModelResponse.ResponseMetadata.RequestId,
                    requestPayload.Prompt,
                    invokeModelRequest.ModelId,
                    invokeModelRequest.ModelId,
                    "OpenAI",
                    false,
                    null, // not available in AWS
                    null
                );

                return;
            }*/

            string finishReason = "";
            if (responsePayload.Choices.Length > 0)
            {
                finishReason = responsePayload.Choices[0].FinishReason;
            }
            var completionId = EventHelper.CreateChatCompletionEvent(
                agent,
                segment,
                responsePayload.Id,
                0,//requestPayload.Temperature,
                responsePayload.Usage.TotalTokens,
                requestPayload.Model,
                responsePayload.Model,
                1 + responsePayload.Choices.Length,
                finishReason,
                "OpenAI",
                false,
                null,
                null
            );

            // Prompt
            EventHelper.CreateChatMessageEvent(
                    agent,
                    segment,
                    responsePayload.Id,
                    responsePayload.Model,
                    requestPayload.Messages[0].Content,
                    requestPayload.Messages[0].Role,
                    0,
                    completionId,
                    false,
                    "openai");

            // Responses
            for (var i = 0; i < responsePayload.Choices.Length; i++)
            {
                EventHelper.CreateChatMessageEvent(
                    agent,
                    segment,
                    responsePayload.Id,
                    responsePayload.Model,//invokeModelRequest.ModelId,
                    responsePayload.Choices[i].Message.Content,
                    responsePayload.Choices[i].Message.Role,
                    i + 1,
                    completionId,
                    true,
                    "openai");
            }
        }

        private void HandleError(ISegment segment, dynamic invokeModelRequest, Task responseTask, IAgent agent)
        {
            agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"Error invoking OpenAI model {invokeModelRequest}: {responseTask.Exception!.Message}");

            dynamic OpenAIException = responseTask.Exception!.InnerException;
            if (OpenAIException == null)
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error invoking model {invokeModelRequest}: Task faulted but there was no inner exception");
                return;
            }

            var requestPayload = GetRequestPayload(invokeModelRequest, agent);
            if (requestPayload == null)
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error invoking model {invokeModelRequest}: Could not deserialize request payload");
                return;
            }

            HttpStatusCode statusCode = OpenAIException.StatusCode;
            string errorCode = OpenAIException.ErrorCode;
            string errorMessage = OpenAIException.Message;
            string requestId = OpenAIException.RequestId;

            var errorData = new LlmErrorData
            {
                HttpStatusCode = ((int)statusCode).ToString(),
                ErrorCode = errorCode,
                ErrorParam = null, // not available in AWS
                ErrorMessage = errorMessage
            };

            /*if (((string)invokeModelRequest.ModelId).FromModelId() == LlmModelType.TitanEmbedded)
            {
                EventHelper.CreateEmbeddingEvent(
                    agent,
                    segment,
                    requestId,
                    requestPayload.Prompt,
                    invokeModelRequest.ModelId,
                    null,
                    "OpenAI",
                    true,
                    null,
                    errorData);
            }
            else
            {*/
            var completionId = EventHelper.CreateChatCompletionEvent(
                agent,
                segment,
                requestId,
                0,//requestPayload.Temperature,
                0,//requestPayload.MaxTokens,
                requestPayload.Model,
                null,
                0,
                null,
                "OpenAI",
                true,
                null,
                errorData);

            // Prompt
            EventHelper.CreateChatMessageEvent(
                    agent,
                    segment,
                    requestId,
                    requestPayload.Model,
                    requestPayload.Messages[0].Content,
                    "user",
                    0,
                    completionId,
                    false,
                    "openai");
            //}
        }

        private static IRequestPayload GetRequestPayload(dynamic invokeModelRequest, IAgent agent)
        {
            var model = "request";

            GPTRequestPayload rp = new GPTRequestPayload();

            if (invokeModelRequest.ToString().Contains("OpenAI.Chat.ChatCompletionOptions"))
            {
                MemoryStream bodyStream = new MemoryStream();
                invokeModelRequest.WriteTo(bodyStream);
                var curPos = bodyStream.Position;
                bodyStream.Position = 0;
                var sr = new StreamReader(bodyStream);
                string utf8Json = sr.ReadToEnd();
                bodyStream.Position = curPos;
                agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"request utf8Json 1: {utf8Json}");

                return WrapperHelpers.DeserializeObject<GPTRequestPayload>(utf8Json);
            }
            /*else if (invokeModelRequest.ToString() == "System.ClientModel.BinaryContent")
             {
                 dynamic requestContent = invokeModelRequest.Data;
                 string utf8Json = requestContent.ToString();
                 agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"request utf8Json 2: {utf8Json}");

                 rp.Model = model + "BinaryContent";
                 return null;
             }
             else if (invokeModelRequest.ToString() == "OpenAI.Chat.ChatMessage[]")
             {
                 rp.Model = model + "ChatMessage";
                 dynamic requestContent = invokeModelRequest;
                 string utf8Json = requestContent.ToString();
                 agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"request utf8Json 3: {utf8Json}");
                 return null;
             }*/

            return null;
        }

        private static IResponsePayload GetResponsePayload(string modelId, dynamic invokeModelResponse, IAgent agent)
        {
            GPTResponsePayload rp = new GPTResponsePayload();

            if (invokeModelResponse.ToString().Contains("OpenAI.Chat.ChatCompletion"))
            {
                dynamic responseContent = invokeModelResponse.GetRawResponse().Content;
                string utf8Json = responseContent.ToString();
                agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"response utf8Json: {utf8Json}");

                return WrapperHelpers.DeserializeObject<GPTResponsePayload>(utf8Json);
            }
            else if (invokeModelResponse.ToString() == "System.ClientModel.ClientResult")
            {
                dynamic responseContent = invokeModelResponse.GetRawResponse().Content;
                string utf8Json = responseContent.ToString();
                agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"response utf8Json: {utf8Json}");

                return WrapperHelpers.DeserializeObject<GPTResponsePayload>(utf8Json);
            }

            return null;
        }
    }

    /// <summary>
    /// The set of models supported by the OpenAI wrapper.
    /// </summary>
    public enum LlmModelType
    {
        GPT,
    }

    public static class LlmModelTypeExtensions
    {
        /// <summary>
        /// Converts a modelId to an LlmModelType. Throws an exception if the modelId is unknown.
        /// </summary>
        /// <param name="modelId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static LlmModelType FromModelId(this string modelId)
        {
            if (modelId.StartsWith("gpt"))
                return LlmModelType.GPT;

            throw new Exception($"Unknown model: {modelId}");
        }
    }

    public class ObjectDumper
    {
        public static string Dump(object obj)
        {
            return new ObjectDumper().DumpObject(obj);
        }

        System.Text.StringBuilder _dumpBuilder = new System.Text.StringBuilder();

        string DumpObject(object obj)
        {
            DumpObject(obj, 0);
            return _dumpBuilder.ToString();
        }

        void DumpObject(object obj, int nestingLevel = 0)
        {
            try
            {
                var nestingSpaces = "".PadLeft(nestingLevel * 4);

                if (obj == null)
                {
                    _dumpBuilder.AppendFormat("{0}null\n", nestingSpaces);
                }
                else if (obj is string || obj.GetType().IsPrimitive)
                {
                    _dumpBuilder.AppendFormat("{0}{1}\n", nestingSpaces, obj);
                }
                else if (ImplementsDictionary(obj.GetType()))
                {
                    using (var e = ((dynamic)obj).GetEnumerator())
                    {
                        var enumerator = (System.Collections.IEnumerator)e;
                        while (enumerator.MoveNext())
                        {
                            dynamic p = enumerator.Current;

                            var key = p.Key;
                            var value = p.Value;
                            _dumpBuilder.AppendFormat("{0}{1} ({2})\n", nestingSpaces, key, value != null ? value.GetType().ToString() : "<null>");
                            DumpObject(value, nestingLevel + 1);
                        }
                    }
                }
                else if (obj is System.Collections.IEnumerable)
                {
                    foreach (dynamic p in obj as System.Collections.IEnumerable)
                    {
                        DumpObject(p, nestingLevel);
                    }
                }
                else
                {
                    foreach (System.ComponentModel.PropertyDescriptor descriptor in System.ComponentModel.TypeDescriptor.GetProperties(obj))
                    {
                        string name = descriptor.Name;
                        object value = descriptor.GetValue(obj);

                        _dumpBuilder.AppendFormat("{0}{1} ({2})\n", nestingSpaces, name, value != null ? value.GetType().ToString() : "<null>");
                        DumpObject(value, nestingLevel + 1);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ObjectDumper exception: {ex}");
            }
        }

        bool ImplementsDictionary(Type t)
        {
            return t.GetInterfaces().Any(i => i.Name.Contains("IDictionary"));
        }
    }
}
