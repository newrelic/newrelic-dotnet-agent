#pragma once

#ifndef PAL_STDCPP_COMPAT
#include <mutex>
#include <atomic>
#include <atlcomcli.h>

#include <cor.h>

#pragma warning(push)
#pragma warning(suppress: 26440)

#pragma warning(pop)

#include "namecache.h"
#include "..\Logging\Logger.h"

#endif

#include <corprof.h>

/*
GLOSSARY
StackFrame		A structure describing the FunctionID of the method, the mdTypeDef of the type the method is attached to and their associated names
StackWalk		A array of StackFrames (preallocated)
ThreadProfile	One is created for each managed thread during the RequestProfile call. It contains the managed thread id, any error code, a StackWalk
and an indicator of the last valid entry in the StackWalk.  It also serves as the context for the snapshot callback; as such, it contains
references to the name cache and profiling COM interface.
Profile			A collection of ThreadProfile(s) for all current managed threads.
ActiveThreadID  A collection of ThreadIDs for all current managed threads.

CAVEATS
Due to the requirement of not using dynamically allocated memory or taking any locks during the snapshot callback, the data structures are preallocated for use during profiling.
Data structures that use dynamically allocated memory can be read, but no operations may take place on them that might require a lock to be taken (_ITERATOR_DEBUG_LEVEL 2 as an example)
*/
namespace NewRelic { namespace Profiler { namespace ThreadProfiler
{
	class ThreadProfilerBase
	{
	public:
		virtual void Init(ICorProfilerInfo4* /*corProfilerInfo*/) noexcept
		{}

		virtual HRESULT RequestProfile(void** profile, int* length) noexcept
		{
			if (profile)
			{
				*profile = nullptr;
			}
			if (length)
			{
				*length = 0;
			}
			return E_NOTIMPL;
		}

		virtual void ReleaseProfile() noexcept
		{}

		virtual HRESULT GetTypeAndMethodNames(const UINT_PTR* /*functionIds*/, int /*length*/, void** results) noexcept
		{
			if (results)
			{
				*results = nullptr;
			}
			return E_NOTIMPL;
		}

		virtual void Shutdown() noexcept
		{}

		virtual HRESULT ThreadDestroyed(ThreadID /*threadId*/) noexcept
		{
			return E_NOTIMPL;
		}

		ThreadProfilerBase() noexcept = default;
		virtual ~ThreadProfilerBase() noexcept = default;
		ThreadProfilerBase(const ThreadProfilerBase&) = delete;
		ThreadProfilerBase(ThreadProfilerBase&&) = delete;
		ThreadProfilerBase& operator=(const ThreadProfilerBase&) = delete;
		ThreadProfilerBase& operator=(ThreadProfilerBase&&) = delete;
	};

#ifdef PAL_STDCPP_COMPAT
	class ThreadProfiler : public ThreadProfilerBase
	{
	public:
		ThreadProfiler() = default;

		~ThreadProfiler() override = default;

		ThreadProfiler(const ThreadProfiler&) = delete;
		ThreadProfiler(ThreadProfiler&&) = delete;
		ThreadProfiler& operator=(const ThreadProfiler&) = delete;
		ThreadProfiler& operator=(ThreadProfiler&&) = delete;
	};
#else

	using waitlock = std::unique_lock<std::mutex>;

	static void Signal(std::condition_variable& conditionvariable, std::atomic_bool& flag) noexcept
	{
		flag.store(true);
		conditionvariable.notify_one();
	}

	template <typename _MutexT>
	static void WaitForSignal(std::condition_variable& conditionvariable, std::atomic_bool& flag, std::atomic_bool& shutdown_flag, _MutexT& mutx)
	{
		waitlock l(mutx);
		conditionvariable.wait(l, [&]() noexcept {return flag.load() || shutdown_flag.load(); });
		flag.store(false);
	}

