# New Relic .NET Agent OpenSearch Instrumentation

## Overview

The OpenSearch instrumentation wrapper provides automatic monitoring for OpenSearch client operations executed within an existing transaction. It creates datastore segments for search, indexing, and cluster operations, capturing operation type, index name, endpoint host/port, and error details.

## Instrumented Methods

### OpenSearchRequestWrapper
- **Wrapper**: [OpenSearchRequestWrapper.cs](OpenSearchRequestWrapper.cs)
- **Assembly**: `OpenSearch.Net`
- **Type**: `OpenSearch.Net.OpenSearchLowLevelClient`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| DoRequest | No | Yes |
| DoRequestAsync | No | Yes |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/OpenSearch/Instrumentation.xml)

## Attributes Added

The wrapper creates datastore segments with the following attributes:

- **Datastore vendor**: Set to "Elasticsearch" (OpenSearch uses Elasticsearch protocol)
- **Index name (model)**: Parsed from the request path (first path component); set to "Unknown" if the index name starts with underscore or is empty
- **Operation**: Derived from request parameters or path analysis using the same logic as Elasticsearch instrumentation:
  - For high-level client: extracted from `RequestParameters` type name
  - For low-level client: determined by analyzing HTTP method, path, and operation type
- **Endpoint host and port**: Retrieved from the response `ApiCallDetails.Uri` property
- **Error details**: Captured from `OriginalException` or `SuccessOrKnownError` properties

## Operation Detection

The wrapper uses the same operation detection strategy as Elasticsearch instrumentation:

1. **Request parameters**: Extracts operation from `RequestParameters` type name when available
2. **Path analysis**: Parses the request path and HTTP method using precedence:
   - Full request type map (method + path + subtype)
   - Subtype map (path + subtype)
   - Request map (method + path)
   - Rename map (abbreviated operations, e.g., `_mget` → "MultiGet")
   - Default: Capitalize path component

Common operations include: Search, Index, Get, Delete, Update, Bulk, MultiGet, MultiSearch, Aggregate, Count, etc.

## OpenSearch and Elasticsearch Compatibility

OpenSearch is a fork of Elasticsearch that maintains API compatibility:
- Uses the same client protocols and request/response formats
- Reports as "Elasticsearch" datastore vendor for consistency
- Shares operation detection and path parsing logic with Elasticsearch instrumentation

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
