/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;

namespace MultiFunctionApplicationHelpers
{
    public static class Logger
    {

        public static void Info()
        {
            Info("");
        }

        public static void Info(params string[] message)
        {
            foreach (var msg in message)
            {
                Console.WriteLine($"{DateTime.Now.ToLongTimeString()} :{msg}");
            }
        }

        public static void Error()
        {
            Error("");
        }

        public static void Error(params string[] message)
        {
            foreach (var msg in message)
            {
                Console.Error.WriteLine($"{DateTime.Now.ToLongTimeString()} :{msg}");
            }
        }

        public static void Error(Exception ex)
        {
            Error(ex.ToString());
        }
    }
}
