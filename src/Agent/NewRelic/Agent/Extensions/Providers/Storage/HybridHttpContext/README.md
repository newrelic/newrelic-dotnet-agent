# HybridHttpContext Storage

## Overview

The HybridHttpContext storage provider combines two storage mechanisms to ensure transaction context data remains available across both synchronous and asynchronous operations. It synchronizes data between ASP.NET's `HttpContext.Items` dictionary and `AsyncLocal<T>` storage. This hybrid approach is designed for ASP.NET applications that use async/await patterns where context may not flow automatically.

## How It Works

### Storage Mechanism

- **Primary Store**: `System.Web.HttpContext.Current.Items`
- **Secondary Store**: `AsyncLocal<T>`
- **Synchronization**: Bidirectional sync ensures data consistency between both stores
- **Priority**: 15 (higher than HttpContext-only storage, used when available)

### Availability

The storage is only available when:
- Running in an ASP.NET application
- `System.Web.HttpContext.Current` is not null
- The `System.Web` assembly is accessible

### Thread Behavior

The hybrid approach ensures context availability in scenarios where HttpContext alone is insufficient:
- **Async/await operations**: AsyncLocal maintains context across async continuations
- **Thread pool operations**: AsyncLocal flows to thread pool threads
- **Task-based async**: Context preserved even when HttpContext might not flow
- **Traditional ASP.NET operations**: HttpContext.Items provides familiar storage

### Synchronization Strategy

Data is synchronized bidirectionally:
1. **GetData**: Retrieves from HttpContext.Items, falls back to AsyncLocal if HttpContext unavailable
2. **SetData**: Writes to both HttpContext.Items (if available) and AsyncLocal
3. **Clear**: Removes from both storage locations

This ensures that context set in one storage mechanism is visible in the other, providing seamless operation across different execution contexts.

### Operations

- **GetData**: Retrieves value from `HttpContext.Items[key]` or falls back to `AsyncLocal<T>.Value`
- **SetData**: Stores value in both `HttpContext.Items[key]` and `AsyncLocal<T>.Value`
- **Clear**: Removes value from both `HttpContext.Items` and `AsyncLocal<T>`
- **CanProvide**: Returns `true` if `HttpContext.Current` is available

## Use Cases

- ASP.NET applications with async/await controller actions
- Applications mixing synchronous and asynchronous code paths
- Scenarios where HttpContext may not flow automatically (certain thread transitions)
- Applications requiring context preservation across complex async operations

## Comparison with HttpContext Storage

| Feature | HttpContext Storage | HybridHttpContext Storage |
|---------|-------------------|--------------------------|
| Priority | 10 | 15 |
| Storage Mechanism | HttpContext.Items only | HttpContext.Items + AsyncLocal |
| Async Support | ASP.NET async only | Full async/await support |
| Thread Flow | ASP.NET managed only | Automatic via AsyncLocal |
| Availability | HttpContext required | HttpContext required (uses AsyncLocal as fallback) |

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
