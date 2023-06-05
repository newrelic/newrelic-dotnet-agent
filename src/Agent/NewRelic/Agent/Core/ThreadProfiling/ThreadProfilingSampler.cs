// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core.Logging;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    /// <summary>
    /// Performs polling of the unmanaged thread profiler for samples of stack snapshots.
    /// </summary>
    public class ThreadProfilingSampler : IThreadProfilingSampler
    {
        /// <summary>
        /// Tracks the state of the background sampling worker.  1: worker has been scheduled/is running.  0: no worker has been scheduled.
        /// </summary>
        private int _workerRunning = 0;

        /// <summary>
        /// Used to signal the background thread to terminate and stop sampling
        /// </summary>
        private readonly ManualResetEventSlim _shutdownEvent = new ManualResetEventSlim(false);

        private Thread _samplingWorker = null;
        private readonly INativeMethods _nativeMethods;

        public ThreadProfilingSampler(INativeMethods nativeMethods)
        {
            _nativeMethods = nativeMethods;
        }

        public bool Start(uint frequencyInMsec, uint durationInMsec, ISampleSink sampleSink, INativeMethods nativeMethods)
        {
            _shutdownEvent.Reset();

            //atomic compare and set - if _workerRunning was a zero, it's now a 1 and create worker will be true
            bool createWorker = 0 == Interlocked.CompareExchange(ref _workerRunning, 1, 0);
            if (createWorker)
            {
                void ThreadStart() => InternalPolling_WaitCallbackAsync(frequencyInMsec, durationInMsec, sampleSink, nativeMethods).GetAwaiter().GetResult();

                _samplingWorker = new Thread(ThreadStart)
                {
                    IsBackground = true
                };
                _samplingWorker.Start();
            }

            //return whether or not we created a session
            return createWorker;
        }

        public void Stop()
        {
            //if we have already asked for termination or the background thread is not operational, we are done here.
            if (_shutdownEvent.Wait(0) || 1 == _workerRunning)
                return;

            //signal sampling worker to terminate
            _shutdownEvent.Set();

            //wait for the sampling worker to terminate
            if (_samplingWorker != null)
            {
                _samplingWorker.Join();
                _samplingWorker = null;
            }
        }

        /// <summary>
        /// Polls for profiled threads.
        /// </summary>
        private async Task InternalPolling_WaitCallbackAsync(uint frequencyInMsec, uint durationInMsec, ISampleSink sampleSink, INativeMethods nativeMethods)
        {
            int samples = 0;

            var lastTickOfSamplingPeriod = DateTime.UtcNow.AddMilliseconds(durationInMsec).Ticks;
            try
            {
                while (!_shutdownEvent.Wait((int)frequencyInMsec))
                {
                    if (DateTime.UtcNow.Ticks > lastTickOfSamplingPeriod)
                    {
                        _shutdownEvent.Set();
                        Log.Debug("InternalPolling_WaitCallback: Duration Elapsed -- Stopping Sampler");
                        break;
                    }

                    try
                    {
                        var threadSnapshots = GetProfileWithRelease(out int result);
                        if (result >= 0)
                        {
                            ++samples;
                            sampleSink.SampleAcquired(threadSnapshots);
                        }
                        else
                        {
                            Log.Error($"Thread Profile sampling failed. ({result:X})");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            finally
            {
                Log.Info($"samples ({samples})");

                await sampleSink.SamplingCompleteAsync();

                nativeMethods.ShutdownNativeThreadProfiler();
                _workerRunning = 0;
            }
        }

        private ThreadSnapshot[] GetProfileWithRelease(out int hresult)
        {
            ThreadSnapshot[] threadSnapshots = null;
            try
            {
                threadSnapshots = GetProfile(out hresult);
                //hresult is passed to caller to know if there was an error
            }
            finally
            {
                _nativeMethods.ReleaseProfile();
            }
            return threadSnapshots;
        }


        private static UIntPtr ReadUIntPtr(IntPtr address)
        {
            return (UIntPtr.Size == sizeof(uint)) ?
                new UIntPtr(unchecked((uint)Marshal.ReadInt32(address))) :
                new UIntPtr(unchecked((ulong)Marshal.ReadInt64(address)));
        }

        private ThreadSnapshot[] GetProfile(out int hresult)
        {
            hresult = _nativeMethods.RequestProfile(out IntPtr nativeSnapshots, out int snapshotLength);
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
