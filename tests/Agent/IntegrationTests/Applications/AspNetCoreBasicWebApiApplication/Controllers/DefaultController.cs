// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreBasicWebApiApplication.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class DefaultController : ControllerBase
{
    public DefaultController()
    {
    }

    public async Task<string> MakeExternalCallUsingHttpClient(string baseAddress, string path)
    {
        using (var client = new HttpClient())
        {
            client.BaseAddress = new Uri(baseAddress);
            var response = await client.GetStringAsync(path);

            if (!string.IsNullOrEmpty(response))
            {
                return "Worked";
            }

            return "Error";
        }
    }

    public string AwesomeName()
    {
        return "Chuck Norris";
    }

    // Synchronous, inline CPU work on the request (traced) thread so the continuous-profiling sampler
    // reliably captures on-CPU stacks across several sampling intervals, producing a non-empty profile
    // for the OTLP receiver test. Kept from being inlined/optimized away so the frame stays sampleable.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoOptimization | System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public string BurnCpu(int seconds = 8)
    {
        var stopwatch = Stopwatch.StartNew();
        var deadline = TimeSpan.FromSeconds(Math.Max(1, seconds));
        long accumulator = 0;

        while (stopwatch.Elapsed < deadline)
        {
            for (var i = 0; i < 1_000_000; i++)
            {
                accumulator += i * 31 + (accumulator & 0xFF);
            }
        }

        return $"burned cpu for {seconds}s ({accumulator})";
    }
    public string GetTraceId()
    {
        return Activity.Current.TraceId.ToString();
    }
}