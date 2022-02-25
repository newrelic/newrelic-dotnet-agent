﻿// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    interface ILoggingAdapter
    {
        public void Debug (string message);
        public void Info(string message);
        public void Warn(string message);
        public void Error(string message);
        public void Fatal(string message);

        public void Configure();
        public void ConfigurePatternLayoutAppenderForDecoration();
        public void ConfigureJsonLayoutAppenderForDecoration();

    }
}
