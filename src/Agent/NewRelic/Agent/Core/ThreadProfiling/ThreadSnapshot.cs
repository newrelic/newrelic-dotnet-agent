/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Runtime.InteropServices;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ThreadSnapshot
    {
        public UIntPtr ThreadId;
        public int ErrorCode;
        public UIntPtr[] FunctionIDs;
    };
}
