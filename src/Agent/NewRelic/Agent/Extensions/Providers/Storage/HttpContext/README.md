# HttpContext Storage

## Overview

The HttpContext storage provider stores transaction context data using ASP.NET's `HttpContext.Items` dictionary. This storage mechanism is designed for traditional ASP.NET applications running on .NET Framework.

## How It Works

### Storage Mechanism

- **Backing Store**: `System.Web.HttpContext.Current.Items`
- **Data Storage**: Uses a string key to store and retrieve typed values in the `HttpContext.Items` dictionary
- **Priority**: 10 (used when `HttpContext.Current` is available)

### Availability

The storage is only available when:
- Running in an ASP.NET application
- `System.Web.HttpContext.Current` is not null
- The `System.Web` assembly is accessible

### Thread Behavior

`HttpContext.Items` automatically follows web requests across threads in ASP.NET, making it suitable for:
- Synchronous request processing
- ASP.NET async operations that preserve HttpContext
- Mid-request thread transitions

### Operations

- **GetData**: Retrieves value from `HttpContext.Items[key]`
- **SetData**: Stores value in `HttpContext.Items[key]`
- **Clear**: Removes value from `HttpContext.Items`
- **CanProvide**: Returns `true` if `HttpContext.Current` is available

## Use Cases

- Traditional ASP.NET web applications (.NET Framework)
- ASP.NET MVC and Web API applications on .NET Framework
- Applications where HttpContext is always available during request processing

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
