/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;

namespace OwinRemotingShared
{
    public class MyMarshalByRefClass : MarshalByRefObject
    {
        public int MyMethod()
        {
            return 666;
        }
    }
}
