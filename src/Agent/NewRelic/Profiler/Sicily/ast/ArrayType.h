#pragma once
#include "Type.h"
#include <memory>

namespace sicily {
    namespace ast {
        class ArrayType : public Type {
            public:
                ArrayType(TypePtr elementType);
                virtual ~ArrayType();

                TypePtr GetElementType() const;

                xstring_t ToString() const;

            private:
                TypePtr elementType_;
        };

        typedef std::shared_ptr<ArrayType> ArrayTypePtr;
    };
};
