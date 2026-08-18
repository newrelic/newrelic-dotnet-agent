// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace PublicApiChangeTests;

public static class ModuleInitializer
{
    // The verified snapshot is checked out with CRLF on Windows (the root .gitattributes
    // marks *.txt as text) and carries a trailing newline. Verify tolerated both silently
    // through 31.0.1; later versions reject them unless tolerance is opted into. Both
    // settings are global and throw if set after the first verify, so a module
    // initializer is the only place they can go.
    [ModuleInitializer]
    public static void Init()
    {
        VerifierSettings.FixNewlinesOnRead();
        VerifierSettings.IgnoreTrailingNewline();
    }
}
