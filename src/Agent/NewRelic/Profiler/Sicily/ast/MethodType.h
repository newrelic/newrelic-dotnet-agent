// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once

#include <memory>
#include <string>

#include "Types.h"

namespace sicily {
    namespace ast {
        class MethodType : public Type
        {
            public:
                MethodType(
                    ClassTypePtr targetType,
                    const xstring_t& methodName,
                    TypePtr returnType,
                    bool instanceMethod,
                    TypeListPtr argTypes = TypeListPtr(new TypeList()),
                    TypeListPtr genericTypes = TypeListPtr(new TypeList())
                );
                virtual ~MethodType();

                ClassTypePtr GetTargetType() const;
                xstring_t GetMethodName() const;
                TypePtr GetReturnType() const;
                TypeListPtr GetArgTypes() const;
                TypeListPtr GetGenericTypes() const;
                bool IsInstanceMethod() const;

                xstring_t ToString() const;

            private:
                ClassTypePtr targetType_;
                xstring_t methodName_;
                TypePtr returnType_;
                TypeListPtr argTypes_;
                TypeListPtr genericTypes_;
                bool instanceMethod_;
        };

        typedef std::shared_ptr<MethodType> MethodTypePtr;
    };
};
