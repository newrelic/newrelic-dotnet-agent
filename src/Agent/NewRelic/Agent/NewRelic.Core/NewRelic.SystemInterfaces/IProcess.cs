// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.SystemInterfaces
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
    }

    public class ProcessStatic : IProcessStatic
    {
        public IProcess GetCurrentProcess()
        {
            return new Process(System.Diagnostics.Process.GetCurrentProcess());
        }
    }

    public class Process : IProcess
    {
        private readonly System.Diagnostics.Process _process;

        public Process(System.Diagnostics.Process process)
        {
            _process = process;
        }

        public string ProcessName { get { return _process.ProcessName; } }
        public int Id { get { return _process.Id; } }
        public string MainModuleFileName { get { return _process.MainModule.FileName; } }
        public DateTime StartTime { get { return _process.StartTime; } }
    }
}
