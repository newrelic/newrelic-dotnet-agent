// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// Simple test application for validating Rust profiler CLR attachment.
// When run with the profiler environment variables set, the CLR will load
// our Rust DLL and call Initialize(). This app exercises enough code paths
// to generate JIT compilation and module load events.

namespace ProfilerTestApp;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== New Relic Rust Profiler Test App ===");
        Console.WriteLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"Process ID: {Environment.ProcessId}");
        Console.WriteLine();

        // Exercise various code paths to generate JIT events
        Console.WriteLine("1. Running basic method calls...");
        var result = DoSomeWork(42);
        Console.WriteLine($"   Result: {result}");

        Console.WriteLine("2. Running async work...");
        await DoAsyncWork();

        Console.WriteLine("3. Running generic method...");
        var list = CreateList<string>("hello", "from", "profiler", "test");
        Console.WriteLine($"   List items: {string.Join(", ", list)}");

        Console.WriteLine("4. Running exception handling...");
        TryCatchWork();

        Console.WriteLine();
        Console.WriteLine("=== Test complete. Check profiler logs for events. ===");
        Console.WriteLine("Press Enter to exit (gives profiler time to flush)...");

        // If running non-interactively (e.g., from a script), exit after a brief pause
        if (Console.IsInputRedirected)
        {
            Thread.Sleep(2000);
        }
        else
        {
            Console.ReadLine();
        }
    }

    static int DoSomeWork(int input)
    {
        // Simple method that will be JIT compiled
        var sum = 0;
        for (var i = 0; i < input; i++)
        {
            sum += i;
        }
        return sum;
    }

    static async Task DoAsyncWork()
    {
        // Async method to test async context handling
        await Task.Delay(100);
        Console.WriteLine("   Async work complete");
    }

    static System.Collections.Generic.List<T> CreateList<T>(params T[] items)
    {
        // Generic method to test generic type handling
        var list = new System.Collections.Generic.List<T>(items);
        return list;
    }

    static void TryCatchWork()
    {
        // Exception handling to test try-catch JIT patterns
        try
        {
            var x = int.Parse("not_a_number");
        }
        catch (FormatException)
        {
            Console.WriteLine("   Caught expected FormatException");
        }
    }
}
