# OperationContext Storage

## Overview

The OperationContext storage provider stores transaction context data using WCF's `OperationContext.Current` extension mechanism. This storage is specifically designed for Windows Communication Foundation (WCF) services and leverages WCF's built-in extension system to maintain context throughout service operation execution.

## How It Works

### Storage Mechanism

- **Backing Store**: `System.ServiceModel.OperationContext.Current.Extensions`
- **Extension Type**: Custom `OperationContextExtension` that implements `IExtension<OperationContext>`
- **Data Storage**: Uses an internal `Hashtable` (`IDictionary`) within the extension to store typed values by string key
- **Priority**: 5 (used when `OperationContext.Current` is available)

### Availability

The storage is only available when:
- Running in a WCF service application
- `System.ServiceModel.OperationContext.Current` is not null
- The `System.ServiceModel` assembly is accessible

### Extension Mechanism

The storage uses WCF's extension pattern:
1. **Extension Registration**: When first accessed, creates and registers an `OperationContextExtension` instance
2. **Singleton Pattern**: Each `OperationContext` maintains a single instance of the extension
3. **Lazy Initialization**: Extension is created on first access via `OperationContext.Current.Extensions.Add()`
4. **Automatic Lifecycle**: Extension exists for the lifetime of the WCF operation

### Thread Behavior

`OperationContext.Current` automatically flows with WCF service operations:
- Synchronous WCF service methods
- WCF async operations (Begin/End pattern and Task-based)
- Thread transitions within WCF service execution
- Available throughout the entire WCF call chain

### Operations

- **GetData**: Retrieves value from `OperationContextExtension.Current.Items[key]`
- **SetData**: Stores value in `OperationContextExtension.Current.Items[key]`
- **Clear**: Removes value from `OperationContextExtension.Current.Items`
- **CanProvide**: Returns `true` if `OperationContext.Current` is available

## Use Cases

- WCF service applications (.NET Framework)
- WCF services hosted in IIS, Windows Services, or self-hosted
- Applications where `OperationContext` is always available during service operation processing
- Distributed WCF applications requiring context propagation

## Implementation Details

### OperationContextExtension

The custom extension class provides:
- **Items Dictionary**: A `Hashtable` for storing key-value pairs
- **Current Property**: Static accessor that retrieves or creates the extension instance
- **CanProvide Property**: Static check for `OperationContext.Current` availability
- **IExtension Implementation**: Empty `Attach()` and `Detach()` methods

### Factory Validation

The storage factory validates WCF availability by:
- Attempting to access `System.ServiceModel.OperationContext.Current`
- Catching any exceptions (type load, file not found, etc.)
- Marking the factory as invalid if WCF assemblies are not accessible

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