	class ThreadProfiler : public ThreadProfilerBase
	{
	public:
#pragma region Public Methods
		//ThreadProfiler construction and the Init() function do not do any heavy lifting.  The ThreadProfiler class is a member of the Profiler class and, as such, 
		//should not do things that allocate resource (threads, etc) during ctor() or Init() calls
		//   This called during Profiler Initialize.  Provides the interface to the EE.
		void Init(ICorProfilerInfo4* corProfilerInfo) noexcept override
		{
			// initialize the thread profiler
			LogTrace(L"Initializing ThreadProfiler");

			_corProfilerInfo = corProfilerInfo;
		}

		//Start the worker thread (if not already running; it will be terminated by calling Shutdown()).  Signal the worker thread that we are requesting profiling.
		// Wait for the profiling to be completed and return a pointer to the data in the marshal-ready data structure.
		// If _shuttingDown is true, abort waiting for the profiling to complete.
		//
		//It is considered and error if RequestProfile is called a second time without calling ReleaseProfile between 
		//the RequestProfile calls
		HRESULT RequestProfile(void** profile, int* length) noexcept override
		{
			if (nullptr == profile || nullptr == length)
			{
				return E_INVALIDARG;
			}

			if (!_marshaledProfiles.empty())
			{
				return E_ILLEGAL_METHOD_CALL;
			}

			if (!_corProfilerInfo)
			{
				LogDebug(L"TP: ", __func__, L" called without proper initialization. (corProfilerInfo)");
				return E_UNEXPECTED;
			}

			try
			{
				Start();

				SignalProfileRequested();

				WaitForProfileCompletedOrShutdown();

				if (HasShutdownBeenRequested())
				{
					*length = 0;
					*profile = nullptr;
					LogTrace(L"The thread profile was aborted.");

					return E_ABORT;
				}

				*length = static_cast<int>(_marshaledProfiles.size());
				*profile = _marshaledProfiles.data();

				if (0 == *length || nullptr == *profile)
				{
					LogDebug(L"RequestProfile failed	: The thread profile was empty (zero-sized/null data).");
					//in this case the profile error code will explain why.
				}
			}
			catch (const std::exception&)
			{
				return E_UNEXPECTED;
			}
			return S_OK;
		} //RequestProfile

		//Release any data cached by a prior call to RequestProfile
		void ReleaseProfile() noexcept override
		{
			_marshaledProfiles.clear();
		}

		//Get the type and method names for each of the provided FunctionIDs
		HRESULT GetTypeAndMethodNames(const UINT_PTR* functionIds, int length, void** results) noexcept override
		{
			if (nullptr == results || nullptr == functionIds || 0 == length)
			{
				return E_INVALIDARG;
			}

			try
			{
				ReleaseGetTypeAndMethodNamesResults();
				_marshaledFunctionIDTypeNameMethodNames.reserve(length);
				for (int idx=0; idx != length; ++idx)
				{
					const auto fid = functionIds[idx];
					const auto& typeAndMethodNames = _nameCache[fid];
					_marshaledFunctionIDTypeNameMethodNames.emplace_back(fid, typeAndMethodNames.TypeName(), typeAndMethodNames.MethodName());
				}
				*results = _marshaledFunctionIDTypeNameMethodNames.data();
			}
			catch (const std::bad_alloc&)
			{
				return E_OUTOFMEMORY;
			}
			catch (const std::exception&)
			{
				return E_UNEXPECTED;
			}
			return S_OK;
		}

		//terminate worker thread and free allocated resources.
		void Shutdown() noexcept override
		{
			try
			{
				SignalShutdown();

				if (_workerThread.joinable())
				{
					_workerThread.join();  //joinable is false upon return
					LogTrace(L"TP: profile thread shut down");
				}
				else
				{
					LogTrace(L"TP: ", __func__, L" called while thread is not running");
				}

				//clean up resources
				ReleaseProfile();
				ReleaseGetTypeAndMethodNamesResults();
				_nameCache.clear();
				_profileCompleted.store(false);
				_profileRequested.store(false);
				_shuttingDown.store(false);
			}
			catch (const std::exception&)
			{
			}
		}

