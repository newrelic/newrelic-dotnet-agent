/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include "../Logging/Logger.h"
#include "stdafx.h"
#include <map>
#include <cor.h>
#include <corprof.h>
#include "../Common/Macros.h"

namespace NewRelic { namespace Profiler {
    struct ModuleAndMethodID {
        ModuleID moduleID;
        mdMethodDef methodID;
        uint64_t compoundID;

        ModuleAndMethodID(ModuleID moduleID, mdMethodDef methodID)
        {
            this->moduleID = moduleID;
            this->methodID = methodID;

            auto longModuleId = uint64_t(moduleID);
            // yeah, this cuts off the top 32 bits of the module id
            compoundID = longModuleId << 32;
            compoundID += methodID;
        }

        bool operator==(const ModuleAndMethodID& other) const
        {
            return moduleID == other.moduleID && methodID == other.methodID;
        }

        bool operator<(const ModuleAndMethodID& other) const
        {
            return compoundID < other.compoundID;
        }
    };

    // The debugger gets really unhappy when we do things the *right way* and add instrumentation 
    // in the GetReJITParameters callback as Broman says we should:
    // https://blogs.msdn.microsoft.com/davbr/2011/10/12/rejit-a-how-to-guide/
    // To work around this, we track the original method bytes ourselves.   We only care about
    // the original bytecode for methods we're instrumenting, not the helper/api method stuff.
    class ILTracker
    {
    private:
        CComPtr<ICorProfilerInfo4> _profilerInfo;
        std::map<ModuleAndMethodID, ByteVectorPtr> _methodToIL;
        std::mutex _lock;

        ByteVectorPtr GetILFromMap(ModuleAndMethodID id)
        {
            std::lock_guard<std::mutex> lock(_lock);
            auto bytes = _methodToIL.find(id);
            if (bytes == _methodToIL.end())
            {
                return nullptr;
            }
            return bytes->second;
        }
    public:
        ILTracker(CComPtr<ICorProfilerInfo4> profilerInfo) : _profilerInfo(profilerInfo)
        { }

        ByteVectorPtr STDMETHODCALLTYPE GetILFunctionBody(
            bool useCache,
            ModuleID moduleId,
            mdMethodDef methodId) 
        {
            auto id = ModuleAndMethodID(moduleId, methodId);
            if (useCache)
            {
                auto bytes = GetILFromMap(id);
                if (bytes != nullptr)
                {
                    return bytes;
                }
            }
            LPCBYTE method;
            ULONG size;
            if (SUCCEEDED(_profilerInfo->GetILFunctionBody(moduleId, methodId, &method, &size)))
            {
                ByteVectorPtr bytes = std::make_shared<ByteVector>();
                bytes->assign(method, method + size);

                if (useCache)
                {
                    std::lock_guard<std::mutex> lock(_lock);
                    _methodToIL[id] = bytes;
                }

                return bytes;
            }
            else {
                return nullptr;
            }
        }

        void Remove(
            ModuleID moduleId,
            mdMethodDef methodId)
        {
            auto id = ModuleAndMethodID(moduleId, methodId);

            std::lock_guard<std::mutex> lock(_lock);
            _methodToIL.erase(id);
        }
    };
}}