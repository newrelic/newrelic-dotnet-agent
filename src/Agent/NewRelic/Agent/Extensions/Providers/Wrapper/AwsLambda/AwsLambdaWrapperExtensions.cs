// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;

namespace NewRelic.Providers.Wrapper.AwsLambda;

internal static class AwsLambdaWrapperExtensions
{
    public static string GetTransactionCategory(IConfiguration configuration) => configuration.AwsLambdaApmModeEnabled ? "Function" : "Lambda";
}
