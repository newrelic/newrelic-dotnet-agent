// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace custom_xml
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Invoking custom instrumentation");

            Transaction();
        }

        static void Transaction()
        {
            Thing();
        }

        static void Thing()
        {
        }
    }
}