		//called by the Profiler when a thread is on its way out...  We receive this notification because we called SetEventMask(COR_PRF_MONITOR_THREADS...) during Initialize.
		HRESULT ThreadDestroyed(ThreadID /*threadId*/) noexcept override
		{
			try
			{
				std::lock_guard<std::mutex> l(_mtx_snapshotInProgress);
			}
			catch (const std::exception& e)
			{
				LogWarn(L"Exception caught in ThreadDestroyed:", e.what());
			}
			return S_OK;
		}

		ThreadProfiler() = default;
		~ThreadProfiler() = default;
		ThreadProfiler(const ThreadProfiler&) = delete;
		ThreadProfiler(ThreadProfiler&&) = delete;
		ThreadProfiler& operator=(const ThreadProfiler&) = delete;
		ThreadProfiler& operator=(ThreadProfiler&&) = delete;
#pragma endregion 

	private:

#pragma region Constants

		//how many statically allocated stack frames do we support.  stack walking truncates results if more than this many 
		// stack frames are profiled
		static constexpr size_t MaxStackFramesSupported = 1337;

		//a guess at how many threads we will see.  This is used to preallocate containers that are one-per-thread.
		static constexpr size_t ThreadCountForReservation = 100;

#pragma endregion

#pragma region Types

		//Preallocated memory for all fields.
		struct StackFrame
		{
			FunctionID functionId{};
			mdTypeDef typeDef{};
			PreallocTypeName typeName{};
			PreallocMethodName methodName{};

			StackFrame() = default;
			StackFrame(const StackFrame&) = delete;
			StackFrame(StackFrame&&) = delete;
			StackFrame& operator=(const StackFrame&) = delete;
			StackFrame& operator=(StackFrame&&) = delete;
		};

		//avoid dynamic memory allocation, create a array for the StackFrames.
		using StackWalk = std::array<StackFrame, MaxStackFramesSupported>;

		//This structure is the unmarshaled version of a thread profile.  It also serves as the context value for the snapshot callback.
		struct ThreadProfile
		{
			ICorProfilerInfo4* _corProfilerInfo;
			NameCache& _nameCache;
			StackWalk& _stackwalk;
			StackWalk::iterator _frameNext{};
			HRESULT _errorCode{};
			ThreadID _managedTID;
			ThreadProfile(ThreadID managedTID, ICorProfilerInfo4* corProfilerInfo, NameCache& nameCache, StackWalk& stackwalk) :
				_managedTID(managedTID), _corProfilerInfo(corProfilerInfo), _nameCache(nameCache), _stackwalk(stackwalk), _frameNext(std::begin(_stackwalk))
			{}
			~ThreadProfile() = default;
			ThreadProfile(ThreadProfile&&) = default;

			ThreadProfile(const ThreadProfile&) = delete;
			ThreadProfile& operator=(const ThreadProfile&) = delete;
			ThreadProfile& operator=(ThreadProfile&&) = delete;
		};

#pragma region Marshaled Layouts
		//!!!MARSHALED LAYOUT!!!
		//This structure is marshaled by the managed code.  Do not change without updating the managed marshaling code.
		//for data returned from RequestProfile. Deleted in Shutdown
		struct MarshaledThreadProfile
		{
			ThreadID threadid{};
			HRESULT hresult{};
			int32_t length{};
			std::unique_ptr<uintptr_t[]> fids{};
			MarshaledThreadProfile(ThreadProfile& tp) : threadid(tp._managedTID), hresult(tp._errorCode), length(static_cast<int32_t>(std::distance(std::begin(tp._stackwalk), tp._frameNext)))
			{
				if (SUCCEEDED(hresult) && length)
				{
					fids = std::make_unique<uintptr_t[]>(length);
					if (fids)
					{
						auto write_itr = fids.get();
						//walk over the StackWalks and copy their FunctionID into the fids array
						for (int32_t idx = 0; idx != length; ++idx)
						{
							const auto& funcdetails = tp._stackwalk.data()[idx];
							*write_itr++ = funcdetails.functionId;
						}
					}
				}
			}
			~MarshaledThreadProfile() = default;
			MarshaledThreadProfile(MarshaledThreadProfile&& other) = default;

