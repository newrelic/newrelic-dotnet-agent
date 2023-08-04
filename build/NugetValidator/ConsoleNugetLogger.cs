// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NuGet.Common;

namespace NugetValidator;

internal class ConsoleNugetLogger : LoggerBase
{
    public override void Log(ILogMessage message)
    {
        Console.WriteLine($"{message}");
    }

    public override Task LogAsync(ILogMessage message)
    {
        Console.WriteLine($"{message}");

        return Task.CompletedTask;
    }
}
