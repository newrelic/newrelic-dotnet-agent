#pragma once
#include "../Common/Macros.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter
{
    // A structure to hold the count of various interesting opcode types.
    struct InstructionTypeCounts {
        unsigned returnCount;
        unsigned shortBranchCount;
        unsigned longBranchCount;
        unsigned switchCount;
        InstructionTypeCounts() {
            returnCount = 0;
            shortBranchCount = 0;
            longBranchCount = 0;
            switchCount = 0;
        }
    };

    class IFunctionHeaderInfo
    {
    public:
        // returns the number of CEE_RET instructions in the function.
        virtual uint16_t GetReturnCount() = 0;

        // Returns the size of the method body.
        virtual unsigned GetMethodBodySize() = 0;

        // Returns a pointer to the method body;
        virtual uint8_t* GetCode() = 0;

        // Returns true if this is a tiny header.
        virtual bool IsTinyHeader() = 0;

        virtual unsigned GetTotalSize() = 0;

        // Returns the size of the header.  The method body begins after the header.
        virtual unsigned GetHeaderSize() = 0;

        virtual unsigned GetMaxStack() = 0;

        // Returns true if this header contains a exception handling block.
        virtual bool HasSEH() = 0;

        // Return the number of return instructions.
        virtual InstructionTypeCounts GetInstructionTypeCounts() = 0;
    };


    typedef std::shared_ptr<IFunctionHeaderInfo> FunctionHeaderInfoPtr;

}}}