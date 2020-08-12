// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <sstream>

#include "TypeList.h"

namespace sicily {
    namespace ast {
        TypeList::TypeList()
        {
        }

        TypeList::~TypeList()
        {
        }

        void
        TypeList::Add(TypePtr type)
        {
            items_.push_back(type);
        }

        uint16_t
        TypeList::GetSize() const
        {
            if (items_.size() > UINT16_MAX) return UINT16_MAX;

            return uint16_t(items_.size());
        }

        TypePtr
        TypeList::GetItem(uint16_t i) const
        {
            return items_[i];
        }

        xstring_t
        TypeList::ToString() const
        {
            auto buf = xstring_t();
            for (size_t i = 0; i < items_.size(); i++) {
                buf += items_[i]->ToString();

                if (i < (items_.size()-1)) {
                    buf += _X(", ");
                }
            }
            return buf;
        }
    };
};

