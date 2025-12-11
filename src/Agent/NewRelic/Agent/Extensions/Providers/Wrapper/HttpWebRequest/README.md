# New Relic .NET Agent HttpWebRequest Instrumentation

## Overview

The HttpWebRequest instrumentation wrapper provides automatic monitoring for outgoing HTTP requests made using `System.Net.HttpWebRequest` within an existing transaction. It creates external segments to track request timing, captures HTTP status codes, and manages distributed tracing headers for cross-process correlation.

## Instrumented Methods

### SerializeHeadersWrapper
- **Wrapper**: [SerializeHeadersWrapper.cs](SerializeHeadersWrapper.cs)
- **Assembly**: `System`
- **Type**: `System.Net.HttpWebRequest`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| SerializeHeaders | No | Yes |

### GetResponseWrapper
- **Wrapper**: [GetResponseWrapper.cs](GetResponseWrapper.cs)
- **Assembly**: `System`
- **Type**: `System.Net.HttpWebRequest`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| GetResponse | No | Yes |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/HttpWebRequest/Instrumentation.xml)

## Attributes Added

The wrapper creates external segments with the following attributes:

- **External segment data**:
  - `http.url`: Request URI (from `HttpWebRequest.RequestUri`)
  - `http.method`: HTTP method (from `HttpWebRequest.Method`, defaults to "<unknown>" if null)
  - `http.statusCode`: HTTP response status code (from `HttpWebResponse.StatusCode`)

## Distributed Tracing

The wrapper manages distributed tracing through two instrumentation points:

### Header Insertion (SerializeHeadersWrapper)

1. **Timing**: Triggered by `SerializeHeaders` method, which is called internally by `HttpWebRequest` just before sending the request
2. **Segment validation**: Only inserts headers if the current segment is an external segment created by `GetResponse` (prevents interference with higher-level instrumentation)
3. **Headers inserted**: Standard distributed tracing headers are added via `Transaction.InsertDistributedTraceHeaders`
4. **Method**: Headers are set using `HttpWebRequest.Headers.Set(key, value)`

### Header Processing (GetResponseWrapper)

1. **Response headers**: Extracted from `HttpWebResponse.Headers` or `WebException.Response.Headers` (for failed requests)
2. **Processing**: Response headers are passed to `Transaction.ProcessInboundResponse` to correlate with the distributed trace
3. **Timing**: Occurs after receiving the response, before ending the segment

### HttpClient and RestSharp Compatibility

The `SerializeHeadersWrapper` explicitly checks if the current segment is an external segment to avoid interfering with HttpClient and RestSharp instrumentation:
- HttpClient and RestSharp have their own header injection support
- These libraries use leaf segments to prevent duplicate header injection
- If the current segment is not external, header injection is skipped

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0