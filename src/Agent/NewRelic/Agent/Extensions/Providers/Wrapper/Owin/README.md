# New Relic .NET Agent Owin Instrumentation

## Overview

The Owin instrumentation wrapper provides automatic monitoring for OWIN (Open Web Interface for .NET) applications hosted with Microsoft.Owin.Hosting. It instruments the application resolution process to enable transaction creation and naming for OWIN middleware pipelines.

## Instrumented Methods

### ResolveAppWrapper
- **Wrapper**: [ResolveAppWrapper.cs](ResolveAppWrapper.cs)
- **Assembly**: `Microsoft.Owin.Hosting`
- **Type**: `Microsoft.Owin.Hosting.Engine.HostingEngine`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| ResolveApp | No | No |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/Owin/Instrumentation.xml)

## Transaction Lifecycle

The `ResolveApp` method is called during OWIN application startup to resolve and configure the middleware pipeline:

- **Purpose**: Instruments the OWIN application resolution process
- **Timing**: Called once during application initialization
- **Effect**: Enables the agent to hook into the OWIN pipeline for transaction creation and management

The actual transaction creation and management happens through separate OWIN middleware instrumentation that works in conjunction with this wrapper.

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