			MarshaledThreadProfile(MarshaledThreadProfile& other) = delete;
			MarshaledThreadProfile& operator=(const MarshaledThreadProfile& other) = delete;
			MarshaledThreadProfile& operator=(MarshaledThreadProfile&& other) = delete;
		};

		//!!!MARSHALED LAYOUT!!!
		//This structure is marshaled by the managed code.  Do not change without updating the managed marshaling code.
		//for data returned from GetTypeAndMethodNames, deleted in Shutdown
		struct alignas(intptr_t)MarshaledFunctiondDTypeNameMethodNameEntry
		{
			const FunctionID _fid;
			LPCWSTR _typeName;
			LPCWSTR _methodName;
			MarshaledFunctiondDTypeNameMethodNameEntry(FunctionID fid, LPCWSTR typeName, LPCWSTR methodName) noexcept :
			_fid(fid), _typeName(typeName), _methodName(methodName)
			{}

			MarshaledFunctiondDTypeNameMethodNameEntry() = delete;
			~MarshaledFunctiondDTypeNameMethodNameEntry() = default;
			MarshaledFunctiondDTypeNameMethodNameEntry(MarshaledFunctiondDTypeNameMethodNameEntry&& other) = default;

			MarshaledFunctiondDTypeNameMethodNameEntry(const MarshaledFunctiondDTypeNameMethodNameEntry&) = delete;
			MarshaledFunctiondDTypeNameMethodNameEntry& operator=(const MarshaledFunctiondDTypeNameMethodNameEntry&) = delete;
			MarshaledFunctiondDTypeNameMethodNameEntry& operator=(MarshaledFunctiondDTypeNameMethodNameEntry&&) = delete;
		};

		//!!!MARSHALED LAYOUT!!!
		//This type is marshaled by the managed code.  Do not change without updating the managed marshaling code.
		//This collection is returned by the call to GetTypeAndMethodNames
		using MarshaledFunctionIDTypeNameMethodNameCollection = std::vector<MarshaledFunctiondDTypeNameMethodNameEntry>;

		//!!!MARSHALED LAYOUT!!!
		//This type is marshaled by the managed code.  Do not change without updating the managed marshaling code.
		//This collection is returned by the call to RequestProfile
		using MarshaledProfileCollection = std::vector<MarshaledThreadProfile>;
#pragma endregion : data structures have been laid out to support marshaling by the managed code.

		//collection of all managed threads (from corProfilerInfo->EnumThreads)
		using ActiveThreadIDs = std::vector<ThreadID>;

		//collect of ThreadProfiles one is created for each managed thread 
		using ThreadProfiles = std::vector<ThreadProfile>;
#pragma endregion 

#pragma region Data
		//
		//Shutdown
		//
		//set during shutdown.  All conditions managed by condition variable check this bool for shutdown.
		std::atomic<bool> _shuttingDown{ false };

		//
		// Profiling Requested - manage signaling between RequestProfile and the worker thread when profiling is requested.
		//
		mutable std::mutex _mtx_ProfileRequested;
		std::condition_variable _cv_ProfileRequested;
		std::atomic_bool _profileRequested{};

		//
		//Profiling Complete - manage signaling between the worker thread and RequestProfile when the profiling is complete.
		//
		mutable std::mutex _mtx_ProfileCompleted;
		std::condition_variable _cv_ProfileCompleted;
		std::atomic_bool _profileCompleted{};

