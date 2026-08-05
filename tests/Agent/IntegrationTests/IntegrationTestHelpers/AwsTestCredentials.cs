// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.IntegrationTestHelpers;

/// <summary>
/// Supplies the AWS credential environment variables needed by test applications that call
/// real AWS services. Currently used by the Bedrock LLM tests. See NR-601301.
///
/// In CI, aws-actions/configure-aws-credentials performs the GitHub OIDC exchange and exports
/// the resulting short-lived credentials into the job environment, which the test application
/// inherits. Nothing is needed here, so an empty set is returned.
///
/// Locally, the AWS SDK for .NET cannot consume an AWS SSO session on its own: SSO credential
/// resolution lives in the AWSSDK.SSO and AWSSDK.SSOOIDC packages, which the test applications
/// do not reference. The AWS CLI can resolve the session, so it is asked to materialize the
/// credentials, which are then handed to the test application as environment variables. That is
/// the same path CI uses, so local and CI runs exercise identical SDK code.
///
/// Credential values are never written to the test log. They travel only through
/// ProcessStartInfo.EnvironmentVariables, which the fixtures do not log.
/// </summary>
public static class AwsTestCredentials
{
    private const string AccessKeyIdVariable = "AWS_ACCESS_KEY_ID";
    private const string SecretAccessKeyVariable = "AWS_SECRET_ACCESS_KEY";
    private const string SessionTokenVariable = "AWS_SESSION_TOKEN";
    private const int AwsCliTimeoutMilliseconds = 30000;

    private static readonly object _resolveLock = new object();

    private static IDictionary<string, string> _resolved;

    /// <summary>
    /// Resolves the AWS credential environment variables, once per test run. Returns an empty
    /// dictionary when the ambient environment already carries credentials.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no credentials are present and the AWS CLI cannot supply them.
    /// </exception>
    public static IDictionary<string, string> GetEnvironmentVariables()
    {
        lock (_resolveLock)
        {
            return _resolved ??= Resolve();
        }
    }

    private static IDictionary<string, string> Resolve()
    {
        // Already set, which is the CI case. The test application inherits the parent
        // environment, so re-exporting would be redundant.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AccessKeyIdVariable)))
        {
            return new Dictionary<string, string>();
        }

        var exportedJson = RunAwsExportCredentials();

        JObject parsed;
        try
        {
            parsed = JObject.Parse(exportedJson);
        }
        catch (Exception ex)
        {
            // The raw output carries live credentials, so it must never reach the message.
            throw new InvalidOperationException(
                "Could not parse the output of 'aws configure export-credentials --format process'. " +
                $"Parse error: {ex.Message}");
        }

        var accessKeyId = (string)parsed["AccessKeyId"];
        var secretAccessKey = (string)parsed["SecretAccessKey"];
        var sessionToken = (string)parsed["SessionToken"];

        if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(secretAccessKey))
        {
            throw new InvalidOperationException(
                "'aws configure export-credentials' returned no usable AWS credentials. " +
                "Sign in to AWS and run the tests again.");
        }

        var variables = new Dictionary<string, string>
        {
            { AccessKeyIdVariable, accessKeyId },
            { SecretAccessKeyVariable, secretAccessKey }
        };

        // Absent for long-lived credentials, present for any assumed-role or SSO session.
        if (!string.IsNullOrEmpty(sessionToken))
        {
            variables.Add(SessionTokenVariable, sessionToken);
        }

        return variables;
    }

    private static string RunAwsExportCredentials()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "aws",
            Arguments = "configure export-credentials --format process",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("The AWS CLI process could not be started.");
            }

            // Output is a few hundred bytes, well inside the pipe buffer, so reading the
            // payload before waiting for exit cannot block.
            var standardOutput = process.StandardOutput.ReadToEnd();

            if (!process.WaitForExit(AwsCliTimeoutMilliseconds))
            {
                throw new InvalidOperationException(
                    $"The AWS CLI did not exit within {AwsCliTimeoutMilliseconds}ms.");
            }

            if (process.ExitCode != 0)
            {
                var standardError = process.StandardError.ReadToEnd();
                throw new InvalidOperationException(
                    $"'aws configure export-credentials' exited with code {process.ExitCode}. " +
                    $"{standardError.Trim()}");
            }

            return standardOutput;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Could not run the AWS CLI to resolve credentials for the Bedrock tests. Confirm the " +
                "AWS CLI is installed and on PATH, and that you are signed in to AWS. " +
                $"Underlying error: {ex.Message}");
        }
    }
}
