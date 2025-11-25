# New Relic .NET Agent Elasticsearch Instrumentation

## Overview

The Elasticsearch instrumentation wrapper provides automatic monitoring for Elasticsearch and OpenSearch client operations executed within an existing transaction. It supports both NEST/Elasticsearch.Net (7.x) and Elastic.Clients.Elasticsearch (prior to 8.15.10) libraries, capturing operation type, index name, endpoint host/port, and error details to create datastore segments.

## Instrumented Methods

### RequestWrapper (Elasticsearch.Net.Transport\`1)
- **Wrapper**: [RequestWrapper.cs](RequestWrapper.cs)
- **Assembly**: `Elasticsearch.Net`
- **Type**: `Elasticsearch.Net.Transport\`1`

| Method | Creates Transaction | Requires Existing Transaction | Max Version |
|--------|-------------------|------------------------------|-------------|
| Request | No | Yes | 8.15.10 |
| RequestAsync | No | Yes | 8.15.10 |

### RequestWrapper (Elastic.Transport.DefaultHttpTransport\`1)
- **Wrapper**: [RequestWrapper.cs](RequestWrapper.cs)
- **Assembly**: `Elastic.Transport`
- **Type**: `Elastic.Transport.DefaultHttpTransport\`1`

| Method | Creates Transaction | Requires Existing Transaction | Max Version |
|--------|-------------------|------------------------------|-------------|
| Request | No | Yes | 8.15.10 |
| RequestAsync | No | Yes | 8.15.10 |

### RequestWrapper (Elastic.Transport.DistributedTransport\`1)
- **Wrapper**: [RequestWrapper.cs](RequestWrapper.cs)
- **Assembly**: `Elastic.Transport`
- **Type**: `Elastic.Transport.DistributedTransport\`1`

| Method | Creates Transaction | Requires Existing Transaction | Max Version |
|--------|-------------------|------------------------------|-------------|
| Request | No | Yes | 8.15.10 |
| RequestAsync | No | Yes | 8.15.10 |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/Elasticsearch/Instrumentation.xml)

## Attributes Added

The wrapper creates datastore segments with the following attributes:

- **Datastore vendor**: Set to "Elasticsearch"
- **Index name (model)**: Parsed from the request path (first path component); set to "Unknown" if the index name starts with underscore or is empty
- **Operation**: Derived from request parameters or path analysis:
  - For high-level client: extracted from `RequestParameters` type name (e.g., `IndexRequestParameters` → "Index")
  - For low-level client: determined by analyzing HTTP method, path, and operation type (e.g., `PUT /my-index/_doc` → "Index", `GET /my-index/_search` → "Search")
- **Endpoint host and port**: Retrieved from the response `ApiCallDetails.Uri` property
- **Error details**: Captured from `OriginalException` or `SuccessOrKnownError` properties

## Operation Detection

The wrapper uses multiple strategies to determine the operation name:

1. **High-level client**: Extracts operation from the `RequestParameters` type name
2. **Low-level client**: Parses the request path and HTTP method using precedence:
   - Full request type map (method + path + subtype, e.g., `DELETE|_search|scroll` → "ClearScroll")
   - Subtype map (path + subtype, e.g., `_search|template` → "SearchTemplate")
   - Request map (method + path, e.g., `PUT|_doc` → "Index")
   - Rename map (abbreviated operations, e.g., `_mget` → "MultiGet", `_msearch` → "MultiSearch")
   - Default: Capitalize path component (e.g., `_search` → "Search", `_search_shards` → "SearchShards")

## Version Considerations

All instrumented methods specify a maximum version of 8.15.10 (exclusive). The wrapper supports:
- **NEST/Elasticsearch.Net** (7.x): Instruments `Elasticsearch.Net.Transport<T>` class
- **Elastic.Clients.Elasticsearch** (8.x): Instruments `Elastic.Transport.DefaultHttpTransport<T>` and `Elastic.Transport.DistributedTransport<T>` classes
- **Version 8.10.0+**: Includes overloads with `OpenTelemetryData` parameter
- **Version 8.12.1+**: Adds support for `DistributedTransport<T>` class

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
