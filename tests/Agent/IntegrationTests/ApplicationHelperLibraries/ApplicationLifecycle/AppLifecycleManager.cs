// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using CommandLine;

namespace ApplicationLifecycle;

public class Options
{
    [Option('p', "port", Required = false, HelpText = "TCP port app should listen on")]
    public string Port { get; set; }
}

public class AppLifecycleManager
{
    private const string ShutdownChannelPrefix = "app_server_wait_for_all_request_done_";

    private const string DefaultPort = "5001";

    private static int MinutesToWait
    {
        get
        {
            // look for TEST_MINUTES_TO_WAIT env var, default to 5 minutes if not found
            var minutes = Environment.GetEnvironmentVariable("TEST_MINUTES_TO_WAIT");
            return string.IsNullOrEmpty(minutes) ? 5 : int.Parse(minutes);
        }
    }

    private static string _applicationName;

    private static readonly bool _isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    private static string ApplicationName
    {
        get
        {
            return _applicationName ?? (_applicationName = Path.GetFileNameWithoutExtension(new Uri(Assembly.GetEntryAssembly().CodeBase).LocalPath)
                                                           + (_isLinux ? string.Empty : ".exe"));
        }
    }

    public static string GetPortFromArgs(string[] args)
    {
        var portToUse = DefaultPort;

        var commandLine = string.Join(" ", args);
        Log($"Joined args: {commandLine}");

        new Parser(with => { with.IgnoreUnknownArguments = true;})
            .ParseArguments<Options>(args)
            .WithParsed(o =>
            {
                portToUse = o.Port ?? DefaultPort;
                Log($"Received port: {o.Port} | Using port: {portToUse}");
            });

        return portToUse;
    }

    public static TestCompletionSignal ArmTestCompletion(string port)
    {
        return new TestCompletionSignal(port);
    }

    public static void WaitForTestCompletion(string port)
    {
        using (var signal = ArmTestCompletion(port))
        {
            signal.Wait();
        }
    }

    // The runner opens a channel the application creates, so an application that works before it waits must arm the channel at startup.
    public sealed class TestCompletionSignal : IDisposable
    {
        private readonly string _channelName;
        private readonly EventWaitHandle _eventWaitHandle;
        private readonly NamedPipeServerStream _pipeServer;

        internal TestCompletionSignal(string port)
        {
            _channelName = ShutdownChannelPrefix + port;

            try
            {
                if (_isLinux)
                {
                    _pipeServer = new NamedPipeServerStream(_channelName, PipeDirection.In);
                }
                else
                {
                    _eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, _channelName);
                }

                Log($"Armed shutdown channel: {_channelName}");
            }
            catch (Exception e)
            {
                Log($"Failed to arm shutdown channel {_channelName}: {e.Message}");
            }
        }

        public void Wait()
        {
            if (_isLinux)
            {
                WaitForPipe();
                return;
            }

            WaitForEvent();
        }

        public void Dispose()
        {
            _eventWaitHandle?.Dispose();
            _pipeServer?.Dispose();
        }

        private void WaitForPipe()
        {
            if (_pipeServer == null)
            {
                Log("Shutdown channel was never armed; not waiting.");
                return;
            }

            var task = _pipeServer.WaitForConnectionAsync();
            if (!task.Wait(TimeSpan.FromMinutes(MinutesToWait)))
            {
                Log("Timed out waiting for test completion.");
                return;
            }

            try
            {
                using (var reader = new StreamReader(_pipeServer))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        Log($"Received shutdown message from test framework: {line}");
                    }
                }
            }
            catch (IOException e)
            {
                Log($"WaitForTestCompletion: exception: {e.Message}");
            }
        }

        private void WaitForEvent()
        {
            if (_eventWaitHandle == null)
            {
                Log("Shutdown channel was never armed; not waiting.");
                return;
            }

            Log($"Waiting for shutdown event handle: {_channelName}");
            if (!_eventWaitHandle.WaitOne(TimeSpan.FromMinutes(MinutesToWait)))
            {
                Log("Timed out waiting for shutdown event handle to be signaled.");
            }
        }
    }

    public static void CreatePidFile()
    {
        var pid = Process.GetCurrentProcess().Id;
        var applicationDirectory =
            Path.Combine(Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath), ApplicationName);
        var pidFilePath = applicationDirectory + ".pid";

        using (var file = File.CreateText(pidFilePath))
        {
            file.WriteLine(pid);
        }

        Log("PID File created: " + pidFilePath);
    }

    public static void Log(string message)
    {
        Console.WriteLine($"[{ApplicationName}] {message}");
    }
}
