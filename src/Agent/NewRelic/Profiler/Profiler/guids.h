// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once

#define MIDL_DEFINE_GUID(type,name,l,w1,w2,b1,b2,b3,b4,b5,b6,b7,b8) \
        const type name = {l,w1,w2,{b1,b2,b3,b4,b5,b6,b7,b8}}

// MIDL_INTERFACE("C5AC80A6-782E-4716-8044-39598C60CFBF")
// ICorProfilerInfo8 : public ICorProfilerInfo7
MIDL_DEFINE_GUID(IID, IID_ICorProfilerInfo8, 0xC5AC80A6, 0x782E, 0x4716, 0x80, 0x44, 0x39, 0x59, 0x8C, 0x60, 0xCF, 0xBF);