		//
		//Snapshot In Progress - manage signaling between the ThreadDestroyed callback and ProfileAllThreads to prevent threads from being 
		//destroyed during snapshot (prior to suspension)
		//
		mutable std::mutex _mtx_snapshotInProgress;

															//worker thread that performs profiling of all current, active managed threads
		std::thread _workerThread;

		//interface to CLR execution engine and metadata services.  Provided during the Initialize call to the profiler.
		CComPtr<ICorProfilerInfo4> _corProfilerInfo;

		//cache of type and method names.
		//NEVER update this cache during the snapshot callback as it's memory is dynamically allocated. 
		NameCache _nameCache;

		//collection of marshal-ready ThreadProfiles.  AKA a profile.  This is the result of a RequestProfile.
		MarshaledProfileCollection _marshaledProfiles;

		//collection of marshal-ready FunctionID, type names and method names. This is the result of the GetTypeAndMethodNames() call
		MarshaledFunctionIDTypeNameMethodNameCollection _marshaledFunctionIDTypeNameMethodNames;

#pragma endregion 

#pragma region Private Methods

		//Return a collection of all active managed threads.
		ActiveThreadIDs GetThreads() const
		{
			ActiveThreadIDs enumeratedThreads;
			CComPtr<ICorProfilerThreadEnum> threadEnum;
			if (SUCCEEDED(_corProfilerInfo->EnumThreads(&threadEnum)))
			{
				const int ThreadEnumBatchSize = 40;
				std::array<ThreadID, ThreadEnumBatchSize> threadIDBatch;
				const auto batchBegin = threadIDBatch.data();
				ULONG celtFetched{};
				HRESULT hr{};
				if (SUCCEEDED(threadEnum->GetCount(&celtFetched)))
				{
					enumeratedThreads.reserve(celtFetched);
				}
				celtFetched = 0;
				while (SUCCEEDED(hr = threadEnum->Next(ThreadEnumBatchSize, batchBegin, &celtFetched)))
				{
					for (ULONG idx = 0; idx != celtFetched; ++idx)
					{
						enumeratedThreads.push_back(batchBegin[idx]);
					}

					if (S_FALSE == hr)
					{
						break;
					}
				}
				if (FAILED(hr))
				{
					LogError(L"TP: ", __func__, L": thread enum Next() failed");
				}
			}
			else
			{
				LogError(L"TP: ", __func__, L": Could not get thread enumerator");
			}
			return enumeratedThreads;
		}

		//start the worker thread
		void Start()
		{
			if (!_workerThread.joinable())
			{
				LogTrace(L"TP: starting profile thread");

				_workerThread = std::thread(&ThreadProfiler::ProfilerThreadStart, this);
				std::this_thread::yield();
			}
		}

		//Test if _shuttingDown is true (and log if it is) returning the state of the flag.
		bool HasShutdownBeenRequested() const noexcept
		{
			// shutdown if it is time to die
			const auto shutdownRequested = _shuttingDown.load();
			if (shutdownRequested) {
				LogInfo(L"TP: Shutting down thread profiler");
			}
			return shutdownRequested;
		}

		//notify the profiler thread that it should begin profiling
		void SignalProfileRequested() noexcept
		{
			Signal(_cv_ProfileRequested, _profileRequested);
		}

		//notify the thread waiting in RequestProfile that the profiling is complete
		void SignalProfileCompleted() noexcept
		{
			Signal(_cv_ProfileCompleted, _profileCompleted);
		}

		//set _shuttingDown to true and signal all threads to check for shutdown
		void SignalShutdown() noexcept
		{
			// initiate shutdown of the background worker thread
			_shuttingDown.store(true);

			//notify the background worker thread so that it can pick up the shutdown
			_cv_ProfileRequested.notify_one();

			//notify any waiting thread in RequestProfile that we are shutting down.
			_cv_ProfileCompleted.notify_all();
		}

