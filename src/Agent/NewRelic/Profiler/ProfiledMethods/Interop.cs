/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;
//--test=NewRelic.Agent.Tests.ProfiledMethods.Interop.RequestProfile_VerifyThreadIDs
namespace NewRelic.Agent.Tests.ProfiledMethods
{
    using ThreadID = UIntPtr;
    using FunctionID = UIntPtr;
    using ThreadIDFunctionIDMap = Dictionary<UIntPtr, FunctionIDCollection>;

    class ThreadIDCollection : List<ThreadID>
    {
        public ThreadIDCollection() { }

        public ThreadIDCollection(IEnumerable<ThreadID> l) : base(l) { }
    };

    class FunctionIDCollection : List<FunctionID>
    {
        public FunctionIDCollection() { }

        public FunctionIDCollection(IEnumerable<FunctionID> l) : base(l) { }
    };


    [TestFixture]
    public class Interop
    {
        const string UnknownClass = "UnknownClass";
        const string UnknownMethod = "UnknownMethod(error)";

        void Request_profile_baseline(FunctionIDCollection functionIds, out Int32 hr)
        {
            Request_profile_baseline(functionIds, null, out hr);
        }

        void Request_profile_baseline(FunctionIDCollection functionIds)
        {
            Request_profile_baseline(functionIds, null, out Int32 _);
        }

        void Request_profile_baseline(ThreadIDCollection threadIds)
        {
            Request_profile_baseline(null, threadIds, out Int32 _);
        }

        void Request_profile_baseline(FunctionIDCollection functionIds, ThreadIDCollection threadIds)
        {
            Request_profile_baseline(functionIds, threadIds, out Int32 _);
        }

        void Request_profile_baseline(FunctionIDCollection functionIds, ThreadIDCollection threadIds, out Int32 hr)
        {
            var snapshot = NativeMethods.GetProfileWithRelease(out hr);

            Assert.GreaterOrEqual(hr, 0);
            Assert.IsNotNull(snapshot, "snapshot is null");
            Assert.Greater(snapshot.Length, 0, "snapshot.Length count is <= 0");

            foreach (var snap in snapshot)
            {
                //GetSnapshot will never return a null for the FunctionIDs
                Assert.IsNotNull(snap.FunctionIDs, "snapshot has a null list of function ids");

                Assert.AreNotEqual(snap.ThreadId, IntPtr.Zero, "ThreadId should be non-zero");
                if (snap.ThreadId != UIntPtr.Zero)
                {
                    threadIds?.Add(snap.ThreadId);
                }

                if (snap.ErrorCode < 0)
                {
                    Assert.AreEqual(snap.FunctionIDs.Length, 0, "non empty list of function ids was return with an error code.");
                }
                else
                {
                    Assert.AreNotEqual(snap.FunctionIDs.Length, 0, "empty list of function ids was return with an successful error code.");
                    functionIds?.AddRange(snap.FunctionIDs);
                }
            }
        }

        void Request_profile_baseline(ThreadIDFunctionIDMap tidfidmap, out Int32 hr)
        {
            var snapshot = NativeMethods.GetProfileWithRelease(out hr);

            Assert.GreaterOrEqual(hr, 0);
            Assert.IsNotNull(snapshot, "snapshot is null");
            Assert.Greater(snapshot.Length, 0, "snapshot.Length count is <= 0");

            foreach (var snap in snapshot)
            {
                //GetSnapshot will never return a null for the FunctionIDs
                Assert.IsNotNull(snap.FunctionIDs, "snapshot has a null list of function ids");

                Assert.AreNotEqual(snap.ThreadId, ThreadID.Zero, "ThreadId should be non-zero");

                if (snap.ErrorCode < 0)
                {
                    Assert.AreEqual(snap.FunctionIDs.Length, 0, "non empty list of function ids was return with an error code.");
                }
                else
                {
                    Assert.AreNotEqual(snap.FunctionIDs.Length, 0, "empty list of function ids was return with an successful error code.");
                    if (snap.ThreadId != ThreadID.Zero)
                    {
                        tidfidmap[snap.ThreadId] = new FunctionIDCollection(snap.FunctionIDs);
                    }
                }
            }
        }

