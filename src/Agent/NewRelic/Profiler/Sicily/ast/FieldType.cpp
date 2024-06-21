// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <sstream>
#include <cassert>

#include "FieldType.h"
#include "TypeList.h"

namespace sicily {
    namespace ast {
        FieldType::FieldType(
            ClassTypePtr targetType,
            const xstring_t& fieldName,
            TypePtr returnType,
            TypePtr requiredModifierType
        )
            : Type(Type::Kind::kFIELD),
            targetType_(targetType), fieldName_(fieldName),
            returnType_(returnType), requiredModifierType_(requiredModifierType)
        {
            assert(targetType != nullptr);
            assert(!fieldName.empty());
            assert(returnType != nullptr);
        }

        FieldType::~FieldType()
        {
        }

        ClassTypePtr
            FieldType::GetTargetType() const
        {
            return targetType_;
        }

        xstring_t
            FieldType::GetFieldName() const
        {
            return fieldName_;
        }

        TypePtr
            FieldType::GetReturnType() const
        {
            return returnType_;
        }

        TypePtr
            FieldType::GetRequiredModifierType() const
        {
            return requiredModifierType_;
        }

        xstring_t
            FieldType::ToString() const
        {
            auto buf = xstring_t();

            buf += GetReturnType()->ToString() + _X(" ");
            if (requiredModifierType_ != nullptr)
            {
                buf += _X("modreq(") + GetRequiredModifierType()->ToString() + _X(") ");
            }
            buf += GetTargetType()->ToString() + _X("::");
            buf += GetFieldName();

            return buf;
        }
    };
};

