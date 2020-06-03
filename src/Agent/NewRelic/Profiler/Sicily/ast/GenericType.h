#pragma once
#include "ClassType.h"
#include "TypeList.h"

namespace sicily {
    namespace ast {
        class GenericType : public ClassType
        {
            public:
                GenericType(
                    const xstring_t& name,
                    const xstring_t& assembly = _X(""),
                    TypeListPtr genericTypes = TypeListPtr(new TypeList()),
                    bool raw = false,
                    ClassKind kind = ClassKind::CLASS
                );
                virtual ~GenericType();

                TypeListPtr GetGenericTypes() const;

                xstring_t ToString() const;

            private:
                TypeListPtr genericTypes_;
        };

        typedef std::shared_ptr<GenericType> GenericTypePtr;
    };
};
