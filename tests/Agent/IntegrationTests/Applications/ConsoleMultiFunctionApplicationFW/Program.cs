/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using MultiFunctionApplicationHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleMultiFunctionApplicationFW
{
    class Program
    {
        static void Main(string[] args)
        {
            MultiFunctionApplication.Execute(args);
        }
    }
}
