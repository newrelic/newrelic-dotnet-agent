// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Internal;

[Library]
internal static class HttpClientDriver
{
    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static void Get() => Get("http://127.0.0.1");

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static void Get(string uri)
    {
        try
        {
            Thread.Sleep(5000);
            using (var client = new HttpClient())
                client.GetAsync(uri).Wait();
        }
        catch { }
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static async Task<string> CancelledGetOperation(string uri)
    {
        try
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(1);
                await client.GetStringAsync(uri);
            }
        }
        catch (Exception)
        {
            //Swallow for test purposes
        }

        return "Worked";
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static void FactoryGet(string uri)
    {
        try
        {
            using (var client = HttpClientFactory.Create())
                client.GetAsync(uri).Wait();
        }
        catch { }
    }

    // Sends a request body via HttpClient. On .NET Framework HttpClient's handler is layered over
    // HttpWebRequest, so the body goes out through HttpWebRequest.GetRequestStream and the response
    // through GetResponse - the methods the HttpWebRequest body wrappers instrument. HttpClient owns
    // the external segment and distributed-trace header injection, so those wrappers must defer and
    // not create a duplicate segment. The instrumentation tests assert exactly one external segment
    // per call, which catches any such double-instrumentation.
    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static void Post(string uri)
    {
        try
        {
            using (var client = new HttpClient())
                client.PostAsync(uri, new StringContent("request-body")).Wait();
        }
        catch { }
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static void Put(string uri)
    {
        try
        {
            using (var client = new HttpClient())
                client.PutAsync(uri, new StringContent("request-body")).Wait();
        }
        catch { }
    }
}