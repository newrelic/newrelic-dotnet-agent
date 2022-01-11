// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once

#include "ICorProfilerCallbackBase.h"

#ifdef PAL_STDCPP_COMPAT
#include "UnixSystemCalls.h"
#else
#include "SystemCalls.h"
#endif

namespace NewRelic { namespace Profiler {

    class CoreCLRCorProfilerCallbackImpl : public ICorProfilerCallbackBase {

    public:
        CoreCLRCorProfilerCallbackImpl()
            : ICorProfilerCallbackBase(
#ifdef PAL_STDCPP_COMPAT
                  std::make_shared<SystemCalls>()
#endif
              )
        {
            GetSingletonish() = this;
        }

        ~CoreCLRCorProfilerCallbackImpl()
        {
            if (GetSingletonish() == this)
                GetSingletonish() = nullptr;
        }
    };
}
}
