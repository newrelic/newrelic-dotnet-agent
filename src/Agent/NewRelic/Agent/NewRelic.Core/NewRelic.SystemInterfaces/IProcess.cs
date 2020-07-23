using System;
using JetBrains.Annotations;

namespace NewRelic.SystemInterfaces
{
    public interface IProcessStatic
    {
        [NotNull]
        IProcess GetCurrentProcess();
    }

    public interface IProcess
    {
        [CanBeNull]
        String ProcessName { get; }
        Int32 Id { get; }
        [NotNull]
        String MainModuleFileName { get; }
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
        [NotNull]
        private readonly System.Diagnostics.Process _process;

        public Process([NotNull] System.Diagnostics.Process process)
        {
            _process = process;
        }

        public string ProcessName { get { return _process.ProcessName; } }
        public int Id { get { return _process.Id; } }
        public string MainModuleFileName { get { return _process.MainModule.FileName; } }
        public DateTime StartTime { get { return _process.StartTime; } }
    }
}
