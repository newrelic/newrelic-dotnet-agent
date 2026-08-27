// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Configuration;

/// <summary>
/// The OpenTelemetry environment variables the agent translates to New Relic configuration,
/// per the OpenTelemetry Bridge configuration spec.
/// </summary>
public static class OpenTelemetryEnvironmentVariables
{
    public const string Prefix = "OTEL_";

    public const string ServiceName = "OTEL_SERVICE_NAME";
    public const string LogLevel = "OTEL_LOG_LEVEL";
    public const string SdkDisabled = "OTEL_SDK_DISABLED";
}
