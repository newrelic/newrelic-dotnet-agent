// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;

namespace NewRelic.Agent.Core
{
    public interface INativeMethods
    {
        void ReleaseProfile();
        int RequestFunctionNames(UIntPtr[] functionIds, int length, [Out] out IntPtr functionInfo);
        int RequestProfile([Out] out IntPtr snapshots, [Out] out int length);
        void ShutdownNativeThreadProfiler();

        int InstrumentationRefresh();
        int ReloadConfiguration();
        int AddCustomInstrumentation(string fileName, string xml);
        int ApplyCustomInstrumentation();
    }
}
