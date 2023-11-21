/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include "Type.h"

namespace sicily {
    namespace ast {
        class PrimitiveType : public Type
        {
            public:
                // See ECMA-335 II.23.1.16
                enum class PrimitiveKind {
                    kVOID = 0x01,
                    kBOOL = 0x02,
                    kCHAR = 0x03,
                    kI1 = 0x04,
                    kU1 = 0x05,
                    kI2 = 0x06,
                    kU2 = 0x07,
                    kI4 = 0x08,
                    kU4 = 0x09,
                    kI8 = 0x0a,
                    kU8 = 0x0b,
                    kR4 = 0x0c,
                    kR8 = 0x0d,
                    kSTRING = 0x0e,
                    kINTPTR = 0x18,
                    kUINTPTR = 0x19,
                    kOBJECT = 0x1C,
                };
                    
                PrimitiveType(PrimitiveKind kind, bool byRef);
                virtual ~PrimitiveType();

                PrimitiveKind GetPrimitiveKind() const;
                bool GetByRef() const;

                xstring_t ToString() const;

            private:
                PrimitiveKind primitiveKind_;
                bool byRef_;

                xstring_t GetKindString() const;
        };

        typedef std::shared_ptr<PrimitiveType> PrimitiveTypePtr;
    };
};
