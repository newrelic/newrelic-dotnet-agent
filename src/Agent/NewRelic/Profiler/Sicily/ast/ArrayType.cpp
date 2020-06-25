/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "ArrayType.h"
#include <cassert>

namespace sicily {
    namespace ast {
        ArrayType::ArrayType(TypePtr elementType)
            : Type(Type::Kind::kARRAY), elementType_(elementType)
        {
            assert(elementType != nullptr);
        }

        ArrayType::~ArrayType()
        {
        }

        TypePtr
        ArrayType::GetElementType() const
        {
            return elementType_;
        }

        xstring_t
        ArrayType::ToString() const
        {
            return elementType_->ToString() + _X("[]");
        }
    };
};

