// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
#if NETFRAMEWORK
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
#endif

namespace NewRelic.OtelDistro.Coordinator;

/// <summary>
/// Finds or creates the process's single <see cref="RegistrationCoordinator"/>. On .NET Framework it lives in the
/// process's default AppDomain, reached from any domain; on .NET it is an in-process singleton. Thin adapter over
/// <see cref="CoordinatorSlot"/>: the COM, mutex and AppDomain lines are proven by live runs, not unit tests.
/// </summary>
public static class CoordinatorHost
{
    private const string LocationInProcess = "in-process";

#if !NETFRAMEWORK
    private static readonly object InProcessGate = new object();
    private static readonly InProcessSlot ProcessSlot = new InProcessSlot();
#endif

    /// <summary>
    /// Returns the process's coordinator, creating it on first use. Never throws: returns null and sets
    /// <paramref name="failure"/> ("&lt;step&gt;: &lt;ExceptionType&gt;: &lt;message&gt;") when it is unavailable.
    /// <paramref name="location"/> is "default AppDomain" or "in-process" on success, null on failure.
    /// </summary>
    public static RegistrationCoordinator TryGet(out string location, out string failure)
    {
        location = null;
        failure = null;
        try
        {
            var expectedVersion = typeof(RegistrationCoordinator).Assembly.FullName;
            RegistrationCoordinator coordinator;
#if NETFRAMEWORK
            coordinator = TryGetFramework(expectedVersion, out location, out failure);
#else
            lock (InProcessGate)
            {
                coordinator = CoordinatorSlot.FindOrCreate(ProcessSlot, expectedVersion, () => new RegistrationCoordinator(), out _, out failure);
            }

            location = coordinator == null ? null : LocationInProcess;
#endif
            return coordinator;
        }
        catch (Exception ex)
        {
            location = null;
            failure = CoordinatorSlot.Describe("locate", ex);
            return null;
        }
    }

#if !NETFRAMEWORK
    private sealed class InProcessSlot : ICoordinatorSlot
    {
        private object _value;

        public object Get() => _value;

        public void Set(object value) => _value = value;
    }
#else
    private const string LocationDefaultDomain = "default AppDomain";
    private const string MutexPrefix = "NewRelic.OtelDistro.Coordinator.";
    private static readonly TimeSpan MutexWait = TimeSpan.FromSeconds(10);

    private static int _resolverInstalled;

