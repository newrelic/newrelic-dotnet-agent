using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Collections;
#pragma warning disable 649 // Unassigned fields. This should be removed when we support thread profiling in the NETSTANDARD2_0 build.

namespace NewRelic.Agent.Core.ThreadProfiling
{
	#region Delegates for PInvoke calls to unmanaged thread profiler
	/// <summary>
	/// Delegate for a callback with an unmanaged class and method name associated with <paramref name="functionId"/>.
	/// </summary>
	/// <param name="functionId">A function identifier.</param>
	/// <param name="className">The class name associated with <paramref name="functionId"/>.</param>
	/// <param name="methodName">The method name associated with <paramref name="functionId"/>.</param>
	public delegate void RequestFunctionNameCallback(IntPtr functionId, IntPtr className, IntPtr methodName);
	#endregion

	public class ThreadProfilingService : ConfigurationBasedService, IThreadProfilingSessionControl, IThreadProfilingProcessing
	{
		private const Int32 InvalidSessionId = 0;
		#region PInvoke Targets and Delegate Instances
		[NotNull]
		private readonly INativeMethods _nativeMethods;
		RequestFunctionNameCallback _callbackDelegateRequestFunctionNames;
		#endregion

		private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		[NotNull]
		private IAgent _agent;
		[NotNull]
		private readonly IDataTransportService _dataTransportService;
		[NotNull]
		private IScheduler _scheduler;

		private ThreadProfilingSampler _sampler;

		private Int32 _profileSessionId;
		private DateTime _startSessionTime;
		private DateTime _stopSessionTime;

		[NotNull]
		private readonly IDictionary<ulong, ClassMethodNames> _functionNames = new ConcurrentDictionary<ulong, ClassMethodNames>();
		[NotNull]
		private readonly Object _syncObjFunctionNames = new Object();

		private readonly Int32 _maxAggregatedNodes;

		[NotNull]
		private readonly ThreadProfilingBucket _threadProfilingBucket;

		// The pruning list maintains a reference to all TreeNodes created. 
		// After collecting the thread profiles, if the number of nodes
		// exceeds the _maxAggregatedNodes, the pruning list will be
		// sorted and pruned.
		public ArrayList PruningList { get; private set; }

		private Int32 TotalThreadCount { get { return _sampler != null ? _sampler.ManagedThreadsFromProfiler.Count : 0; } }

		// Sync object used to serialize access to the three thread lists. Don't expect access to occur
		// often enough to warrant three separate synchronization objects. Optimize later if necessary.
		[NotNull]
		private readonly Object _syncObjFailedProfiles = new Object();

		/// <summary>
		/// Count by thread Id of failed thread profiles received from unmanaged thread profiler.
		/// </summary>
		[NotNull]
		private readonly Dictionary<UIntPtr, UInt32> _failedThreads = new Dictionary<UIntPtr, UInt32>();
		[NotNull]
		private readonly Dictionary<UIntPtr, UInt32> _failedThreadErrorCodes = new Dictionary<UIntPtr, UInt32>();

		/// <summary>
		/// List of thread ids where the stack trace was large (greater than 2000)
		/// </summary>
		[NotNull]
		private readonly List<UIntPtr> _largeStackOverflows = new List<UIntPtr>();
		private IntPtr _requestFunctionNamesFunctionPointer = IntPtr.Zero;

		#region Construction and Initializations

		public ThreadProfilingService([NotNull] IAgent agent, [NotNull] IDataTransportService dataTransportService, [NotNull] IScheduler scheduler, [NotNull] INativeMethods nativeMethods, Int32 maxAggregatedNodes = 20000)
		{
			_agent = agent;
			_dataTransportService = dataTransportService;
			_scheduler = scheduler;
			_maxAggregatedNodes = maxAggregatedNodes;
			_nativeMethods = nativeMethods;

			_threadProfilingBucket = new ThreadProfilingBucket(this);
			PruningList = new ArrayList();
		}

