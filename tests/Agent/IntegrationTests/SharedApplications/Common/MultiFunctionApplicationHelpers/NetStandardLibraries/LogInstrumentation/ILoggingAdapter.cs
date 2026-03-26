// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation;

interface ILoggingAdapter
{
    // Basic logging methods for different levels
    public void Debug(string message);
    public void Info(string message);
    public void Warn(string message);
    public void Error(Exception exception);
    public void ErrorNoMessage(Exception exception);
    public void Fatal(string message);
    public void NoMessage();

    // Logging methods for testing context and structured logging
    public void InfoWithContextDictionary(string message, Dictionary<string, object> context);
    public void InfoWithObjectParameter(string message, object param);
    public void InfoWithStructuredArgs(string messageTemplate, object[] args);
    public void LogMessageInNestedScopes();


    // Configuration methods to set up the logging framework for different scenarios
    public void Configure();
    public void ConfigureWithInfoLevelEnabled();
    public void ConfigurePatternLayoutAppenderForDecoration();
    public void ConfigureJsonLayoutAppenderForDecoration();

}
