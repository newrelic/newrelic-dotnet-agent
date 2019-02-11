#include "PrimitiveType.h"

namespace sicily {
    namespace ast {
        PrimitiveType::PrimitiveType(PrimitiveKind kind)
            : Type(kPRIMITIVE), primitiveKind_(kind)
        {
        }

        PrimitiveType::~PrimitiveType()
        {
        }

        PrimitiveType::PrimitiveKind
        PrimitiveType::GetPrimitiveKind() const
        {
            return primitiveKind_;
        }

        xstring_t
        PrimitiveType::ToString() const
        {
            switch (GetPrimitiveKind()) {
                case kOBJECT: return _X("object");
                case kSTRING: return _X("string");
                case kCHAR: return _X("char");
                case kVOID: return _X("void");
                case kBOOL: return _X("bool");
                case kI1: return _X("int8");
                case kI2: return _X("int16");
                case kI4: return _X("int32");
                case kI8: return _X("int64");
                case kR4: return _X("float32");
                case kR8: return _X("float64");
                case kU1: return _X("unsigned int8");
                case kU2: return _X("unsigned int16");
                case kU4: return _X("unsigned int32");
                case kU8: return _X("unsigned int64");
                //case kNATIVE_INT: return _X("native unsigned int");
                //case kNATIVE_UNSIGNED_INT: return _X("native unsigned int");
                //case kNATIVE_FLOAT: return _X("native float");
                default: return _X("unknown");
            };
        }
    };
};