		private void InitializeUnmanagedConnection()
		{
			try
			{
				_callbackDelegateRequestFunctionNames = RequestClassFunctionNameCallback;
				_requestFunctionNamesFunctionPointer = Marshal.GetFunctionPointerForDelegate(_callbackDelegateRequestFunctionNames);
			}
			catch (Exception e)
			{
				Log.ErrorFormat("Exception initializing unmanaged connection from Thread Profiling Service: {0}", e);
			}
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
		public void Start(IAgent agent)
		{
			_agent = agent;
			InitializeUnmanagedConnection();
		}

		/// <summary>
		/// Stops the <see cref="ThreadProfilingService"/> service. This will halt a 
		/// thread profiling session that might be running.
		/// </summary>
		public void Stop(IAgent agent)
		{
			_agent = agent;

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
		public Boolean StartThreadProfilingSession(Int32 profileSessionId, UInt32 frequencyInMsec, UInt32 durationInMsec)
		{
#if NET45
			Log.Info("Starting a thread profiling session");
			var startedNewSession = false;

			try
			{
				if (_sampler == null)
				{
					_sampler = new ThreadProfilingSampler(_agent, _scheduler, _nativeMethods);
				}

				// Remove existing data in tree and cache buffers
				ResetCache();

				startedNewSession = _sampler.Start(frequencyInMsec, durationInMsec);

				if (startedNewSession)
				{
					_startSessionTime = DateTime.UtcNow;
					_profileSessionId = profileSessionId;
				}
			}
			catch (Exception e)
			{
				Log.ErrorFormat("Failed to start thread profiler: {0}", e);
			}

			return startedNewSession;
#else
			Log.Warn("The thread profiler is not currently available for .NET Core");
			return false;
#endif
		}

		public Boolean StopThreadProfilingSession(Int32 profileId, Boolean reportData = true)
		{
			if (_sampler == null)
				return false;

			if (_profileSessionId != InvalidSessionId && _profileSessionId != profileId)
			{
				Log.WarnFormat("A request to stop a thread profiling session was made. Requesting profile Id = {0}. In process profile Id = {1}", profileId, _profileSessionId);
				return false;
			}

			_sampler.Stop(reportData);

			if (!reportData)
			{
				_profileSessionId = InvalidSessionId;
				ResetCache();
			}

			return true;
		}

		#endregion

		#region Failed Thread Profiles

		public void AddLargeStackOverflowProfile(UIntPtr threadId)
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

		public void AddFailedThreadProfile(UIntPtr threadId, uint errorCode)
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

		public void UpdateTree(StackInfo stackInfo, UInt32 depth)
		{
			_threadProfilingBucket.UpdateTree(stackInfo, depth);
		}

		public void AddNodeToPruningList([NotNull] ProfileNode node)
		{
			PruningList.Add(node);
		}

		public int GetTotalBucketNodeCount()
		{
			return _threadProfilingBucket.GetNodeCount();
		}
		#endregion

		#region Aggregation Process

		public void PerformAggregation()
		{
			try
			{
				var started = DateTime.UtcNow;

				Log.FinestFormat("Starting Aggregation process at {0}:{1}:{2}:{3}",
					started.Hour, started.Minute, started.Second, started.Millisecond);

				_stopSessionTime = DateTime.UtcNow;

				ResolveFunctionNames();
				UpdateRunnableCounts();
				SortPruningTree();
				_threadProfilingBucket.PruneTree();

				var profileData = SerializeData();
				
				_dataTransportService.SendThreadProfilingData(profileData);

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

		[NotNull]
		private IEnumerable<ThreadProfilingModel> SerializeData()
		{
			var samples = new Dictionary<String, Object>();
			if (_threadProfilingBucket.Tree.Root.Children.Count > 0)
				samples.Add("OTHER", _threadProfilingBucket.Tree.Root.Children);
			
			// Note: runnable thread count will always equal total thread count since we don't track the difference.
			var model = new ThreadProfilingModel(_profileSessionId, _startSessionTime, _stopSessionTime, _sampler.NumberSamplesInSession, samples, TotalThreadCount, TotalThreadCount);

			// We only ever have one set of data, but collector expects an array of data
			return new[] {model};
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
			HashSet<ulong> functionIds = _threadProfilingBucket.GetFunctionIds();

			// this calls the profiler.  It creates a thread to look up function ids and
			// joins on the thread so it should block until after it has called back with the
			// function info.
			PopulateFunctionNameCache(functionIds.ToArray());

			lock (_syncObjFunctionNames)
			{
				if (Log.IsFinestEnabled)
				{
					foreach (var id in functionIds)
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
		private void PopulateFunctionNameCache(ulong[] functionIds)
		{
			if (_requestFunctionNamesFunctionPointer == IntPtr.Zero)
			{
				Log.Error("The thread profiler RequestFunctionNames handle is uninitialized");
				return;
			}
			try
			{
				_nativeMethods.RequestFunctionNames(functionIds, _requestFunctionNamesFunctionPointer);
			}
			catch (Exception e)
			{
				Log.Error(e);
			}
		}

		private void RequestClassFunctionNameCallback(IntPtr functionId, IntPtr className, IntPtr methodName)
		{
			try
			{
				var names = MarshalNames(className, methodName);

				lock (_syncObjFunctionNames)
				{
					if (!_functionNames.ContainsKey((ulong)functionId.ToInt64()))
					{
						_functionNames.Add((ulong)functionId.ToInt64(), names);
					}
				}
			}
			catch (Exception e)
			{
				Log.Error(e);
			}
		}

		[NotNull]
		private static ClassMethodNames MarshalNames(IntPtr classNamePtr, IntPtr methodNamePtr)
		{
			var className = Marshal.PtrToStringUni(classNamePtr);
			if (String.IsNullOrEmpty(className))
				className = "No_class_name";

			var methodName = Marshal.PtrToStringUni(methodNamePtr);
			if (String.IsNullOrEmpty(methodName))
				methodName = "No_method_name";

			return new ClassMethodNames(className, methodName);
		}

		#endregion

		#region Updating the runnable counts on the tree

		private void UpdateRunnableCounts()
		{
			var nonRunnableLeafNodes = _configuration.ThreadProfilingIgnoreMethods;
			UpdateRunnableCounts(_threadProfilingBucket.Tree.Root, nonRunnableLeafNodes);
		}

		private static void UpdateRunnableCounts([NotNull] ProfileNode node, [NotNull] IEnumerable<String> nonRunnableLeafNodes)
		{
			if (node == null)
				throw new ArgumentNullException("node");

			if (node.Children.Count == 0)
				UpdateRunnableCountsForLeafNode(node, nonRunnableLeafNodes);
			else
				UpdateRunnableCountsForNodeChildren(node, nonRunnableLeafNodes);
		}

		private static void UpdateRunnableCountsForLeafNode([NotNull] ProfileNode node, [NotNull] IEnumerable<String> nonRunnableLeafNodes)
		{
			var combinedClassMethodName = node.Details.ClassName + ":" + node.Details.MethodName;

			if (!nonRunnableLeafNodes.Contains(combinedClassMethodName))
				return;

			node.NonRunnableCount = node.RunnableCount;
			node.RunnableCount = 0;
		}

		private static void UpdateRunnableCountsForNodeChildren([NotNull] ProfileNode node, [NotNull] IEnumerable<String> nonRunnableLeafNodes)
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
			_threadProfilingBucket.ClearTree();

			PruningList.Clear();

			_largeStackOverflows.Clear();
			_failedThreads.Clear();
			_failedThreadErrorCodes.Clear();

			if (_sampler != null)
			{
				_sampler.NumberSamplesInSession = 0;
			}
		}

	}

	public class ClassMethodNames
	{
		[NotNull]
		public readonly String Class;
		[NotNull]
		public readonly String Method;

		public ClassMethodNames([NotNull] String @class, [NotNull] String method)
		{
			Class = @class;
			Method = method;
		}
	}
}
