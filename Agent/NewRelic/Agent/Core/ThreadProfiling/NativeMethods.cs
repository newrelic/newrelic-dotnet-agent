using System;
using System.Runtime.InteropServices;

namespace NewRelic.Agent.Core.ThreadProfiling
{
	[StructLayout(LayoutKind.Sequential)]
	public class FidTypeMethodName
	{
		public UIntPtr FunctionID;
		[MarshalAs(UnmanagedType.LPWStr)]
		public String TypeName;
		[MarshalAs(UnmanagedType.LPWStr)]
		public String MethodName;
	};

	[StructLayout(LayoutKind.Sequential)]
	public struct ThreadSnapshot
	{
		public UIntPtr ThreadId;
		public int ErrorCode;
		public UIntPtr[] FunctionIDs;
	};

	public class LinuxNativeMethods : INativeMethods
	{
		private const string DllName = "NewRelicProfiler";

		[DllImport(DllName, EntryPoint = "InstrumentationRefresh", CallingConvention = CallingConvention.Cdecl)]
		private static extern int ExternInstrumentationRefresh();

		[DllImport(DllName, EntryPoint = "AddCustomInstrumentation", CallingConvention = CallingConvention.Cdecl)]
		private static extern int ExternAddCustomInstrumentation(string fileName, string xml);

		[DllImport(DllName, EntryPoint = "ApplyCustomInstrumentation", CallingConvention = CallingConvention.Cdecl)]
		private static extern int ExternApplyCustomInstrumentation();

		public int InstrumentationRefresh()
		{
			return ExternInstrumentationRefresh();
		}

		public int AddCustomInstrumentation(string fileName, string xml)
		{
			return ExternAddCustomInstrumentation(fileName, xml);
		}

		public int ApplyCustomInstrumentation()
		{
			return ExternApplyCustomInstrumentation();
		}

		public void ShutdownNativeThreadProfiler()
		{
			throw new NotImplementedException("ShutdownNativeThreadProfiler");
		}

		public FidTypeMethodName[] GetFunctionInfo(UIntPtr[] functionIDs)
		{
			throw new NotImplementedException("GetFunctionInfo");
		}

		public ThreadSnapshot[] GetProfileWithRelease(out int hr)
		{
			throw new NotImplementedException("GetProfileWithRelease");
		}
	}

	public class WindowsNativeMethods : INativeMethods
	{
		private const string DllName = "NewRelic.Profiler.dll";

		[DllImport(DllName, EntryPoint = "InstrumentationRefresh", CallingConvention = CallingConvention.Cdecl)]
		private static extern int ExternInstrumentationRefresh();

		[DllImport(DllName, EntryPoint = "AddCustomInstrumentation", CallingConvention = CallingConvention.Cdecl)]
		private static extern int ExternAddCustomInstrumentation(string fileName, string xml);

		[DllImport(DllName, EntryPoint = "ApplyCustomInstrumentation", CallingConvention = CallingConvention.Cdecl)]
		private static extern int ExternApplyCustomInstrumentation();

		public int InstrumentationRefresh()
		{
			return ExternInstrumentationRefresh();
		}

		public int AddCustomInstrumentation(string fileName, string xml)
		{
			return ExternAddCustomInstrumentation(fileName, xml);
		}

		public int ApplyCustomInstrumentation()
		{
			return ExternApplyCustomInstrumentation();
		}


		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ShutdownThreadProfiler();

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ReleaseProfile();

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int RequestProfile([Out] out IntPtr snapshots, [Out] out int length);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int RequestFunctionNames(UIntPtr[] functionIds, int length, [Out] out IntPtr functionInfo);

		public void ShutdownNativeThreadProfiler()
		{
			ShutdownThreadProfiler();
		}

		public FidTypeMethodName[] GetFunctionInfo(UIntPtr[] functionIDs)
		{
			//get these once and not each iteration of the loop
			var typeOfFidTypeMethodName = typeof(FidTypeMethodName);
			var sizeOfFidTypeMethodName = Marshal.SizeOf(typeOfFidTypeMethodName);

			var result = RequestFunctionNames(functionIDs, functionIDs.Length, out IntPtr functionInfo);
			if (result == 0)
			{
				var typeMethodNames = new FidTypeMethodName[functionIDs.Length];
				for (int idx = 0; idx != typeMethodNames.Length; ++idx)
				{
					typeMethodNames[idx] = (FidTypeMethodName)Marshal.PtrToStructure(functionInfo, typeOfFidTypeMethodName);
					functionInfo += sizeOfFidTypeMethodName;
				}
				return typeMethodNames;
			}
			return new FidTypeMethodName[0];
		}

		public ThreadSnapshot[] GetProfileWithRelease(out int hresult)
		{
			ThreadSnapshot[] threadSnapshots = null;
			try
			{
				threadSnapshots = GetProfile(out hresult);
				//hresult is passed to caller to know if there was an error
			}
			finally
			{
				ReleaseProfile();
			}
			return threadSnapshots;
		}


		private static UIntPtr ReadUIntPtr(IntPtr address)
		{
			return (UIntPtr.Size == sizeof(UInt32)) ?
				new UIntPtr(unchecked((uint)Marshal.ReadInt32(address))) :
				new UIntPtr(unchecked((ulong)Marshal.ReadInt64(address)));
		}

		public static ThreadSnapshot[] GetProfile(out int hresult)
		{
			hresult = RequestProfile(out IntPtr nativeSnapshots, out int snapshotLength);
			if (hresult >= 0 && IntPtr.Zero != nativeSnapshots && snapshotLength > 0)
			{
				var marshalledSnapshots = new ThreadSnapshot[snapshotLength];
				for (int indx = 0; indx != snapshotLength; ++indx)
				{
					var marshalled = new ThreadSnapshot();
					marshalled.ThreadId = ReadUIntPtr(nativeSnapshots);
					nativeSnapshots += UIntPtr.Size;
					marshalled.ErrorCode = Marshal.ReadInt32(nativeSnapshots);
					nativeSnapshots += sizeof(int);
					// did we get stack walk? nominally 0 or 1 if the stack was too deep
					if (marshalled.ErrorCode >= 0)
					{
						var countOfSnapshots = Marshal.ReadInt32(nativeSnapshots);
						nativeSnapshots += sizeof(int);
						marshalled.FunctionIDs = new UIntPtr[countOfSnapshots];
						if (countOfSnapshots > 0)
						{
							var FunctionIDPointer = Marshal.ReadIntPtr(nativeSnapshots);
							for (int fidx = 0; fidx != countOfSnapshots; ++fidx, FunctionIDPointer += IntPtr.Size)
							{
								marshalled.FunctionIDs[fidx] = ReadUIntPtr(FunctionIDPointer);
							}
						}
						nativeSnapshots += IntPtr.Size;
					}
					else
					{
						nativeSnapshots += sizeof(int) + IntPtr.Size;
					}
					marshalledSnapshots[indx] = marshalled;
				}
				return marshalledSnapshots;
			}
			else
			{
				return new ThreadSnapshot[0];
			}
		}
	}
}
