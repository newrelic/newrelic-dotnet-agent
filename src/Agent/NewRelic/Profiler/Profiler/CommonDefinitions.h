// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <vector>
#include <memory>
#include <stdint.h>

namespace NewRelic { namespace Profiler
{
    typedef std::vector<uint8_t> ByteVector;
    typedef std::shared_ptr<ByteVector> ByteVectorPtr;
}}
