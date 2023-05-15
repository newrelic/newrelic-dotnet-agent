// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.Logging;
using NewRelic.SystemExtensions.Collections.Generic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable 649 // Unassigned fields. This should be removed when we support thread profiling in the NETSTANDARD2_0 build.

namespace NewRelic.Agent.Core.ThreadProfiling
{
    public class ThreadProfilingService : ConfigurationBasedService, IThreadProfilingSessionControl, IThreadProfilingProcessing, ISampleSink
    {
        private const int InvalidSessionId = 0;
        private readonly INativeMethods _nativeMethods;
        private readonly IDataTransportService _dataTransportService;
        private ThreadProfilingSampler _sampler;
        private int _profileSessionId;
        private DateTime _startSessionTime;
        private DateTime _stopSessionTime;
        private readonly Dictionary<UIntPtr, ClassMethodNames> _functionNames = new Dictionary<UIntPtr, ClassMethodNames>();
        private readonly object _syncObjFunctionNames = new object();
        private readonly int _maxAggregatedNodes;

        /// <summary>
        /// This is incremented every time a sample is acquired.
        /// </summary>
        private int _numberSamplesInSession = 0;

        /// <summary>
        /// Passed to the profiling session via the StopThreadProfilerCommand. When profiling is started this value is defaulted to true.
        /// It's used primarily by the sampling completing method to control whether or not we send the profile samples to the collector.
        /// </summary>
        private volatile bool _reportData = true;

        // i.e.,  this is a dictionary of ManagedThreadId, Total Call Count
        private readonly Dictionary<UIntPtr, int> _managedThreadsFromProfiler = new Dictionary<UIntPtr, int>();

        private readonly ThreadProfilingBucket _threadProfilingBucket;

        // The pruning list maintains a reference to all TreeNodes created. 
        // After collecting the thread profiles, if the number of nodes
        // exceeds the _maxAggregatedNodes, the pruning list will be
        // sorted and pruned.
        public ArrayList PruningList { get; private set; }

        // Sync object used to serialize access to the three thread lists. Don't expect access to occur
        // often enough to warrant three separate synchronization objects. Optimize later if necessary.
        private readonly object _syncObjFailedProfiles = new object();

        /// <summary>
        /// Count by thread Id of failed thread profiles received from unmanaged thread profiler.
        /// </summary>
        private readonly Dictionary<UIntPtr, uint> _failedThreads = new Dictionary<UIntPtr, uint>();
        private readonly Dictionary<UIntPtr, int> _failedThreadErrorCodes = new Dictionary<UIntPtr, int>();

        /// <summary>
        /// List of thread ids where the stack trace was large (greater than 2000)
        /// </summary>
        private readonly List<UIntPtr> _largeStackOverflows = new List<UIntPtr>();

        #region Construction and Initializations

        public ThreadProfilingService(IDataTransportService dataTransportService, INativeMethods nativeMethods, int maxAggregatedNodes = 20000)
        {
            _dataTransportService = dataTransportService;
            _maxAggregatedNodes = maxAggregatedNodes;
            _nativeMethods = nativeMethods;

            _threadProfilingBucket = new ThreadProfilingBucket(this);
            PruningList = new ArrayList();
        }

        #endregion

        #region Service Start/Stop

        /// <summary>
        /// This function initializes components of the <see cref="ThreadProfilingService"/>
        /// that will be used for thread profiling.
        /// </summary>
        /// <remarks>
        /// Thread Profiling could potentially be always-on but that is not how New Relic agents currently support the
        /// feature, so this service initialization does not actually start thread profiling. It just prepares the components such 
        /// as the aggregation thread and the unmanaged connection so that thread profiling can later be turned on using the 
        /// <see cref="ThreadProfilingService.StartThreadProfilingSession"/> function.
        /// </remarks>
        public void Start()
        {
        }

        /// <summary>
        /// Stops the <see cref="ThreadProfilingService"/> service. This will halt a 
        /// thread profiling session that might be running.
        /// </summary>
        public void Stop()
        {
            // Shutdown a running thread profiling session.
            StopThreadProfilingSession(_profileSessionId);
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
            // If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).
        }

        #endregion

        #region Profiling Start/Stop

