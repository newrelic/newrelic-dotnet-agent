// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <vector>
#include <stdint.h>

#define BYTEVECTOR(variableName, ...)\
    unsigned char myTempBytes##variableName[] = {##__VA_ARGS__};\
    std::vector<uint8_t> variableName(myTempBytes##variableName, myTempBytes##variableName + sizeof(myTempBytes##variableName) / sizeof(unsigned char));
