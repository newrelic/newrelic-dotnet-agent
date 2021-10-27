// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Runtime.CompilerServices;

namespace NetStandardTestLibrary
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