        /// <summary>
        /// Starts a new thread profiling session.
        /// </summary>
        /// <param name="profileSessionId">A unique identifier received from the collector identifying this thread profiling session.</param>
        /// <param name="frequencyInMsec">The sampling interval.</param>
        /// <param name="durationInMsec">The total time for the thread profiling session.</param>
        /// <returns>true if a new thread profiling session is started. false if one already exists.</returns>
        public bool StartThreadProfilingSession(int profileSessionId, uint frequencyInMsec, uint durationInMsec)
        {
            Log.Info($"Starting a thread profiling session {{ SessionId: {profileSessionId}, SamplePeriodMs: {frequencyInMsec}, DurationMs: {durationInMsec} }}");
            var startedNewSession = false;

            try
            {
                if (_sampler == null)
                {
                    _sampler = new ThreadProfilingSampler(_nativeMethods);
                }

                // Remove existing data in tree and cache buffers
                ResetCache();

                _reportData = true;

                startedNewSession = _sampler.Start(frequencyInMsec, durationInMsec, this, _nativeMethods);

                if (startedNewSession)
                {
                    _startSessionTime = DateTime.UtcNow;
                    _profileSessionId = profileSessionId;
                    _numberSamplesInSession = 0;
                }
            }
            catch (Exception e)
            {
                Log.ErrorFormat("Failed to start thread profiler: {0}", e);
            }

            return startedNewSession;
        }

        public bool StopThreadProfilingSession(int profileId, bool reportData = true)
        {
            if (_sampler == null)
                return false;

            if (_profileSessionId != InvalidSessionId && _profileSessionId != profileId)
            {
                Log.WarnFormat("A request to stop a thread profiling session was made. Requesting profile Id = {0}. In process profile Id = {1}", profileId, _profileSessionId);
                return false;
            }

            _reportData = reportData;

            _sampler.Stop();

            _profileSessionId = InvalidSessionId;
            ResetCache();

            return true;
        }
        public void SampleAcquired(ThreadSnapshot[] threadSnapshots)
        {

            foreach (var snapshot in threadSnapshots)
            {
                int errorCode = snapshot.ErrorCode;
                var threadId = snapshot.ThreadId;
                string branchDescription = string.Empty;
                try
                {
                    if (errorCode == 1)
                    {
                        branchDescription = "LargeStackOverflow";
                        AddLargeStackOverflowProfile(threadId);
                    }
                    else if (errorCode != 0)
                    {
                        branchDescription = "FailedProfileThreadData";
                        AddFailedThreadProfile(threadId, errorCode);
                    }
                    else
                    {
                        branchDescription = "ProfiledThreadData";
                        UpdateTree(threadId, snapshot.FunctionIDs);
                    }
                }
                catch (Exception e)
                {
                    Log.Debug(branchDescription + " EXCEPTION : " + e.ToString());
                }
            }

            ++_numberSamplesInSession;
        }

        /// <summary>
        /// This is called by the sampler prior to terminating the native thread profiler which will reset all of the resources including the name cache.
        /// </summary>
        public async Task SamplingCompleteAsync()
        {
            if (_reportData)
            {
                await PerformAggregationAsync();
            }
        }

        #endregion

        #region Failed Thread Profiles

        private void AddLargeStackOverflowProfile(UIntPtr threadId)
        {
            // Using same sync object for both large stack overflows and failed thread profiles
            // since the former is very rare and having another sync object seems like overkill.
            lock (_syncObjFailedProfiles)
            {
                if (!_largeStackOverflows.Contains(threadId))
                {
                    _largeStackOverflows.Add(threadId);
                }
            }
        }

        private void AddFailedThreadProfile(UIntPtr threadId, int errorCode)
        {
            lock (_syncObjFailedProfiles)
            {
                if (!_failedThreads.ContainsKey(threadId))
                {
                    _failedThreads.Add(threadId, 1);
                }
                else
                {
                    _failedThreads[threadId]++;
                }

                if (!_failedThreadErrorCodes.ContainsKey(threadId))
                {
                    _failedThreadErrorCodes.Add(threadId, errorCode);
                }
            }
        }