    private static RegistrationCoordinator TryGetFramework(string expectedVersion, out string location, out string failure)
    {
        location = null;
        failure = null;

        AppDomain target;
        Func<RegistrationCoordinator> create;
        string where;
        var current = AppDomain.CurrentDomain;
        if (current.IsDefaultAppDomain())
        {
            // Already the default domain: no COM activation. Other domains reach this same slot through the default domain's data.
            target = current;
            create = () => new RegistrationCoordinator();
            where = LocationInProcess;
        }
        else
        {
            try
            {
                target = GetDefaultDomain();
            }
            catch (Exception ex)
            {
                failure = CoordinatorSlot.Describe("default domain", ex);
                return null;
            }

            // Every non-default domain needs this before the slot read, not just the creator: reading the slot unwraps a proxy
            // whose type must bind to the copy already loaded here, even when it was loaded from outside the probing path.
            InstallAssemblyResolver(typeof(RegistrationCoordinator).Assembly);

            create = () => CreateInDomain(target);
            where = LocationDefaultDomain;
        }

        // One mutex per process serialises find-or-create across all AppDomains. The wait is bounded so a stalled holder
        // makes this domain fall back instead of hanging app start-up.
        int processId;
        using (var process = Process.GetCurrentProcess())
        {
            processId = process.Id;
        }

        using (var mutex = new Mutex(false, MutexPrefix + processId))
        {
            bool acquired;
            try
            {
                acquired = mutex.WaitOne(MutexWait);
            }
            catch (AbandonedMutexException)
            {
                // A previous owner died while holding it; ownership is ours now and the slot logic is idempotent.
                acquired = true;
            }

            if (!acquired)
            {
                failure = "mutex: timed out after " + (int)MutexWait.TotalSeconds + " s";
                return null;
            }

            try
            {
                var coordinator = CoordinatorSlot.FindOrCreate(new DomainDataSlot(target), expectedVersion, create, out _, out failure);
                location = coordinator == null ? null : where;
                return coordinator;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private sealed class DomainDataSlot : ICoordinatorSlot
    {
        private readonly AppDomain _domain;

        public DomainDataSlot(AppDomain domain)
        {
            _domain = domain;
        }

        public object Get() => _domain.GetData(CoordinatorSlot.SlotName);

        public void Set(object value) => _domain.SetData(CoordinatorSlot.SlotName, value);
    }

    private static RegistrationCoordinator CreateInDomain(AppDomain domain)
    {
        var assembly = typeof(RegistrationCoordinator).Assembly;
        var path = assembly.Location;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            throw new InvalidOperationException("coordinator assembly has no file location");
        }

        return (RegistrationCoordinator)domain.CreateInstanceFromAndUnwrap(path, typeof(RegistrationCoordinator).FullName);
    }

    internal static void InstallAssemblyResolver(Assembly coordinatorAssembly)
    {
        if (Interlocked.Exchange(ref _resolverInstalled, 1) != 0)
        {
            return;
        }

        var simpleName = coordinatorAssembly.GetName().Name;
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            string.Equals(new AssemblyName(args.Name).Name, simpleName, StringComparison.Ordinal) ? coordinatorAssembly : null;
    }

    private static AppDomain GetDefaultDomain()
    {
        // ICLRMetaHost route first: it needs no COM registration. The legacy CorRuntimeHost activation is the fallback.
        try
        {
            return GetDefaultDomainViaMetaHost();
        }
        catch (Exception metaHostFailure)
        {
            try
            {
                return GetDefaultDomainViaLegacyActivation();
            }
            catch (Exception legacyFailure)
            {
                throw new InvalidOperationException(
                    "MetaHost: " + metaHostFailure.GetType().Name + ": " + metaHostFailure.Message +
                    "; legacy: " + legacyFailure.GetType().Name + ": " + legacyFailure.Message);
            }
        }
    }

    // CLRCreateInstance -> ICLRMetaHost.GetRuntime("v4.0.30319") -> ICLRRuntimeInfo.GetInterface(CLSID_CorRuntimeHost,
    // IID_ICorRuntimeHost) -> ICorRuntimeHost.GetDefaultDomain().
    private static AppDomain GetDefaultDomainViaMetaHost()
    {
        var clsidMetaHost = new Guid("9280188D-0E8E-4867-B30C-7FA83884E8DE");
        var iidMetaHost = typeof(ICLRMetaHost).GUID;
        var iidRuntimeInfo = typeof(ICLRRuntimeInfo).GUID;
        var clsidCorRuntimeHost = new Guid("CB2F6723-AB3A-11D2-9C40-00C04FA30A3E");
        var iidCorRuntimeHost = typeof(ICorRuntimeHost).GUID;

        var metaHost = (ICLRMetaHost)CLRCreateInstance(ref clsidMetaHost, ref iidMetaHost);
        var runtimeInfo = (ICLRRuntimeInfo)metaHost.GetRuntime("v4.0.30319", ref iidRuntimeInfo);
        var corHost = (ICorRuntimeHost)runtimeInfo.GetInterface(ref clsidCorRuntimeHost, ref iidCorRuntimeHost);
        corHost.GetDefaultDomain(out var domain);
        return (AppDomain)domain;
    }

    private static AppDomain GetDefaultDomainViaLegacyActivation()
    {
        var clsidCorRuntimeHost = new Guid("CB2F6723-AB3A-11D2-9C40-00C04FA30A3E");
        var hostType = Type.GetTypeFromCLSID(clsidCorRuntimeHost, true);
        var corHost = (ICorRuntimeHost)Activator.CreateInstance(hostType);
        corHost.GetDefaultDomain(out var domain);
        return (AppDomain)domain;
    }

    // The return needs an explicit Interface marshal: without it the object return is marshalled as a VARIANT and the
    // call fails with "Specified OLE variant is invalid." (the failure the spike hit).
    [DllImport("mscoree.dll", CallingConvention = CallingConvention.StdCall, PreserveSig = false)]
    [return: MarshalAs(UnmanagedType.Interface)]
    private static extern object CLRCreateInstance(ref Guid clsid, ref Guid riid);

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("D332DB9E-B9B3-4125-8207-A14884F53216")]
    private interface ICLRMetaHost
    {
        [return: MarshalAs(UnmanagedType.Interface)]
        object GetRuntime([MarshalAs(UnmanagedType.LPWStr)] string version, ref Guid iid);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("BD39D1D2-BA2F-486A-89B0-B4B0CB466891")]
    private interface ICLRRuntimeInfo
    {
        void GetVersionString_Unused();
        void GetRuntimeDirectory_Unused();
        void IsLoaded_Unused();
        void LoadErrorString_Unused();
        void LoadLibrary_Unused();
        void GetProcAddress_Unused();
        [return: MarshalAs(UnmanagedType.Interface)]
        object GetInterface(ref Guid clsid, ref Guid iid);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("CB2F6722-AB3A-11D2-9C40-00C04FA30A3E")]
    private interface ICorRuntimeHost
    {
        void CreateLogicalThreadState_Unused();
        void DeleteLogicalThreadState_Unused();
        void SwitchinLogicalThreadState_Unused();
        void SwitchoutLogicalThreadState_Unused();
        void LocksHeldByLogicalThread_Unused();
        void MapFile_Unused();
        void GetConfiguration_Unused();
        void Start_Unused();
        void Stop_Unused();
        void CreateDomain_Unused();
        void GetDefaultDomain([MarshalAs(UnmanagedType.IUnknown)] out object appDomain);
    }
#endif
}
