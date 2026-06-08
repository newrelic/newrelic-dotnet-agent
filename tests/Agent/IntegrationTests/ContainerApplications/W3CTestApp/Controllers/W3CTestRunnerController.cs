// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using W3CTestApp.Models;

namespace W3CTestApp.Controllers;

/// <summary>
/// Runs the official W3C trace-context python validation suite (cloned into the image at build time)
/// against this service and returns the python process exit code. An exit code of 0 means the agent's
/// W3C trace context propagation passed every spec test.
/// </summary>
[ApiController]
public class W3CTestRunnerController : ControllerBase
{
    // The trace-context repo is cloned to this path in the Dockerfile; the python tests live in the test subdirectory.
    private const string TraceContextTestDirectory = "/trace-context/test";

    // The python harness POSTs test instructions to this service. Everything runs in the same container, so localhost resolves both ways.
    private const string ServiceEndpoint = "http://127.0.0.1:80/test";

    private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(60);

    [HttpGet("runtests")]
    public async Task<IActionResult> RunTests()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "python3",
            WorkingDirectory = TraceContextTestDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add("unittest");
        startInfo.ArgumentList.Add("-v");

        startInfo.Environment["SERVICE_ENDPOINT"] = ServiceEndpoint;
        startInfo.Environment["STRICT_LEVEL"] = "1";

        var output = new StringBuilder();

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        int exitCode;
        using var cts = new CancellationTokenSource(RunTimeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
            exitCode = process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { /* best effort */ }
            output.AppendLine($"Python test run timed out after {RunTimeout.TotalSeconds} seconds.");
            exitCode = -1;
        }

        return Ok(new W3CTestRunResult { ExitCode = exitCode, Output = output.ToString() });
    }
}
