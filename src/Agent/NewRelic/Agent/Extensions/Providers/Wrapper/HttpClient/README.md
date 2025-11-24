# New Relic .NET Agent HttpClient Instrumentation

## Overview
This wrapper instruments outbound HTTP calls made via `System.Net.Http.HttpClient` and `System.Net.Http.SocketsHttpHandler`. It records external request segments (URL, HTTP method, status code), propagates distributed trace headers, and (when legacy cross application tracing is enabled) processes response headers for cross-app linking.

## Instrumented Methods

### SendAsync Wrapper
- Wrapper: [`SendAsync`](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/HttpClient/SendAsync.cs)
- Assembly: `System.Net.Http`
- Type(s): `System.Net.Http.HttpClient`, `System.Net.Http.SocketsHttpHandler`

| Method (name + parameters) | Creates Transaction | Requires Existing Transaction | Min Version | Max Version | Notes |
|----------------------------|---------------------|-------------------------------|-------------|-------------|-------|
| [`SendAsync(System.Net.Http.HttpRequestMessage, System.Net.Http.HttpCompletionOption, System.Threading.CancellationToken)`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Net.Http/src/System/Net/Http/HttpClient.cs) | No | Yes |  | 5.0 | HttpClient overload (pre-`SocketsHttpHandler` / up to maxVersion). Attaches DT headers, starts external segment, captures status code. |
| [`SendAsync(System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken)`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/SocketsHttpHandler.cs) | No | Yes | 5.0 |  | SocketsHttpHandler async path (.NET 5+). Uses synchronous continuation to ensure header/thread safety; adds DT headers & status. |
| [`Send(System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken)`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/SocketsHttpHandler.cs) | No | Yes | 5.0 |  | Synchronous send; processes response in success delegate; records status & CAT if applicable. |

## Instrumentation XML
[`Instrumentation.xml`](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/HttpClient/Instrumentation.xml)

## Attributes Added
Each outbound call adds an external segment/span with:
- Target URI host, port, and path
- HTTP method
- HTTP status code (on response)
- External/cross-app linking metadata (CAT only when DT disabled & CAT enabled)
- Distributed tracing outbound headers (W3C / New Relic) injected into the request

## Distributed Tracing
Distributed tracing headers are inserted into the `HttpRequestMessage` before send. Response status is captured. If distributed tracing is disabled and legacy cross application tracing is enabled, response headers are flattened and processed for CAT linking.

## Version Considerations
Instrumentation uses version gating:
- `HttpClient.SendAsync` applies only up to `maxVersion=5.0`.
- `SocketsHttpHandler` methods apply from `minVersion=5.0` onward.
This ensures the correct implementation (handler vs client) is wrapped across runtime versions.

## License
Copyright 2020 New Relic, Inc. All rights reserved.  
SPDX-License-Identifier: Apache-2.0
