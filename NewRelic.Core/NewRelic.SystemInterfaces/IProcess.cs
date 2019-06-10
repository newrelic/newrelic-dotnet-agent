using NewRelic.Memoization;
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
		long PrivateMemorySize64 { get; }
		long VirtualMemorySize64 { get; }
		long WorkingSet64 { get; }
		System.Diagnostics.FileVersionInfo FileVersionInfo { get; }
		TimeSpan UserProcessorTime { get; }
	}

	public class ProcessStatic : IProcessStatic
	{
		private static System.Diagnostics.Process _currentSystemProcess;
		private static IProcess _currentProcess;

		public IProcess GetCurrentProcess()
		{
			return Memoizer.Memoize(ref _currentProcess, CreateProcess);
		}

		private static IProcess CreateProcess()
		{
			return new Process(Memoizer.Memoize(ref _currentSystemProcess, GetCurrentSystemProcess));
		}

		private static System.Diagnostics.Process GetCurrentSystemProcess()
		{
			return System.Diagnostics.Process.GetCurrentProcess();
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
		public long PrivateMemorySize64 { get { return _process.PrivateMemorySize64; } }
		public long VirtualMemorySize64 { get { return _process.VirtualMemorySize64; } }
		public long WorkingSet64 { get { return _process.WorkingSet64; } }
		public System.Diagnostics.FileVersionInfo FileVersionInfo { get { return _process.MainModule.FileVersionInfo; } }
		public TimeSpan UserProcessorTime { get { return _process.UserProcessorTime; } }
	}
}
