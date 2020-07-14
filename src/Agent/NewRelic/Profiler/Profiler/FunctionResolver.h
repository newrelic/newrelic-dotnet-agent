/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once

#include <map>
#include "../Logging/Logger.h"
#include "Function.h"
#include <cor.h>
#include <corprof.h>

namespace NewRelic { namespace Profiler
{

    struct ModuleAndMethodID {
        ModuleID moduleID;
        mdMethodDef methodID;

        ModuleAndMethodID(ModuleID moduleID, mdMethodDef methodID)
        {
            this->moduleID = moduleID;
            this->methodID = methodID;
        }

        bool operator==(const ModuleAndMethodID& other) const
        {
            return moduleID == other.moduleID && methodID == other.methodID;
        }

        bool operator<(const ModuleAndMethodID& other) const
        {
            return moduleID < other.moduleID || methodID < other.methodID;
        }
    };

    const FunctionID INVALID_FUNCTION_ID = 0;

    // Most of our JIT logic works off of FunctionIDs, but we see method reJITs through the GetReJITParameters
    // callback which does not give us the id.  Instead it gives us the module and method ids which can map to one or
    // more FunctionIDs in the case of generic functions.  This resolver class helps us track the mapping between
    // FunctionIDs and module/method ids for those generic functions.
    //
    // If instrumentation is added to a generic method after the agent starts, we find the module id and method id of 
    // the method and ask for a reJIT.  In this scenario the GetReJITParameters method is called but this class does not
    // yet have the function id.  GetReJITParameters will finish without adding instrumentation, then ReJITCompilationStarted
    // will be called with the function id.  That method notifies this class by calling RequestGenericMethodReJIT.
    // We look up the module/method id and check if it's been queued to be rejitted.  If it is, we add the function id 
    // info to our map and request another reJIT.  When that triggers, GetReJITParameters will be called once again 
    // and we will have the function id.
    // 
    // Note that the bytecode for a given generic class / method *should* be the same regardless of the specific generic
    // type as noted by Broman (https://blogs.msdn.microsoft.com/davbr/2011/10/12/rejit-a-how-to-guide/).  For this reason
    // we only map a single function id to a module/method.  This is okay because when we look up the class and method details
    // for a function id we use the apis that don't return the type specifics of the generic method.  That means that while 
    // multiple function ids may map to a single generic method, the AgentShim will see invocations for all of those function ids 
    // use a single function id.  For our purposes, at least for the current agent functionality we support, this is fine.
    class FunctionResolver
    {
    private:
        // We fall back on this map for generic methods when our calls to GetFunctionFromToken fail.
        std::map<ModuleAndMethodID, FunctionID> _functionToMethod;
        std::set<ModuleAndMethodID> _methodsToReJIT;

        std::mutex _functionToMethodMutex;
        std::mutex _methodsToReJITMutex;
        CComPtr<ICorProfilerInfo4> _corProfilerInfo;

        bool ShouldReJIT(FunctionID functionId, ModuleID moduleId, mdMethodDef methodId)
        {
            ModuleAndMethodID moduleAndMethodID(moduleId, methodId);

            auto rejit = ShouldReJIT(moduleAndMethodID);
            if (rejit)
            {
                AddUnderLock(functionId, moduleAndMethodID);
            }
            
            return rejit;
        }

        bool ShouldReJIT(ModuleAndMethodID moduleAndMethodID)
        {
            std::lock_guard<std::mutex> rejitMethodsLock(_methodsToReJITMutex);
            auto it = _methodsToReJIT.find(moduleAndMethodID);
            if (it == _methodsToReJIT.end())
            {
                return false;
            }

            _methodsToReJIT.erase(it);
            return true;
        }

        void AddUnderLock(FunctionID functionId, ModuleAndMethodID moduleAndMethodID)
        {
            std::lock_guard<std::mutex> lock(_functionToMethodMutex);
            _functionToMethod[moduleAndMethodID] = functionId;
        }

        FunctionID GetGenericFunctionIdUnderLock(ModuleAndMethodID moduleAndMethodID)
        {
            std::lock_guard<std::mutex> lock(_functionToMethodMutex);

            auto it = _functionToMethod.find(moduleAndMethodID);
            if (it != _functionToMethod.end())
            {
                auto id = it->second;

                // cool.  we found the id, now remove it from our map.
                _functionToMethod.erase(it);
                return id;
            }
            return INVALID_FUNCTION_ID;
        }

        void AddToReJITSetUnderLock(ModuleAndMethodID moduleAndMethodID)
        {
            std::lock_guard<std::mutex> rejitMethodsLock(_methodsToReJITMutex);
            _methodsToReJIT.emplace(moduleAndMethodID);
        }

    public:
        FunctionResolver(CComPtr<ICorProfilerInfo4> corProfilerInfo)
        {
            _corProfilerInfo = corProfilerInfo;
        }

        // This adds the functionId->moduleId/methodId to a map if a call to GetFunctionFromToken fails.
        void AddFunctionIfGeneric(Function& function)
        {
            FunctionID functionId;
            if (_corProfilerInfo->GetFunctionFromToken(function.GetModuleID(), function.GetMethodToken(), &functionId) == CORPROF_E_FUNCTION_IS_PARAMETERIZED)
            {
                ModuleAndMethodID moduleAndMethodID(function.GetModuleID(), function.GetMethodToken());

                AddUnderLock(function.GetFunctionId(), moduleAndMethodID);
            }
        }

        // Returns a FunctionID for the given moduleId and methodId, or 0 if it is not found.
        FunctionID GetFunctionId(ModuleID moduleId, mdMethodDef methodId)
        {
            FunctionID functionId = INVALID_FUNCTION_ID;
            HRESULT hr = _corProfilerInfo->GetFunctionFromToken(moduleId, methodId, &functionId);
            if (SUCCEEDED(hr))
            {
                return functionId;
            }
            else if (hr == CORPROF_E_FUNCTION_IS_PARAMETERIZED)
            {
                LogTrace(L"Generic function lookup falling back to map");
                return GetGenericFunctionId(moduleId, methodId);
            }
            else {
                LogError(L"GetFunctionFromToken call failed: ", to_hex_string(hr, 0, true));
                return INVALID_FUNCTION_ID;
            }
        }

        bool IsValid(FunctionID functionId)
        {
            return functionId != INVALID_FUNCTION_ID;
        }

        // If this function is marked for reJIT, store the module/method id to function id relationship
        // and request a reJIT.
        void RequestGenericMethodReJIT(FunctionID functionId)
        {
            ModuleID moduleId;
            mdToken methodId;

            if (_corProfilerInfo->GetFunctionInfo(functionId, nullptr, &moduleId, &methodId) != S_OK)
            {
                LogTrace(L"GetFunctionInfo call failed in ", __func__);
                return;
            }

            if (ShouldReJIT(functionId, moduleId, methodId))
            {
                LogDebug(L"Requesting a reJIT of a generic function");
                _corProfilerInfo->RequestReJIT(1, &moduleId, &methodId);
            }
        }

        FunctionID GetGenericFunctionId(ModuleID moduleId, mdMethodDef methodId)
        {
            ModuleAndMethodID moduleAndMethodID(moduleId, methodId);

            auto id = GetGenericFunctionIdUnderLock(moduleAndMethodID);
            if (!IsValid(id))
            {
                LogTrace(L"Generic function lookup failed, queued a reJIT");
                AddToReJITSetUnderLock(moduleAndMethodID);
            }

            return id;
        }
    };
}}
