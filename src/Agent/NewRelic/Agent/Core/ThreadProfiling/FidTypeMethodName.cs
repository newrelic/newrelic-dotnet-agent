// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;

namespace NewRelic.Agent.Core.ThreadProfiling;

[StructLayout(LayoutKind.Sequential)]
public class FidTypeMethodName
{
    public UIntPtr FunctionID;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string TypeName;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string MethodName;
};