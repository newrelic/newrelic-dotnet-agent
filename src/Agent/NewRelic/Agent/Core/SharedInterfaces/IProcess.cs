// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Utilities;
using System;

namespace NewRelic.Agent.Core.SharedInterfaces
{
    public interface IProcessStatic
    {
        IProcess GetCurrentProcess();
    }

    public interface IProcess
    {

        string ProcessName { get; }
        int Id { get; }

        string MainModuleFileName { get; }
        DateTime StartTime { get; }
        long PrivateMemorySize64 { get; }
        long WorkingSet64 { get; }
        System.Diagnostics.FileVersionInfo FileVersionInfo { get; }
        TimeSpan UserProcessorTime { get; }

        void Refresh();
    }

    public class ProcessStatic : IProcessStatic
    {
        private static System.Diagnostics.Process _currentSystemProcess;
        private static IProcess _currentProcess;

        public IProcess GetCurrentProcess() => _currentProcess ??= CreateProcess();

        private static IProcess CreateProcess() => new Process(_currentSystemProcess ??= GetCurrentSystemProcess());

        private static System.Diagnostics.Process GetCurrentSystemProcess() => System.Diagnostics.Process.GetCurrentProcess();
    }

    public class Process : IProcess
    {
        private readonly System.Diagnostics.Process _process;

        public Process(System.Diagnostics.Process process)
        {
            _process = process;
        }

        public string ProcessName => _process.ProcessName;
        public int Id => _process.Id;
        public string MainModuleFileName => _process.MainModule.FileName;
        public DateTime StartTime => _process.StartTime;
        public long PrivateMemorySize64 => _process.PrivateMemorySize64;
        public long WorkingSet64 => _process.WorkingSet64;
        public System.Diagnostics.FileVersionInfo FileVersionInfo => _process.MainModule.FileVersionInfo;
        public TimeSpan UserProcessorTime => _process.UserProcessorTime;

        public void Refresh()
        {
            _process.Refresh();
        }
    }
}
