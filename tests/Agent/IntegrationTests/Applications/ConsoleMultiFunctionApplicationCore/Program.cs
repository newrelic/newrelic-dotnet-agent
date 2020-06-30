/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using MultiFunctionApplicationHelpers;
using System;

namespace ConsoleMultiFunctionApplicationCore
{
    class Program
    {
        static void Main(string[] args)
        {
            MultiFunctionApplication.Execute(args);
        }
    }
}
