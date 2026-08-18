// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.MySql;

// Small retry helper for MySQL test setup operations. MySQL in the unbounded
// test environment occasionally throws transient connection / packet-read
// faults mid-stream; retrying the setup with a fresh connection avoids flaky
// failures. Only use this for setup (e.g. CreateProcedure), never for the
// instrumented calls that tests assert metric call counts on.
public static class MySqlRetryHelper
{
    public static void ExecuteWithRetry(Action action, int maxAttempts = 3, int delayMs = 1000)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex)
            {
                if (attempt >= maxAttempts)
                {
                    throw;
                }

                ConsoleMFLogger.Info($"MySqlRetryHelper: attempt {attempt} of {maxAttempts} failed with '{ex.Message}'. Retrying in {delayMs}ms.");
                Thread.Sleep(delayMs);
            }
        }
    }
}