		//wait for the worker thread to signal that profiling is complete
		void WaitForProfileCompletedOrShutdown()
		{
			WaitForSignal(_cv_ProfileCompleted, _profileCompleted, _shuttingDown, _mtx_ProfileCompleted);
		}

		//wait until event is fired that indicates we should start profiling
		void WaitForProfileRequestedOrShutdown()
		{
			WaitForSignal(_cv_ProfileRequested, _profileRequested, _shuttingDown, _mtx_ProfileRequested);
		}

		//Release results from a prior call to GetTypeAndMethodNames()
		void ReleaseGetTypeAndMethodNamesResults() noexcept
		{
			_marshaledFunctionIDTypeNameMethodNames.clear();
		}

		//Get the list of active managed threads (GetThreads) and call _corProfilerInfo->DoStackSnapshot for each one. Capture the StackWalk 
		//  (function id, type and method names; observing the name cache for previously captured names) in a preallocated data structure. 
		//  After a thread's StackWalk has been captured, copy this data into the name cache and copy the data into the marshal-ready collection.
		void ProfileAllThreads()
		{
			ThreadProfiles profiles;
			profiles.reserve(ThreadCountForReservation);
			_marshaledProfiles.reserve(ThreadCountForReservation);

			auto stackwalk = std::make_unique<StackWalk>();

			std::lock_guard<std::mutex> l(_mtx_snapshotInProgress);

			const auto localActiveThreads = GetThreads();
			for (const auto threadId : localActiveThreads)
			{
				if (HasShutdownBeenRequested()) {
					break;
				}

				try
				{
					// get or create the thread profile for this thread
					profiles.emplace_back(threadId, _corProfilerInfo, _nameCache, *stackwalk);
					auto& threadProfile = profiles.back();

					// LEGACY: on 64-bit architecture prefer native stack walking, see: StackWalk64

					// If context is NULL, the stack walk will begin at the last available managed frame for the target thread.
					const auto result = _corProfilerInfo->DoStackSnapshot(threadId, StaticStackFrameCallback,
						COR_PRF_SNAPSHOT_INFO::COR_PRF_SNAPSHOT_DEFAULT, &threadProfile, nullptr, 0);

					//if DoStackSnapshot failed, we won't have a stackwalk.  this can happen if a managed thread does not currently 
					//have any managed code frames on the stack. (A thread pool thread has returned to the waiting-for-work native code)
					if (FAILED(result))
					{
						threadProfile._errorCode = result;

						//if the thread terminates between Enum and snapshot we may get CORPROF_E_STACKSNAPSHOT_INVALID_TGT_THREAD
						continue;
					}

					std::for_each(std::begin(threadProfile._stackwalk), threadProfile._frameNext, [&](const StackFrame& funcdetails)
					{
						if (funcdetails.functionId && funcdetails.typeDef != 0)
						{
							_nameCache.insert(funcdetails.functionId, funcdetails.typeDef, funcdetails.typeName, funcdetails.methodName);
						}
					});

					//transform the threadProfile into a snapshot to pass back to caller of RequestProfile
					_marshaledProfiles.emplace_back(threadProfile);

					// LEGACY: check the result for certain failures and fall back on native stack walking to find the first managed function call and then try again
				}
				catch (...)
				{
					// the show must go on! if we fail to profile one thread, continue trying to profile the others
					LogTrace(L"TP: exception in ", __func__);
				}
			}
		}

