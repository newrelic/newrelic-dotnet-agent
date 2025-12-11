# New Relic .NET Agent RestSharp Instrumentation

## Overview

The RestSharp instrumentation wrapper provides automatic monitoring for outgoing HTTP requests made using the RestSharp client library within an existing transaction. It creates external segments to track request timing, captures HTTP status codes, and manages distributed tracing headers for cross-process correlation.

## Instrumented Methods

### ExecuteTaskAsyncWrapper (RestSharp < 106.7.0)
- **Wrapper**: [ExecuteTaskAsyncWrapper.cs](ExecuteTaskAsyncWrapper.cs)
- **Assembly**: `RestSharp`
- **Type**: `RestSharp.RestClient`

| Method | Creates Transaction | Requires Existing Transaction | Max Version |
|--------|-------------------|------------------------------|-------------|
| ExecuteTaskAsync | No | Yes | 106.7.0 |

### ExecuteTaskAsyncWrapper (RestSharp >= 106.7.0)
- **Wrapper**: [ExecuteTaskAsyncWrapper.cs](ExecuteTaskAsyncWrapper.cs)
- **Assembly**: `RestSharp`
- **Type**: `RestSharp.RestClient`

| Method | Creates Transaction | Requires Existing Transaction | Min Version | Max Version |
|--------|-------------------|------------------------------|-------------|-------------|
| ExecuteAsync | No | Yes | 106.7.0 | 107.0.0 |

### AppendHeadersWrapper (RestSharp < 106.7.0)
- **Wrapper**: [AppendHeadersWrapper.cs](AppendHeadersWrapper.cs)
- **Assembly**: `RestSharp`
- **Type**: `RestSharp.Http`

| Method | Creates Transaction | Requires Existing Transaction | Max Version |
|--------|-------------------|------------------------------|-------------|
| AppendHeaders | No | Yes | 106.7.0 |

### ConfigureWebRequestWrapper (RestSharp >= 106.7.0)
- **Wrapper**: [ConfigureWebRequestWrapper.cs](ConfigureWebRequestWrapper.cs)
- **Assembly**: `RestSharp`
- **Type**: `RestSharp.Http`

| Method | Creates Transaction | Requires Existing Transaction | Min Version | Max Version |
|--------|-------------------|------------------------------|-------------|-------------|
| ConfigureWebRequest | No | Yes | 106.7.0 | 107.0.0 |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/RestSharp/Instrumentation.xml)

## Attributes Added

The wrapper creates external segments with the following attributes:

- **External segment data**:
  - `http.url`: Request URI
  - `http.method`: HTTP method (GET, POST, etc.)
  - `http.statusCode`: HTTP response status code

## Distributed Tracing

The wrapper manages distributed tracing through two separate instrumentation points that handle different RestSharp versions:

### Header Insertion (Version < 106.7.0)

**Method**: `AppendHeaders(HttpWebRequest)`
1. **Timing**: Called just before RestSharp sends the request
2. **Access**: Receives `HttpWebRequest` as parameter
3. **Action**: Inserts distributed tracing headers into the `HttpWebRequest.Headers` collection

### Header Insertion (Version >= 106.7.0)

**Method**: `ConfigureWebRequest(string, Uri)`
1. **Timing**: Called during request configuration
2. **Return value**: Returns configured `HttpWebRequest`
3. **Action**: Inserts distributed tracing headers into the returned `HttpWebRequest.Headers` collection

### Version-Specific Behavior

RestSharp version 106.7.0 introduced breaking changes:
- **Pre-106.7.0**: `AppendHeaders` was a separate method accepting `HttpWebRequest`
- **Post-106.7.0**: `AppendHeaders` became a local method without the `HttpWebRequest` parameter
- **Solution**: Instrument the parent `ConfigureWebRequest` method and insert headers into the returned `HttpWebRequest`

## Version Considerations

The instrumentation explicitly handles the API changes in RestSharp 106.7.0:

### Request Execution Changes
- **Versions < 106.7.0**: Instruments `ExecuteTaskAsync` method
- **Versions >= 106.7.0**: Instruments `ExecuteAsync` method (renamed)
- Both methods have identical wrapper behavior despite the name change

### Header Injection Changes
- **Versions < 106.7.0**: Instruments `AppendHeaders(HttpWebRequest)` method
- **Versions >= 106.7.0**: Instruments `ConfigureWebRequest` method to access the `HttpWebRequest` being configured

All instrumentation is capped at version 107.0.0 (exclusive) to avoid compatibility issues with future major versions.

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
