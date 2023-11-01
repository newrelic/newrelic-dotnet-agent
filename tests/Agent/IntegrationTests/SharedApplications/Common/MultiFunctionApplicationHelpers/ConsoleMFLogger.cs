// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;

namespace MultiFunctionApplicationHelpers
{
    public static class ConsoleMFLogger
    {

        private static string LogTs => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss,fff");

        private static int Tid => System.Threading.Thread.CurrentThread.ManagedThreadId;

        public static void Info()
        {
            Info("");
        }

        public static void Info(params string[] message)
        {
            foreach (var msg in message)
            {
                Console.WriteLine($"{LogTs} tid:{Tid} {msg}");
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
                Console.Error.WriteLine($"{LogTs} tid:{Tid} {msg}");
            }
        }

        public static void Error(Exception ex)
        {
            Error(ex.ToString());
        }

    }
}
