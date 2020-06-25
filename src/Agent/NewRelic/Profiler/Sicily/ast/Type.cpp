/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "Type.h"

namespace sicily {
    namespace ast {
        Type::Type(Kind kind)
            : kind_(kind)
        {
        }

        Type::~Type()
        {
        }

        Type::Kind
        Type::GetKind() const
        {
            return kind_;
        }
    };
};

