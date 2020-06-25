/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "PrimitiveType.h"

namespace sicily {
    namespace ast {
        PrimitiveType::PrimitiveType(PrimitiveKind kind)
            : Type(Kind::kPRIMITIVE), primitiveKind_(kind)
        {
        }

        PrimitiveType::~PrimitiveType()
        {
        }

        PrimitiveType::PrimitiveKind
        PrimitiveType::GetPrimitiveKind() const
        {
            return primitiveKind_;
        }

        xstring_t
        PrimitiveType::ToString() const
        {
            switch (GetPrimitiveKind()) {
                case PrimitiveKind::kOBJECT: return _X("object");
                case PrimitiveKind::kSTRING: return _X("string");
                case PrimitiveKind::kCHAR: return _X("char");
                case PrimitiveKind::kVOID: return _X("void");
                case PrimitiveKind::kBOOL: return _X("bool");
                case PrimitiveKind::kI1: return _X("int8");
                case PrimitiveKind::kI2: return _X("int16");
                case PrimitiveKind::kI4: return _X("int32");
                case PrimitiveKind::kI8: return _X("int64");
                case PrimitiveKind::kR4: return _X("float32");
                case PrimitiveKind::kR8: return _X("float64");
                case PrimitiveKind::kU1: return _X("unsigned int8");
                case PrimitiveKind::kU2: return _X("unsigned int16");
                case PrimitiveKind::kU4: return _X("unsigned int32");
                case PrimitiveKind::kU8: return _X("unsigned int64");
                //case kNATIVE_INT: return _X("native unsigned int");
                //case kNATIVE_UNSIGNED_INT: return _X("native unsigned int");
                //case kNATIVE_FLOAT: return _X("native float");
                default: return _X("unknown");
            };
        }
    };
};

