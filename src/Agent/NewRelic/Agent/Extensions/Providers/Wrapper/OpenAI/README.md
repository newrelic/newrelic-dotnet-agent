# New Relic .NET Agent OpenAI Instrumentation

## Overview

The OpenAI instrumentation wrapper provides automatic monitoring for OpenAI .NET SDK and Azure.OpenAI chat completion operations executed within an existing transaction. It creates LLM (Large Language Model) segments, captures chat completion requests and responses, records LLM events with detailed metadata, and reports supportability metrics. The wrapper automatically detects whether the client is OpenAI or Azure OpenAI and adjusts the vendor name accordingly.

## Instrumented Methods

### OpenAiChatWrapper
- **Wrapper**: [OpenAiChatWrapper.cs](OpenAiChatWrapper.cs)
- **Assembly**: `OpenAI`
- **Type**: `OpenAI.Chat.ChatClient`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| CompleteChat | No | Yes |
| CompleteChatAsync | No | Yes |

**Note**: This instrumentation also applies to `Azure.OpenAI.AzureChatClient`, which inherits from `OpenAI.Chat.ChatClient`. The wrapper automatically detects Azure OpenAI clients and reports them with vendor name "azureopenai".

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/OpenAI/Instrumentation.xml)

## Attributes Added

The wrapper creates LLM segments and records the following events:

### LLM Completion Summary Event
- **llm.conversation_id**: Unique identifier for the conversation
- **llm.model**: Model used for completion (e.g., "gpt-4", "gpt-3.5-turbo")
- **request.model**: Requested model name
- **request.temperature**: Sampling temperature (0.0-2.0)
- **request.max_tokens**: Maximum tokens to generate
- **response.number_of_messages**: Number of messages in response
- **response.choices.finish_reason**: Reason completion finished (e.g., "stop", "length")
- **vendor**: "openai" or "azureopenai" (automatically detected based on client type)
- **ingest_source**: "DotNet"
- **duration**: Request duration in milliseconds
- **error**: Boolean indicating if error occurred
- **http_status_code**: HTTP status code from API response
- **error.code**: Error code if request failed
- **error.message**: Error message if request failed

### LLM Chat Completion Message Events
For each message in the conversation (request and response):
- **id**: Unique message identifier
- **llm.conversation_id**: Links message to conversation
- **request_id**: Links message to specific request
- **sequence**: Message order in conversation
- **vendor**: "openai" or "azureopenai" (automatically detected based on client type)
- **ingest_source**: "DotNet"
- **content**: Message content (subject to truncation based on configuration)
- **role**: Message role ("system", "user", "assistant", "tool")
- **is_response**: Boolean indicating if message is from model response
- **completion_id**: Completion choice identifier

### Supportability Metrics
- **Supportability/DotNet/ML/{vendor}/{version}**: Tracks OpenAI or Azure.OpenAI SDK version usage
- **Supportability/DotNet/LLM/{vendor}-Chat**: Tracks LLM chat usage by vendor

Where `{vendor}` is either "openai" or "azureopenai" depending on the detected client type.

## Configuration Requirements

LLM instrumentation requires the following agent configuration:

- **`ai_monitoring.enabled`**: Must be `true` to enable AI monitoring features
- **`ai_monitoring.record_content.enabled`**: Controls whether message content is captured (default: `true`)

When `ai_monitoring.enabled` is `false`, the wrapper returns `NoOp` and no instrumentation occurs.

## Content Truncation

Message content is subject to truncation based on agent configuration:
- Maximum content length is configurable
- Content exceeding the limit is truncated
- Truncation is applied to both request and response messages

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
