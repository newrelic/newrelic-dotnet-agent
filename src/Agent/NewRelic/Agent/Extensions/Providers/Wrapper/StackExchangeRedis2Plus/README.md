# New Relic .NET Agent StackExchangeRedis2Plus Instrumentation

## Overview

The StackExchangeRedis2Plus instrumentation wrapper provides connection tracking for StackExchange.Redis client library versions 2.0 and later. It instruments the connection multiplexer creation to enable datastore instance reporting and connection pooling metrics.

## Instrumented Methods

### CreateMultiplexerWrapper
- **Wrapper**: [CreateMultiplexerWrapper.cs](CreateMultiplexerWrapper.cs)
- **Assembly**: `StackExchange.Redis`
- **Type**: `StackExchange.Redis.ConnectionMultiplexer`

| Method | Creates Transaction | Requires Existing Transaction | Min Version |
|--------|-------------------|------------------------------|-------------|
| CreateMultiplexer | No | No | 2.0.0.0 |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/StackExchangeRedis2Plus/Instrumentation.xml)

## Purpose

This wrapper focuses specifically on connection multiplexer creation for StackExchange.Redis 2.0+:

- **Connection tracking**: Monitors when Redis connection multiplexers are created
- **Instance identification**: Captures connection configuration for datastore instance reporting
- **Metrics**: Enables connection pool and instance-level metrics

## Version Considerations

This wrapper targets StackExchange.Redis versions 2.0 and later:

- **Min version**: 2.0.0.0 (inclusive)
- **Reason for separate wrapper**: StackExchange.Redis 2.0 introduced breaking changes to internal APIs
- **Complementary instrumentation**: Works alongside other Redis instrumentation for operation-level segments

For StackExchange.Redis 1.x, see the [StackExchangeRedis](../StackExchangeRedis/README.md) instrumentation.

## Architecture Differences from Version 1.x

StackExchange.Redis 2.0 introduced significant internal changes:
- Connection multiplexer creation process changed
- Internal execution methods were refactored
- Connection pooling behavior was modified

This dedicated wrapper ensures proper instrumentation of the 2.0+ connection creation flow without affecting 1.x compatibility.

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
