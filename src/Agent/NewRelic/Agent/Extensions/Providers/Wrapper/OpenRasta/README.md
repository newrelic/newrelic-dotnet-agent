# New Relic .NET Agent OpenRasta Instrumentation

## Overview

The OpenRasta instrumentation wrapper provides automatic transaction naming for OpenRasta web framework applications. It instruments the OpenRasta HTTP handler to extract resource and operation information for naming web transactions.

## Instrumented Methods

### OpenRastaHandlerWrapper
- **Wrapper**: [OpenRastaHandlerWrapper.cs](OpenRastaHandlerWrapper.cs)
- **Assembly**: `OpenRasta.Hosting.AspNet`
- **Type**: `OpenRasta.Hosting.AspNet.OpenRastaHandler`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| GetHandler | No | Yes |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/OpenRasta/Instrumentation.xml)

## Transaction Naming

The wrapper extracts routing information from OpenRasta to name web transactions:

- **Transaction name format**: Typically based on resource handler type and method
- **Priority**: Framework-level priority (allows higher-priority naming to override)
- **Source**: Retrieved from OpenRasta's internal routing and handler resolution

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
