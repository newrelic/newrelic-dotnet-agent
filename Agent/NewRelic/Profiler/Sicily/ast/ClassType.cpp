#include <cstring>
#include <cassert>
#include <sstream>

#include "ClassType.h"

namespace sicily {
    namespace ast {
        ClassType::ClassType(const xstring_t& name, const xstring_t& assembly, bool raw, ClassKind classKind) :
            Type(Type::kCLASS),
            assembly_(assembly),
            name_(name),
            raw_(raw),
            classKind_(classKind)
        {
            assert(!name.empty());
        }

        ClassType::ClassType(Type::Kind kind, const xstring_t& name, const xstring_t& assembly, bool raw, ClassKind classKind) :
            Type(kind),
            assembly_(assembly),
            name_(name),
            raw_(raw),
            classKind_(classKind)
        {
            assert(!name.empty());
        }

        ClassType::~ClassType()
        {
        }

        xstring_t
        ClassType::GetAssembly() const
        {
            return assembly_;
        }

        xstring_t
        ClassType::GetName() const
        {
            return name_;
        }

        bool
        ClassType::IsRaw() const
        {
            return raw_;
        }

        ClassType::ClassKind
        ClassType::GetClassKind() const
        {
            return classKind_;
        }

        xstring_t
        ClassType::ToString() const
        {
            auto buf = xstring_t();

            if (!IsRaw()) {
                switch (classKind_)
                {
                case ClassKind::CLASS:
                    buf += _X("class ");
                    break;
                case ClassKind::VALUETYPE:
                    buf += _X("valuetype ");
                    break;
                }
            }

            if (!GetAssembly().empty()) {
                buf += _X("[") + GetAssembly() + _X("]");
            }

            buf += GetName();

            return buf;
        }
    };
};

