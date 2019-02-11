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