		//worker thread method.  Initialize the thread for calling the Execution Engine.  Wait for RequestProfile to signal
		// a profiling request.  When requested call ProfileAllThreads to capture the profile and signal the blocked thread in 
		// RequestProfile that profiling is complete.  Terminate when _shuttingDown is true.
		void ProfilerThreadStart()
		{
			LogTrace(L"TP: profile thread started");

			// This function needs to be called on any thread before making any ICorProfilerInfo* calls and must be 
			// made before any thread is suspended by this profiler.
			// As you might have already figured out, this is done to avoid deadlocks situation when 
			// the suspended thread holds on the loader lock / heap lock while the current thread is trying to obtain
			// the same lock.
			HRESULT hr = _corProfilerInfo->InitializeCurrentThread();
			if (FAILED(hr))
			{
				LogError(L"TP: InitializeCurrentThread failed: ", std::hex, std::showbase, hr,
					std::resetiosflags(std::ios_base::basefield | std::ios_base::showbase));
			}

			for (;;)
			{
				try
				{
					WaitForProfileRequestedOrShutdown();

					if (HasShutdownBeenRequested())
					{
						break;
					}

					ProfileAllThreads();

					SignalProfileCompleted();
				}
				catch (...)
				{
					LogError("TP: Exception thrown while profiling.");
					// an exception here is recoverable, "The thread must go on!"
				}
			}
			LogTrace(L"TP: profile thread terminating");
		}

		static HRESULT __stdcall StaticStackFrameCallback(uintptr_t functionId, uintptr_t instructionPointer, uintptr_t frameInfo, uint32_t contextSize, uint8_t context[], void * clientData)
		{
			instructionPointer; frameInfo; contextSize; context;
			try
			{
				const HRESULT StackTooDeep = S_FALSE; // HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER);

				ThreadProfile& threadProfile = *static_cast<ThreadProfile*>(clientData);
				// for now we just clear the whole thing and start counting again if we go 
				// over since we *must* get the root of the stack but we can afford to lose leaves

				if (threadProfile._frameNext == std::end(threadProfile._stackwalk))
				{
					threadProfile._frameNext = std::begin(threadProfile._stackwalk);
					//this will overwrite a previous error code.
					threadProfile._errorCode = StackTooDeep;
				}

				auto& thisframe = *threadProfile._frameNext;
				thisframe.functionId = functionId;
				const auto& nameCache = threadProfile._nameCache;

				if (functionId && !nameCache.has_fid(functionId))
				{
					// get the interfaces we need and the metadata token
					CComPtr<IMetaDataImport2> metaDataImport;
					mdToken mdTokenForFunction{};
					HRESULT hr{};
					if (SUCCEEDED(hr = threadProfile._corProfilerInfo->GetTokenAndMetaDataFromFunction(functionId, IID_IMetaDataImport2, (IUnknown**)&metaDataImport, &mdTokenForFunction)) &&
						metaDataImport != nullptr)
					{
						auto& preallocMethodName = thisframe.methodName;
						//first is buffer, second is actual name length
						if (SUCCEEDED(hr = metaDataImport->GetMethodProps(mdTokenForFunction, &thisframe.typeDef,
							&preallocMethodName.first.front(), (ULONG)preallocMethodName.first.size(), &preallocMethodName.second,
							nullptr, nullptr, nullptr, nullptr, nullptr)))
						{
							auto& preallocTypeName = thisframe.typeName;
							auto& typeName = nameCache.typename_for(thisframe.typeDef);
							if (typeName == TypeAndMethodNames::GetUnknownTypeName())
							{
								// get the name of the class from the cache. Make a cache entry if not found.
								hr = metaDataImport->GetTypeDefProps(thisframe.typeDef, &preallocTypeName.first.front(), static_cast<ULONG>(preallocTypeName.first.size()), &preallocTypeName.second, nullptr, nullptr);
							}
							else
							{
								wcscpy_s(preallocTypeName.first.data(), static_cast<ULONG>(preallocTypeName.first.size()), typeName->c_str());
							}
						}
					}
					//don't overwrite StackTooDeep.  
					if (FAILED(hr) && StackTooDeep != threadProfile._errorCode)
					{
						threadProfile._errorCode = hr;
					}
				}

				//advance the index to the next slot
				++threadProfile._frameNext;
			}
			catch (...)
			{
				//do not log here because of deadlock (the suspend thread issue)
			}
			return S_OK;
		}
#pragma endregion
	};
#endif
}}}
