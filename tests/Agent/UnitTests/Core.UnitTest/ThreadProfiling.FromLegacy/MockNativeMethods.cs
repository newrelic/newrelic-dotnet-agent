/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    public class MockNativeMethods : INativeMethods
    {

        public void RequestFunctionNames(ulong[] functionId, IntPtr callback)
        {
        }

        public void RequestProfile(IntPtr successCallback, IntPtr failureCallback, IntPtr completeCallback)
        {
        }
    }
}
