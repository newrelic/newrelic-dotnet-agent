// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using Amazon.Lambda.Core;
using NewRelic.Api.Agent;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaFunctionTestApp; // TODO: This assembly and namespace are currently hard-coded in the AwsLambdaWrapper instrumentation.xml - that obviously has to change at some point

public class Function
{
    public IEnumerable<KeyValuePair<string, string>> FunctionHandler(string input, ILambdaContext context)
    {
        return GetEnvironmentVariables();
    }

    [Transaction]
    private static IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        var envVars = new List<KeyValuePair<string, string>>();

        foreach (DictionaryEntry environmentVariable in Environment.GetEnvironmentVariables())
        {
            envVars.Add(new KeyValuePair<string, string>(environmentVariable.Key as string, environmentVariable.Value as string));
        }

        return envVars.OrderBy(kvp => kvp.Key);
    }
}
