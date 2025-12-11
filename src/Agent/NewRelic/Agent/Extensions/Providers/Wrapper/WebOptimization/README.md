# New Relic .NET Agent WebOptimization Instrumentation

## Overview

The WebOptimization instrumentation wrapper provides automatic monitoring for ASP.NET Web Optimization Framework (bundling and minification) request handling. It instruments the BundleHandler to track timing for JavaScript and CSS bundle processing within existing web transactions.

## Instrumented Methods

### BundleHandlerWrapper
- **Wrapper**: [BundleHandlerWrapper.cs](BundleHandlerWrapper.cs)
- **Assembly**: `System.Web.Optimization`
- **Type**: `System.Web.Optimization.BundleHandler`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| ProcessRequest | No | Yes |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/WebOptimization/Instrumentation.xml)

## Purpose

The Web Optimization Framework (Microsoft.AspNet.Web.Optimization) provides bundling and minification for JavaScript and CSS files in ASP.NET applications. This instrumentation tracks:

- Time spent processing bundle requests
- Bundle generation and caching operations
- Minification overhead

## Bundle Request Handling

When a bundle URL is requested (e.g., `/bundles/jquery`), the `BundleHandler.ProcessRequest` method:
1. Resolves the bundle definition
2. Processes included files (reads, transforms, minifies)
3. Combines files into a single response
4. Applies caching headers

The instrumentation creates segments to capture the timing of these operations within the web transaction.

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
