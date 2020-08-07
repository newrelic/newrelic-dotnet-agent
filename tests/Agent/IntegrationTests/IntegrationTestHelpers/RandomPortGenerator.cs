// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Globalization;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class RandomPortGenerator
    {
        private static readonly object LockObject = new object();

        private static readonly int RandomSeed = Process.GetCurrentProcess().Id + AppDomain.CurrentDomain.Id + Environment.TickCount;

        private static readonly Random Random = new Random(RandomSeed);

        public static int NextPort()
        {
            lock (LockObject)
            {
                return Random.Next(60000) + 5000;
            }
        }

        public static string NextPortString()
        {
            return NextPort().ToString(CultureInfo.InvariantCulture);
        }
    }
}
