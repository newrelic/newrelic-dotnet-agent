# New Relic .NET Agent Bedrock Instrumentation

## Overview

The Bedrock instrumentation wrapper provides automatic monitoring for AWS Bedrock Runtime API calls in .NET applications. It instruments LLM (Large Language Model) operations including text completion and embedding requests to create AI monitoring events with comprehensive request and response tracking. The wrapper supports multiple Bedrock foundation models including Llama 2, Claude, Titan, Cohere Command, and Jurassic.

## Instrumented Methods

### InvokeModelAsyncWrapper
- **Wrapper**: [InvokeModelAsyncWrapper.cs](InvokeModelAsyncWrapper.cs)
- **Assembly**: `AWSSDK.BedrockRuntime`
- **Type**: `Amazon.BedrockRuntime.AmazonBedrockRuntimeClient`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| [InvokeModelAsync](https://github.com/aws/aws-sdk-net/blob/main/sdk/src/Services/BedrockRuntime/Generated/_netstandard/AmazonBedrockRuntimeClient.cs) | No | Yes |

### ConverseAsyncWrapper
- **Wrapper**: [ConverseAsyncWrapper.cs](ConverseAsyncWrapper.cs)
- **Assembly**: `AWSSDK.BedrockRuntime`
- **Type**: `Amazon.BedrockRuntime.AmazonBedrockRuntimeClient`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| [ConverseAsync](https://github.com/aws/aws-sdk-net/blob/main/sdk/src/Services/BedrockRuntime/Generated/_netstandard/AmazonBedrockRuntimeClient.cs) | No | Yes |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/Bedrock/Instrumentation.xml)

## Attributes Added

The wrapper creates AI monitoring events with the following attributes:

### LLM Completion Events
- **llm.conversation_id**: Request ID from AWS response metadata
- **llm.model**: Model ID (e.g., `meta.llama2-13b-chat-v1`, `anthropic.claude-v2`)
- **request.model**: Same as llm.model
- **request.temperature**: Temperature parameter from request (if provided)
- **request.max_tokens**: Max tokens parameter from request (if provided)
- **response.number_of_messages**: Total message count (prompt + responses)
- **response.choices.finish_reason**: Stop reason from model response
- **vendor**: Set to "Bedrock"
- **ingest_source**: Set to "DotNet"
- **error**: Boolean indicating if request failed
- **http_status_code**: HTTP status code from error responses
- **error.code**: AWS error code from exception
- **error.message**: Error message from exception

### LLM Message Events
- **id**: Request ID from AWS response metadata
- **request.model**: Model ID
- **content**: Message text (prompt or response)
- **role**: Message role ("user" for prompts, "assistant" for responses)
- **sequence**: Message sequence number in conversation
- **completion_id**: Links message to its completion event
- **is_response**: Boolean indicating if message is a response
- **vendor**: Set to "Bedrock"
- **token_count**: Token count for message (Converse API only)

### LLM Embedding Events
- **id**: Request ID from AWS response metadata
- **input**: Input text for embedding
- **request.model**: Model ID (e.g., `amazon.titan-embed-text-v1`)
- **vendor**: Set to "Bedrock"
- **error**: Boolean indicating if request failed
- **http_status_code**: HTTP status code from error responses
- **error.code**: AWS error code from exception
- **error.message**: Error message from exception

## Model Support

### InvokeModelAsync
Supports the following Bedrock foundation models:
- **Llama 2**: Meta's Llama 2 models (completion)
- **Claude**: Anthropic's Claude models (completion)
- **Titan**: Amazon Titan text models (completion)
- **Titan Embeddings**: Amazon Titan embedding models (embedding)
- **Cohere Command**: Cohere's Command models (completion)
- **Jurassic**: AI21 Labs' Jurassic models (completion)

### ConverseAsync
Supports all Bedrock models compatible with the Converse API for text completion. Only supports text content (not image or tool use content types).

## Event Creation

### Completion Flow
1. Creates a custom segment named `Llm/completion/Bedrock/InvokeModelAsync` or `Llm/completion/Bedrock/ConverseAsync`
2. Captures request and response payloads by deserializing model-specific JSON formats
3. Creates one `LlmChatCompletionSummary` event with request parameters and response metadata
4. Creates one `LlmChatCompletionMessage` event for the user prompt
5. Creates one or more `LlmChatCompletionMessage` events for assistant responses

### Embedding Flow (InvokeModelAsync only)
1. Creates a custom segment named `Llm/embedding/Bedrock/InvokeModelAsync`
2. Creates one `LlmEmbedding` event with input text and model information

### Error Handling
- Captures AWS Bedrock exceptions and creates events with error data
- Logs warnings for null responses or non-success HTTP status codes
- Continues creating events even on error to maintain observability

## Supportability Metrics

The wrapper records the following supportability metrics:
- **DotNet/ML/Bedrock/{version}**: Tracks AWS SDK version usage
- **DotNet/LLM/Bedrock-Invoke**: Tracks InvokeModelAsync API calls
- **DotNet/LLM/Bedrock-Converse**: Tracks ConverseAsync API calls
- **Supportability/DotNet/ML/Bedrock/{modelId}**: Tracks usage by specific model ID

## Configuration

AI monitoring must be enabled for instrumentation to function:
- Set `ai_monitoring.enabled=true` in agent configuration
- If disabled, wrappers return no-op delegates and skip all processing

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0