        NativeMethods.FidTypeMethodName[] Request_function_names_baseline(FunctionIDCollection functionIds)
        {
            Assert.IsNotNull(functionIds, "functionIds is null");
            Assert.Greater(functionIds.Count, 0, "functionIds count is <= 0");
            NativeMethods.FidTypeMethodName[] results;
            try
            {
                results = NativeMethods.GetFunctionInfo(functionIds.Distinct().ToArray());
            }
            finally
            {
                NativeMethods.ShutdownThreadProfiler();
            }
            Assert.IsNotNull(results, "results from GetFunctionInfo was null");
            Assert.Greater(results.Length, 0, "results from GetFunctionInfo was empty");
            Assert.AreEqual(results[0].FunctionID, functionIds[0], "GetFunctionInfo results function id did not match");
            Assert.IsTrue(results[0].TypeName.Length != 0, "GetFunctionInfo results type name was zero length");
            Assert.IsTrue(results[0].MethodName.Length != 0, "GetFunctionInfo results method name was zero length");
            return results;
        }

        [Test]
        public void Request_profile_n_times_request_fnames()
        {
            FunctionIDCollection functionIds = new FunctionIDCollection();
            Request_profile_baseline(functionIds);
            Request_profile_baseline(functionIds);
            Request_profile_baseline(functionIds);
            Request_function_names_baseline(functionIds);
        }

        [Test]
        public void Request_profile_then_request_fnames_n_times()
        {
            FunctionIDCollection functionIds = new FunctionIDCollection();
            Request_profile_baseline(functionIds, out int hr);
            Request_function_names_baseline(functionIds);

            functionIds.Clear();
            Request_profile_baseline(functionIds, out hr);
            Request_function_names_baseline(functionIds);
        }


        [Test]
        public void Request_profile_then_rfn_rfn()
        {
            //the second request function names should return unknown/unknown
            FunctionIDCollection functionIds = new FunctionIDCollection();
            Request_profile_baseline(functionIds, out int hr);
            Request_function_names_baseline(functionIds);

            var results = Request_function_names_baseline(functionIds);

            StringAssert.AreEqualIgnoringCase(results[0].TypeName, UnknownClass);
            StringAssert.AreEqualIgnoringCase(results[0].MethodName, UnknownMethod);
        }

