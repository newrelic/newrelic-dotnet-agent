// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <sstream>

#include "GenericParamType.h"

namespace sicily
{
    namespace ast
    {
        GenericParamType::GenericParamType(GenericParamKind kind, uint32_t number) :
            Type(Type::Kind::kGENERICPARAM),
            kind_(kind),
            number_(number)
        { }

        xstring_t GenericParamType::ToString() const
        {
            auto stream = xstring_t();
            stream.push_back('!');
            if (kind_ == GenericParamKind::kMETHOD) stream.push_back('!');
            stream += to_xstring(number_);

            return stream;
        }

        GenericParamType::GenericParamKind GenericParamType::GetGenericParamKind()
        {
            return kind_;
        }

        uint32_t GenericParamType::GetNumber()
        {
            return number_;
        }
    }
}
