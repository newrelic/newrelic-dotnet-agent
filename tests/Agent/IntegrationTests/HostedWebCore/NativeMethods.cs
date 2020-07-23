using System;
using System.Runtime.InteropServices;

namespace HostedWebCore
{
	public static class NativeMethods
	{
		[DllImport(@"inetsrv\hwebcore.dll")]
		public static extern Int32 WebCoreActivate(
			[In, MarshalAs(UnmanagedType.LPWStr)] String appHostConfigPath,
			[In, MarshalAs(UnmanagedType.LPWStr)] String rootWebConfigPath,
			[In, MarshalAs(UnmanagedType.LPWStr)] String instanceName);

		[DllImport(@"inetsrv\hwebcore.dll")]
		public static extern Int32 WebCoreShutdown(Boolean immediate);
	}
}
