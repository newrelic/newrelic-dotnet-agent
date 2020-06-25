/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace FunctionalTests.Helpers
{
    public class ImpersonateUser : IDisposable
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool LogonUser(string lpszUsername, string lpszDomain, string lpszPassword, int dwLogonType, int dwLogonProvider, out IntPtr phToken);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private IntPtr userHandle = IntPtr.Zero;
        private WindowsImpersonationContext impersonationContext;

        public ImpersonateUser(string user, string domain, string password)
        {
            if (!string.IsNullOrEmpty(user))
            {
                // Call LogonUser to get a token for the user
                bool loggedOn = LogonUser(user, domain, password, 9, 3, out userHandle);
                if (!loggedOn)
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                // Begin impersonating the user
                impersonationContext = WindowsIdentity.Impersonate(userHandle);
            }
        }

        public void Dispose()
        {
            if (userHandle != IntPtr.Zero)
                CloseHandle(userHandle);
            if (impersonationContext != null)
                impersonationContext.Undo();
        }
    }
}
