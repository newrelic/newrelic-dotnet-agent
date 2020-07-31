// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    public interface INativeMethods
    {
        // Client interface for requesting a function name by id
        void RequestFunctionNames(ulong[] functionIds, IntPtr callback);

        void RequestProfile(IntPtr successCallback, IntPtr failureCallback, IntPtr completeCallback);
    }
}
