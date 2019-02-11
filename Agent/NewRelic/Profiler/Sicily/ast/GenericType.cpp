#include <cstring>
#include <cassert>
#include <sstream>

#include "GenericType.h"

namespace sicily {
    namespace ast {
        GenericType::GenericType(const xstring_t& name, const xstring_t& assembly, TypeListPtr genericTypes, bool raw, ClassKind kind) :
            ClassType(Type::Kind::kGENERICCLASS, name, assembly, raw, kind),
            genericTypes_(genericTypes)
        {
            assert(genericTypes != nullptr);
        }

        GenericType::~GenericType()
        {
        }

        TypeListPtr
        GenericType::GetGenericTypes() const
        {
            return genericTypes_;
        }

        xstring_t
        GenericType::ToString() const
        {
            auto buf = xstring_t();

            buf += ClassType::ToString();

            if (genericTypes_ != NULL) {
                size_t size = genericTypes_->GetSize();
                buf.push_back('`');
                buf += to_xstring((unsigned)size);
                buf.push_back('<');
                buf += genericTypes_->ToString();
                buf.push_back('>');
            }

            return buf;
        }
    };
};
