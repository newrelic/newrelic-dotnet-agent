/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <string>
#include <memory>
#include "../Exceptions.h"

namespace sicily {
    namespace ast {
        class Type
        {
            public:
                enum class Kind {
                    kPRIMITIVE,
                    kARRAY,
                    kMETHOD,
                    kGENERICMETHOD,
                    kCLASS,
                    kGENERICCLASS,
                    kGENERICPARAM,
                    kFIELD,
                };

                Type(Kind kind);
                virtual ~Type();

                Kind GetKind() const;

                virtual xstring_t ToString() const = 0;

            private:
                Kind kind_;
        };

        typedef std::shared_ptr<Type> TypePtr;

        struct UnknownTypeKindException : AstException
        {
            UnknownTypeKindException(Type::Kind kind) : kind_(kind) {}
            Type::Kind kind_;
        };
    };
};
