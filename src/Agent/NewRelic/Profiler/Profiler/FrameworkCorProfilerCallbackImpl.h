// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once

#include "ICorProfilerCallbackBase.h"

namespace NewRelic { namespace Profiler
{
    class FrameworkCorProfilerCallbackImpl : public ICorProfilerCallbackBase
    {

    private:
        std::shared_ptr<ModuleInjector::ModuleInjector> _moduleInjector;

    public:
        FrameworkCorProfilerCallbackImpl()
        {
            GetSingletonish() = this;
        }

        ~FrameworkCorProfilerCallbackImpl()
        {
            if (GetSingletonish() == this)
                GetSingletonish() = nullptr;
        }
    };
}}
