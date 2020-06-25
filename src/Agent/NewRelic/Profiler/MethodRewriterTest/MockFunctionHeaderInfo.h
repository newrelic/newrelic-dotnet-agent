/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include "../MethodRewriter/IFunctionHeaderInfo.h"
#include "../Common/Macros.h"

namespace NewRelic {
    namespace Profiler {
        namespace MethodRewriter {
            namespace Test {
                class MockFunctionHeaderInfo : public IFunctionHeaderInfo
                {
                public:
                    MockFunctionHeaderInfo(uint16_t returnCount) : _returnCount(returnCount)
                    {
                    }

                    virtual uint16_t GetReturnCount() override {
                        return _returnCount;
                    }

                    virtual unsigned GetMethodBodySize() override {
                        return 0;
                    }

                    virtual uint8_t* GetCode() override {
                        return nullptr;
                    }

                    virtual bool IsTinyHeader() override {
                        return true;
                    }

                    virtual unsigned GetTotalSize() override {
                        return 0;
                    }

                    virtual unsigned GetHeaderSize() override {
                        return 0;
                    }

                    virtual unsigned GetMaxStack() override {
                        return 2;
                    }

                    virtual bool HasSEH() override {
                        return false;
                    }

                    // Return the number of return instructions.
                    virtual InstructionTypeCounts GetInstructionTypeCounts() override {
                        return InstructionTypeCounts();
                    }
                private:
                    uint16_t _returnCount;

                };
            }
        }
    }
}