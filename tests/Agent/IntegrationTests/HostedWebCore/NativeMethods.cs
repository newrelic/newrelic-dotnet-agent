using System;
using System.Runtime.InteropServices;

namespace HostedWebCore
{
    public static class NativeMethods
    {
        [DllImport(@"inetsrv\hwebcore.dll")]
        public static extern int WebCoreActivate(
            [In, MarshalAs(UnmanagedType.LPWStr)] string appHostConfigPath,
            [In, MarshalAs(UnmanagedType.LPWStr)] string rootWebConfigPath,
            [In, MarshalAs(UnmanagedType.LPWStr)] string instanceName);

        [DllImport(@"inetsrv\hwebcore.dll")]
        public static extern int WebCoreShutdown(bool immediate);
    }
}
