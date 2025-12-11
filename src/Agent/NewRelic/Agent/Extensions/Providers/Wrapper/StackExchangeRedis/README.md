# New Relic .NET Agent StackExchangeRedis Instrumentation

## Overview

The StackExchangeRedis instrumentation wrapper provides automatic monitoring for Redis operations using StackExchange.Redis client library versions 1.x within an existing transaction. It creates datastore segments for all Redis commands including get, set, delete, and other operations.

## Instrumented Methods

### ExecuteSyncImplWrapper (StackExchange.Redis)
- **Wrapper**: [ExecuteSyncImplWrapper.cs](ExecuteSyncImplWrapper.cs)
- **Assembly**: `StackExchange.Redis`
- **Type**: `StackExchange.Redis.ConnectionMultiplexer`

| Method | Creates Transaction | Requires Existing Transaction | Min Version | Max Version |
|--------|-------------------|------------------------------|-------------|-------------|
| ExecuteAsyncImpl | No | Yes | 1.0.0.0 | 2.0.0.0 |
| ExecuteSyncImpl | No | Yes | 1.0.0.0 | 2.0.0.0 |

### ExecuteSyncImplWrapper (StackExchange.Redis.StrongName)
- **Wrapper**: [ExecuteSyncImplWrapper.cs](ExecuteSyncImplWrapper.cs)
- **Assembly**: `StackExchange.Redis.StrongName`
- **Type**: `StackExchange.Redis.ConnectionMultiplexer`

| Method | Creates Transaction | Requires Existing Transaction | Min Version | Max Version |
|--------|-------------------|------------------------------|-------------|-------------|
| ExecuteAsyncImpl | No | Yes | 1.0.0.0 | 2.0.0.0 |
| ExecuteSyncImpl | No | Yes | 1.0.0.0 | 2.0.0.0 |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/StackExchangeRedis/Instrumentation.xml)

## Attributes Added

The wrapper creates datastore segments with the following attributes:

- **Datastore vendor**: Set to "Redis"
- **Operation**: Redis command name (e.g., "GET", "SET", "HGET", "ZADD")
- **Connection info**: Host and port from Redis connection
- **Database index**: Redis database number if specified

## Version Considerations

This wrapper targets StackExchange.Redis versions 1.x (1.0.0.0 to 2.0.0.0 exclusive):

- **Min version**: 1.0.0.0 (inclusive)
- **Max version**: 2.0.0.0 (exclusive)
- **Assembly variants**: Supports both `StackExchange.Redis` and `StackExchange.Redis.StrongName` assemblies

For StackExchange.Redis 2.0+, see the [StackExchangeRedis2Plus](../StackExchangeRedis2Plus/README.md) instrumentation.

## Internal Method Instrumentation

The wrapper instruments internal execution methods that are called by all public Redis operation methods:

- **ExecuteSyncImpl**: Internal synchronous command execution
- **ExecuteAsyncImpl**: Internal asynchronous command execution

This approach ensures comprehensive coverage of all Redis operations without instrumenting each individual command method.

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
