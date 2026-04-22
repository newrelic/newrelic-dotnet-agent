// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET10_0_OR_GREATER || NET481_OR_GREATER || NET8_0 || NET462

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Hangfire;

public static class TestJobs
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private static readonly Random _random = new Random();

    public static void SimpleJob(string data)
    {
        DoWork();
    }

    [AutomaticRetry(Attempts = 0)]
    public static void FailingJob()
    {
        DoWork();
        throw new InvalidOperationException("Job intentionally failed");
    }

    public static async Task SimpleAsyncJob(string data)
    {
        await DoWorkAsync();
    }

    [AutomaticRetry(Attempts = 0)]
    public static async Task FailingAsyncJob()
    {
        await DoWorkAsync();
        throw new InvalidOperationException("Job intentionally failed");
    }

    private static void DoWork()
    {
        _httpClient.GetAsync("https://httpbin.org/delay/1").Wait();
        Thread.Sleep(_random.Next(25, 100));
    }

    private static async Task DoWorkAsync()
    {
        await _httpClient.GetAsync("https://httpbin.org/delay/1");
        await Task.Delay(_random.Next(25, 100));
    }
}

#endif
