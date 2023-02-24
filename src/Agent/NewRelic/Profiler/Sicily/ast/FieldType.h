/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once

#include <memory>
#include <string>

#include "Types.h"

namespace sicily {
    namespace ast {
        class FieldType : public Type
        {
        public:
            FieldType(
                ClassTypePtr targetType,
                const xstring_t& fieldName,
                TypePtr returnType
            );
            virtual ~FieldType();

            ClassTypePtr GetTargetType() const;
            xstring_t GetFieldName() const;
            TypePtr GetReturnType() const;

            xstring_t ToString() const;

        private:
            ClassTypePtr targetType_;
            xstring_t fieldName_;
            TypePtr returnType_;
        };

        typedef std::shared_ptr<FieldType> FieldTypePtr;
    };
};