        // Just logging the counts. If necessary, can look at the actual thread Ids.
        private void LogFailedProfiles()
        {
            if (_largeStackOverflows.Count > 0)
                Log.DebugFormat("The agent was not able to retrieve the entire stack for {0} managed threads.", _largeStackOverflows.Count);

            if (_failedThreads.Count > 0)
                Log.DebugFormat("The agent was not able to retrieve a stack trace for {0} managed threads because it would be unsafe for the CLR or it was during JIT compilation or garbage collection.", _failedThreads.Count);

            Log.Finest("The Failed thread error codes:");
            foreach (var pair in _failedThreadErrorCodes)
            {
                Log.FinestFormat("ThreadId: {0}  ErrorCode: {1}", pair.Key, pair.Value);
            }
        }

        #endregion

        #region Bucket Tree Management

        private void UpdateTree(UIntPtr threadId, UIntPtr[] fids)
        {
            if (null != fids && fids.Length > 0)
            {
                _managedThreadsFromProfiler[threadId] = _managedThreadsFromProfiler.GetValueOrDefault(threadId) + fids.Length;

                _threadProfilingBucket.UpdateTree(fids);
            }
        }

        public void AddNodeToPruningList(ProfileNode node)
        {
            PruningList.Add(node);
        }

        // for unit testing only
        public int GetTotalBucketNodeCount()
        {
            return _threadProfilingBucket.GetNodeCount();
        }
        #endregion

        #region Aggregation Process

        public async Task PerformAggregationAsync()
        {
            try
            {
                _stopSessionTime = DateTime.UtcNow;

                Log.FinestFormat("Starting Aggregation process at {0}:{1}:{2}:{3}",
                    _stopSessionTime.Hour, _stopSessionTime.Minute, _stopSessionTime.Second, _stopSessionTime.Millisecond);

                ResolveFunctionNames();
                UpdateRunnableCounts();
                SortPruningTree();
                _threadProfilingBucket.PruneTree();

                var profileData = SerializeData();

                await _dataTransportService.SendThreadProfilingDataAsync(profileData);

                LogFailedProfiles();

                _profileSessionId = InvalidSessionId;
            }
            catch (Exception e)
            {
                var msg = new StringBuilder(e.Message);
                if (e.InnerException != null)
                {
                    msg.Append("; ");
                    msg.Append(e.InnerException.Message);
                }
                Log.ErrorFormat("Exception performing thread profiling data aggregation: {0}", msg);
            }
        }

        public void SortPruningTree()
        {
            if (PruningList.Count <= _maxAggregatedNodes)
                return;

            var treeNodeComparer = new ProfileNodeComparer();
            PruningList.Sort(treeNodeComparer);

            for (var i = _maxAggregatedNodes; i < PruningList.Count; i++)
            {
                var node = ((ProfileNode)PruningList[i]);
                if (node == null)
                    continue;

                node.IgnoreForReporting = true;
            }
        }

        #endregion

        private IEnumerable<ThreadProfilingModel> SerializeData()
        {
            var samples = new Dictionary<string, object>();
            if (_threadProfilingBucket.Tree.Root.Children.Count > 0)
                samples.Add("OTHER", _threadProfilingBucket.Tree.Root.Children);

            // Note: runnable thread count will always equal total thread count since we don't track the difference.
            var threadCount = _managedThreadsFromProfiler.Count;
            var model = new ThreadProfilingModel(_profileSessionId, _startSessionTime, _stopSessionTime, _numberSamplesInSession, samples, threadCount, threadCount);

            // We only ever have one set of data, but collector expects an array of data
            return new[] { model };
        }

        #region Resolving Function Ids as Class and Method Names

        /// <summary>
        /// Using the native callback, RequestClassFunctionNameCallback, retrieves the managed code
        /// class and method names for all function ids in the profile buckets.
        /// </summary>
        /// <remarks>
        /// As they are retrieved, the class and function names are stored in _functionNames dictionary,
        /// from which they are later used to populate the FunctionNodes. This appears to be most
        /// efficient since there are likely to be duplicates across the bucket trees.
        /// </remarks>
        private void ResolveFunctionNames()
        {
            var fids = _threadProfilingBucket.GetFunctionIds().ToArray();

            // this calls the profiler.  It creates a thread to look up function ids and
            // joins on the thread so it should block until after it has called back with the
            // function info.

            PopulateFunctionNameCache(fids);

            lock (_syncObjFunctionNames)
            {
                if (Log.IsFinestEnabled)
                {
                    foreach (var id in fids)
                    {
                        if (!_functionNames.TryGetValue(id, out ClassMethodNames name))
                        {
                            Log.FinestFormat("ThreadProfilingService function lookup failed for id {0}", id);
                        }
                    }
                }
                _threadProfilingBucket.PopulateNames(_functionNames);
            }
        }