        [Test]
        public void RequestProfile_Abort()
        {

            Stopwatch sw = new Stopwatch();
            sw.Start();
            long baseline = sw.ElapsedTicks;
            long captureShutdownCalled = 0;
            long captureShutdownReturned = 0;
            long captureRPCalled = 0;
            long captureRPReturned = 0;
            using (var ShutdownPrimer = new ManualResetEventSlim(false))
            {
                using (var mse = new ManualResetEventSlim(false))
                {
                    void cb(object state)
                    {
                        ShutdownPrimer.Wait();
                        captureShutdownCalled = sw.ElapsedTicks;
                        NativeMethods.ShutdownThreadProfiler();
                        captureShutdownReturned = sw.ElapsedTicks;
                        mse.Set();
                    }

                    //This exists to JIT the timer class and its support
                    using (Timer t = new Timer(cb, null, 0, -1))
                    {
                        ShutdownPrimer.Set();
                        mse.Wait();
                    }
                    ShutdownPrimer.Reset();

                    using (var RecurseEvent = new ManualResetEventSlim(false))
                    {
                        RecurseEvent.Reset();
                        const int ThreadsToCreate = 10;
                        const int FramesPerThread = 10;
                        int threadsWaiting = 0;
                        void RecurseFunc(object o)
                        {
                            int depth = (int)o;
                            if (--depth == 0)
                            {
                                Interlocked.Increment(ref threadsWaiting);
                                RecurseEvent.Wait();
                            }
                            else
                                RecurseFunc(depth);
                        }

                        Thread[] threads = new Thread[ThreadsToCreate];

                        for (int i = 0; i != threads.Length; ++i)
                        {
                            threads[i] = new Thread(RecurseFunc, 4096 * 16) { IsBackground = true };
                            threads[i].Start(FramesPerThread);
                        }

                        while (threadsWaiting != ThreadsToCreate) Thread.Yield();

                        {
                            NativeMethods.RequestProfile(out IntPtr nativeSnapshots, out int sshotLength);
                            NativeMethods.ReleaseProfile();
                        }

                        captureShutdownCalled = 0;
                        captureShutdownReturned = 0;
                        mse.Reset();
                        //set up a timer to call ShutdownThreadProfiler (on another thread).
                        int snapshotLength = 0;
                        Int32 hresult = -1;
                        using (Timer t = new Timer(cb, null, 0, -1))
                        {
                            ShutdownPrimer.Set();
                            captureRPCalled = sw.ElapsedTicks;
                            hresult = NativeMethods.RequestProfile(out IntPtr nativeSnapshots, out snapshotLength);
                            captureRPReturned = sw.ElapsedTicks;

                            //free the native memory
                            NativeMethods.ReleaseProfile();
                            //wait for Shutdown cb to complete
                            mse.Wait();
                            //free all of the recurse threads to terminate
                            RecurseEvent.Set();
                            for (int i = 0; i != threads.Length; ++i) { threads[i].Join(); threads[i] = null; }
                        }

                        var msRPCalled = (double)(captureRPCalled - baseline) / 10000;
                        var msShutdownCalled = (double)(captureShutdownCalled - baseline) / 10000;
                        var msShutdownReturned = (double)(captureShutdownReturned - baseline) / 10000;
                        var msRPReturned = (double)(captureRPReturned - baseline) / 10000;

                        System.Diagnostics.Debugger.Log(100, "RequestProfile", $"  snapshotLength: {snapshotLength}");

                        System.Diagnostics.Debugger.Log(100, "RequestProfile", $"        RPCalled: {msRPCalled:0.00000}");
                        System.Diagnostics.Debugger.Log(100, "RequestProfile", $"  ShutdownCalled: {msShutdownCalled:0.00000}");
                        System.Diagnostics.Debugger.Log(100, "RequestProfile", $"      RPReturned: {msRPReturned:0.00000}");
                        System.Diagnostics.Debugger.Log(100, "RequestProfile", $"ShutdownReturned: {msShutdownReturned:0.00000}");
                        System.Diagnostics.Debugger.Log(100, "RequestProfile", $"         RP Time: {msRPReturned - msRPCalled:0.00000}");

                        Assert.AreEqual(mse.IsSet, true);
                        Assert.AreNotEqual(-1, hresult);
                        Assert.IsTrue(NativeMethods.E_ABORT == hresult || NativeMethods.E_ILLEGAL_METHOD_CALL == hresult);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void RecurseFunc(object o, ref int threadsWaiting, ConcurrentBag<UIntPtr> RecurseThreadIds, ManualResetEventSlim RecurseEvent)
        {
            int depth = (int)o;
            if (--depth == 0)
            {
                Interlocked.Increment(ref threadsWaiting);
                RecurseThreadIds.Add(NativeMethods.GetCurrentExecutionEngineThreadId());
                RecurseEvent.Wait();
            }
            else
                RecurseFunc(depth, ref threadsWaiting, RecurseThreadIds, RecurseEvent);
        }

        [Test]
        public void RequestProfile_VerifyThreadIDs()
        {
            //create a number of thread that recurses to a specified depth and block on a ManualResetEvent... this thread should save their threadids (GetCurrentExecutionEngineThreadId)
            //GetSnapshot on this thread
            //Set ManualResetEvent and allow the thread/task to complete/terminate
            //verify that all of the recurse thread ids are in the snapshot
            //call ShutdownThreadProfiler.
            using (var RecurseEvent = new ManualResetEventSlim(false))
            {
                const int ThreadsToCreate = 10;
                const int FramesPerThread = 1;
                int threadsWaiting = 0;

                var RecurseThreadIds = new ConcurrentBag<UIntPtr>();

                RecurseEvent.Reset();

                Thread[] threads = new Thread[ThreadsToCreate];

                for (int i = 0; i != threads.Length; ++i)
                {
                    threads[i] = new Thread(() => RecurseFunc(FramesPerThread, ref threadsWaiting, RecurseThreadIds, RecurseEvent), 4096 * 16) { IsBackground = true };
                    threads[i].Start();
                }

                while (threadsWaiting != ThreadsToCreate) Thread.Yield();

                var tidfidmap = new ThreadIDFunctionIDMap();
                Request_profile_baseline(tidfidmap, out Int32 hr);

                //free all of the recurse threads to terminate
                RecurseEvent.Set();
                for (int i = 0; i != threads.Length; ++i) { threads[i].Join(); threads[i] = null; }

                CollectionAssert.IsSubsetOf(RecurseThreadIds, tidfidmap.Keys);

                var fids = new FunctionIDCollection();
                foreach (var pr in tidfidmap)
                {
                    fids.AddRange(pr.Value);
                }
                var FidTypeMethodNames = Request_function_names_baseline(fids);

                Assert.IsNotNull(FidTypeMethodNames);
                Assert.Greater(FidTypeMethodNames.Length, 0);

                foreach (var tid in RecurseThreadIds)
                {
                    int matchingMethods = 0;
                    foreach (var fid in tidfidmap[tid])
                    {
                        foreach (var ftm in FidTypeMethodNames)
                        {
                            if (ftm.FunctionID == fid)
                            {
                                if (ftm.MethodName.Equals("RecurseFunc"))
                                    ++matchingMethods;
                                break;
                            }
                        }
                    }
                    Assert.AreEqual(matchingMethods, FramesPerThread);
                }

                NativeMethods.ShutdownThreadProfiler();

            }
        }


        [Test]
        public void Request_profile_without_freeing_previous()
        {
            var hresult = NativeMethods.RequestProfile(out IntPtr nativeSnapshots, out int snapshotLength);
            hresult = NativeMethods.RequestProfile(out nativeSnapshots, out snapshotLength);
            NativeMethods.ReleaseProfile();
            Assert.AreEqual(NativeMethods.E_ILLEGAL_METHOD_CALL, hresult);
        }

        [Test]
        public void Request_function_names_invalidargs()
        {
            //the second request function names should return unknown/unknown
            FunctionIDCollection functionIds = new FunctionIDCollection();
            IntPtr functionInfo;

            //Count is 0
            var result = NativeMethods.RequestFunctionNames(new UIntPtr[0], 0, out functionInfo);
            Assert.AreEqual(result, NativeMethods.E_INVALIDARG, "RequestFunctionNames did not return expected E_INVALIDARG when zero is specified as number of function ids");

            //ptr to fids is null
            result = NativeMethods.RequestFunctionNames(null, 15, out functionInfo);
            Assert.AreEqual(result, NativeMethods.E_INVALIDARG, "RequestFunctionNames did not return expected E_INVALIDARG when array of function ids is null");
        }

        [Test]
        public void Request_function_names_invalid_functionids()
        {
            //the second request function names should return unknown/unknown
            FunctionIDCollection functionIds = new FunctionIDCollection();
            {
                functionIds.Add(UIntPtr.Zero);
                var results = Request_function_names_baseline(functionIds);
                StringAssert.AreEqualIgnoringCase(results[0].TypeName, UnknownClass);
                StringAssert.AreEqualIgnoringCase(results[0].MethodName, UnknownMethod);
            }

            functionIds.Clear();
            {
                if (IntPtr.Size == sizeof(ulong))
                    functionIds.Add(new UIntPtr(unchecked((ulong)-1)));
                else
                    functionIds.Add(new UIntPtr(unchecked((uint)-1)));

                var results = Request_function_names_baseline(functionIds);
                StringAssert.AreEqualIgnoringCase(results[0].TypeName, UnknownClass);
                StringAssert.AreEqualIgnoringCase(results[0].MethodName, UnknownMethod);
            }
        }
    }

    public class NativeMethods
    {
        public const Int32 E_ILLEGAL_METHOD_CALL = unchecked((Int32)0x8000000E);
        public const Int32 E_INVALIDARG = unchecked((Int32)0x80070057);
        public const Int32 E_ABORT = unchecked((Int32)0x80004004);

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
            public Int32 ErrorCode;
            public UIntPtr[] FunctionIDs;
        };
        const string DllName = "NewRelic.Profiler.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr GetCurrentExecutionEngineThreadId();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ShutdownThreadProfiler();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ReleaseProfile();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern Int32 RequestProfile([Out] out IntPtr snapshots, [Out] out Int32 length);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern Int32 RequestFunctionNames(UIntPtr[] functionIds, int length, [Out] out IntPtr functionInfo);

        public static FidTypeMethodName[] GetFunctionInfo(UIntPtr[] functionIDs)
        {
            var res = new FidTypeMethodName[functionIDs.Length];

            var result = RequestFunctionNames(functionIDs, res.Length, out IntPtr functionInfo);
            if (result == 0)
            {
                for (int idx = 0; idx != res.Length; ++idx)
                {
                    res[idx] = (FidTypeMethodName)Marshal.PtrToStructure(functionInfo, typeof(FidTypeMethodName));
                    functionInfo += Marshal.SizeOf(res[idx]);
                }
            }
            return res;
        }

        public static ThreadSnapshot[] GetProfileWithRelease(out Int32 hr)
        {
            ThreadSnapshot[] res = new ThreadSnapshot[0];
            try
            {
                res = GetProfile(out hr);
            }
            finally
            {
                ReleaseProfile();
            }
            return res;
        }

        static readonly bool is64Bit = (sizeof(Int64) == IntPtr.Size);

        private static UIntPtr ReadUIntPtr(IntPtr address)
        {
            if (is64Bit)
            {
                return new UIntPtr(unchecked((ulong)Marshal.ReadInt64(address)));
            }
            return new UIntPtr(unchecked((uint)Marshal.ReadInt32(address)));
        }

        public static ThreadSnapshot[] GetProfile(out Int32 hr)
        {
            Int32 hresult = RequestProfile(out IntPtr nativeSnapshots, out int snapshotLength);
            hr = hresult;
            if (hresult >= 0 && IntPtr.Zero != nativeSnapshots && snapshotLength > 0)
            {
                ThreadSnapshot[] marshalledSnapshots = new ThreadSnapshot[snapshotLength];
                for (int indx = 0; indx != snapshotLength; ++indx)
                {
                    marshalledSnapshots[indx] = new ThreadSnapshot();
                    marshalledSnapshots[indx].ThreadId = ReadUIntPtr(nativeSnapshots);
                    nativeSnapshots += ThreadID.Size;
                    marshalledSnapshots[indx].ErrorCode = Marshal.ReadInt32(nativeSnapshots);
                    nativeSnapshots += sizeof(Int32);
                    // did we get stack walk? nominally 0 or 1 if the stack was too deep
                    if (marshalledSnapshots[indx].ErrorCode >= 0)
                    {
                        Int32 countOfSnapshots = Marshal.ReadInt32(nativeSnapshots);
                        nativeSnapshots += sizeof(Int32);
                        marshalledSnapshots[indx].FunctionIDs = new UIntPtr[countOfSnapshots];
                        if (countOfSnapshots > 0)
                        {
                            IntPtr FunctionIDPointer = Marshal.ReadIntPtr(nativeSnapshots);
                            for (int fidx = 0; fidx != countOfSnapshots; ++fidx, FunctionIDPointer += IntPtr.Size)
                            {
                                marshalledSnapshots[indx].FunctionIDs[fidx] = ReadUIntPtr(FunctionIDPointer);
                            }
                        }
                        nativeSnapshots += IntPtr.Size;
                    }
                    else
                    {
                        nativeSnapshots += sizeof(Int32) + IntPtr.Size;
                    }
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
