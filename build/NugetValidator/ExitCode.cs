// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NugetValidator;

public enum ExitCode : int
{
    Success = 0,
    Error = 1,
    FileNotFound = 2,
    BadArguments = 160
}
