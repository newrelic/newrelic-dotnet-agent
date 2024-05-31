// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    interface ILoggingAdapter
    {
        public void Debug (string message);
        public void Info(string message);
        public void Info(string message, Dictionary<string, object> context);
        public void InfoWithParam(string message, object param);

        public void Warn(string message);
        public void Error(Exception exception);

        public void ErrorNoMessage(Exception exception);
        public void Fatal(string message);
        public void NoMessage();
        public void Configure();
        public void ConfigureWithInfoLevelEnabled();
        public void ConfigurePatternLayoutAppenderForDecoration();
        public void ConfigureJsonLayoutAppenderForDecoration();

        public void  LogMessageInNestedScopes();
    }
}