        /// <summary>
        /// Calls the unmanaged profiler with a list of function ids to fetch.  For each function the id, assembly and type 
        /// name will be returned through the RequestFunctionNamesFunction.  Note that those calls happen on another thread.
        /// </summary>
        private void PopulateFunctionNameCache(UIntPtr[] functionIds)
        {
            try
            {
                var typeMethodNames = GetFunctionInfo(functionIds);

                foreach (var ftm in typeMethodNames)
                {
                    lock (_syncObjFunctionNames)
                    {
                        if (!_functionNames.ContainsKey(ftm.FunctionID))
                        {
                            _functionNames.Add(ftm.FunctionID, new ClassMethodNames(ftm.TypeName, ftm.MethodName));
                        }
                    }
                }

            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }


        #endregion

        #region Updating the runnable counts on the tree

        private void UpdateRunnableCounts()
        {
            var nonRunnableLeafNodes = _configuration.ThreadProfilingIgnoreMethods;
            UpdateRunnableCounts(_threadProfilingBucket.Tree.Root, nonRunnableLeafNodes);
        }

        private static void UpdateRunnableCounts(ProfileNode node, IEnumerable<string> nonRunnableLeafNodes)
        {
            if (node == null)
                throw new ArgumentNullException("node");

            if (node.Children.Count == 0)
                UpdateRunnableCountsForLeafNode(node, nonRunnableLeafNodes);
            else
                UpdateRunnableCountsForNodeChildren(node, nonRunnableLeafNodes);
        }

        private static void UpdateRunnableCountsForLeafNode(ProfileNode node, IEnumerable<string> nonRunnableLeafNodes)
        {
            var combinedClassMethodName = node.Details.ClassName + ":" + node.Details.MethodName;

            if (!nonRunnableLeafNodes.Contains(combinedClassMethodName))
                return;

            node.NonRunnableCount = node.RunnableCount;
            node.RunnableCount = 0;
        }

        private static void UpdateRunnableCountsForNodeChildren(ProfileNode node, IEnumerable<string> nonRunnableLeafNodes)
        {
            if (node == null)
                throw new ArgumentNullException("node");

            nonRunnableLeafNodes = nonRunnableLeafNodes.ToList();

            foreach (var child in node.Children)
            {
                if (child == null)
                    continue;

                UpdateRunnableCounts(child, nonRunnableLeafNodes);

                node.RunnableCount -= child.NonRunnableCount;
                node.NonRunnableCount += child.NonRunnableCount;
            }
        }

        #endregion

        public void ResetCache()
        {

            _numberSamplesInSession = 0;
            _threadProfilingBucket.ClearTree();

            lock (_syncObjFunctionNames)
            {
                _functionNames.Clear();
            }

            _managedThreadsFromProfiler.Clear();
            PruningList.Clear();

            lock (_syncObjFailedProfiles)
            {
                _largeStackOverflows.Clear();
                _failedThreads.Clear();
                _failedThreadErrorCodes.Clear();
            }
        }

        private FidTypeMethodName[] GetFunctionInfo(UIntPtr[] functionIDs)
        {
            //get these once and not each iteration of the loop
            var typeOfFidTypeMethodName = typeof(FidTypeMethodName);
            var sizeOfFidTypeMethodName = Marshal.SizeOf(typeOfFidTypeMethodName);

            var result = _nativeMethods.RequestFunctionNames(functionIDs, functionIDs.Length, out IntPtr functionInfo);
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

        public bool IgnoreMinMinimumSamplingDuration
        {
            get
            {
                return _configuration.GetAgentCommandsCycle != _configuration.DefaultHarvestCycle;
            }
        }
    }
}
