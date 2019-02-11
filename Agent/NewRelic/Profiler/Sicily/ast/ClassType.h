#pragma once
#include "Type.h"

namespace sicily {
    namespace ast {
        class ClassType : public Type
        {
            public:
                enum ClassKind
                {
                    VALUETYPE = 0x11,
                    CLASS = 0x12,
                };

                ClassType(
                    const xstring_t& name,
                    const xstring_t& assembly = _X(""),
                    bool raw = false,
                    ClassKind classKind = CLASS
                );
                virtual ~ClassType();

                xstring_t GetAssembly() const;
                xstring_t GetName() const;
                bool IsRaw() const;
                ClassKind GetClassKind() const;

                xstring_t ToString() const;

            protected:
                ClassType(
                    const Type::Kind kind,
                    const xstring_t& name,
                    const xstring_t& assembly = _X(""),
                    bool raw = false,
                    ClassKind classKind = CLASS);

            private:
                xstring_t assembly_;
                xstring_t name_;
                bool raw_;
                ClassKind classKind_;
        };

        typedef std::shared_ptr<ClassType> ClassTypePtr;
    };
};
