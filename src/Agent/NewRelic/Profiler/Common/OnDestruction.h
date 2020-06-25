/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <functional>

namespace NewRelic { namespace Profiler
{
    struct OnDestruction
    {
        std::function<void()> _onDestroyed;

        OnDestruction(std::function<void()> onDestroyed)
            : _onDestroyed(onDestroyed)
        { }

        ~OnDestruction()
        {
            _onDestroyed();
        }
    };
}}
