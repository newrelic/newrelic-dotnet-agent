// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace ReleaseNotesBuilder
{
    enum ExitCode : int
    {
        Success = 0,
        FileNotFound = 2,
        NotAChangelog = 11,
        InvalidData = 13,
        BadArguments = 160
    }
}
