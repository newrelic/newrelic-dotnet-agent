/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System.Runtime.CompilerServices;

namespace NetStandardClassLibrary
{
    public class MyClass
    {
        [NewRelic.Api.Agent.Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int MyMethodToBeInstrumented()
        {
            return 1;
        }
    }
}
