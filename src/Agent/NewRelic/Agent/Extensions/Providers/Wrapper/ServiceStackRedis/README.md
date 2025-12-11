# New Relic .NET Agent ServiceStackRedis Instrumentation

## Overview

The ServiceStackRedis instrumentation wrapper provides automatic monitoring for Redis operations using the ServiceStack.Redis client library within an existing transaction. It creates datastore segments for all Redis commands by instrumenting the internal command execution method.

## Instrumented Methods

### SendCommandWrapper
- **Wrapper**: [SendCommandWrapper.cs](SendCommandWrapper.cs)
- **Assembly**: `ServiceStack.Redis`
- **Type**: `ServiceStack.Redis.RedisNativeClient`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| SendCommand | No | Yes |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/ServiceStackRedis/Instrumentation.xml)

## Attributes Added

The wrapper creates datastore segments with the following attributes:

- **Datastore vendor**: Set to "Redis"
- **Operation**: Redis command name (e.g., "GET", "SET", "HGET", "ZADD")
- **Connection info**: Host and port from Redis connection

## Internal Method Instrumentation

The wrapper instruments the `SendCommand` internal method which is called by all public Redis operation methods in ServiceStack.Redis. This ensures comprehensive coverage of all Redis operations without needing to instrument each individual command method.

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
