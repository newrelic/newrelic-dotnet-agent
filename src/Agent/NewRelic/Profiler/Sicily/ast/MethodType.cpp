// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <sstream>
#include <cassert>

#include "MethodType.h"
#include "TypeList.h"

namespace sicily {
    namespace ast {
        MethodType::MethodType(
            ClassTypePtr targetType,
            const xstring_t& methodName,
            TypePtr returnType,
            bool instanceMethod,
            TypeListPtr argTypes,
            TypeListPtr genericTypes
        )
            : Type(Type::Kind::kMETHOD),
                targetType_(targetType), methodName_(methodName),
                returnType_(returnType), argTypes_(argTypes),
                genericTypes_(genericTypes), instanceMethod_(instanceMethod)
        {
            assert(targetType != nullptr);
            assert(!methodName.empty());
            assert(returnType != nullptr);
            assert(argTypes != nullptr);
            assert(genericTypes != nullptr);
        }

        MethodType::~MethodType()
        {
        }

        ClassTypePtr
        MethodType::GetTargetType() const
        {
            return targetType_;
        }

        xstring_t
        MethodType::GetMethodName() const
        {
            return methodName_;
        }

        TypePtr
        MethodType::GetReturnType() const
        {
            return returnType_;
        }

        TypeListPtr
        MethodType::GetArgTypes() const
        {
            return argTypes_;
        }

        TypeListPtr
        MethodType::GetGenericTypes() const
        {
            return genericTypes_;
        }

        bool
        MethodType::IsInstanceMethod() const
        {
            return instanceMethod_;
        }

        xstring_t
        MethodType::ToString() const
        {
            auto buf = xstring_t();

            if (instanceMethod_) {
                buf += _X("instance ");
            }

            buf += GetReturnType()->ToString() + _X(" ");
            buf += GetTargetType()->ToString() + _X("::");
            buf += GetMethodName();

            TypeListPtr genericTypes = GetGenericTypes();
            if (genericTypes->GetSize() > 0) {
                buf.push_back('<');
                buf += genericTypes->ToString();
                buf.push_back('>');
            }

            TypeListPtr argTypes = GetArgTypes();
            buf.push_back('(');
            if (argTypes->GetSize() > 0) {
                buf += argTypes->ToString();
            }
            buf.push_back(')');

            return buf;
        }
    };
};

