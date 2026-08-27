// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <memory>
#include "../Common/Macros.h"
#include "../Common/xplat.h"
#include "../SignatureParser/ITokenResolver.h"
#include "../SignatureParser/SignatureParser.h"
#include "../SignatureParser/Types.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace RuntimeAsync
{
    // A method compiled with .NET 11 runtime-async carries MethodImplAttributes.Async and does
    // NOT follow the return convention its metadata signature implies. Per the ECMA-335 augment
    // (dotnet/runtime docs/design/specs/runtime-async.md, I.8.4.5), before `ret` the stack holds
    // nothing for Task/ValueTask, or the type argument for Task<T>/ValueTask<T>. The declared
    // task type never appears on the stack at all.
    //
    // These helpers map a declared return type onto the type the IL body actually produces, so
    // the rewriter can size its result local, store, box and return against reality instead of
    // against the signature. See NR-610232.

    // Names come from IMetaDataImport::GetTypeDefProps / GetTypeRefProps via CorTokenResolver,
    // i.e. raw ECMA-335 metadata names -- so the generic forms carry the `1 arity suffix and the
    // non-generic forms must not.
    inline bool IsNonGenericTaskTypeName(const xstring_t& typeName)
    {
        return typeName == _X("System.Threading.Tasks.Task")
            || typeName == _X("System.Threading.Tasks.ValueTask");
    }

    inline bool IsGenericTaskTypeName(const xstring_t& typeName)
    {
        return typeName == _X("System.Threading.Tasks.Task`1")
            || typeName == _X("System.Threading.Tasks.ValueTask`1");
    }

    // Returns the type the method's IL body leaves on the stack before `ret`:
    //   Task, ValueTask        -> a void return type (nothing is pushed)
    //   Task<T>, ValueTask<T>  -> T
    //   anything else          -> nullptr
    //
    // nullptr means "this is not a shape we understand" and callers MUST decline to instrument.
    // It is the expected answer for an inert Async flag: the spec says the flag "only has effect"
    // on Task/ValueTask returns, so it can legally appear on a method that still uses the ordinary
    // synchronous convention. Rewriting such a method against the async convention would inject
    // the very InvalidProgramException this code exists to prevent, so an unrecognized shape is
    // never a reason to guess.
    //
    // Recognition is by metadata type name rather than by signature element kind because
    // ValueTask and ValueTask`1 are structs (ELEMENT_TYPE_VALUETYPE) while Task and Task`1 are
    // classes (ELEMENT_TYPE_CLASS); the name is the one thing common to all four.
    inline SignatureParser::ReturnTypePtr GetEffectiveReturnType(
        SignatureParser::ReturnTypePtr declaredReturnType,
        SignatureParser::ITokenResolverPtr tokenResolver)
    {
        if (declaredReturnType == nullptr || tokenResolver == nullptr)
        {
            return nullptr;
        }

        // void and TypedReference returns cannot be task types
        if (declaredReturnType->_kind != SignatureParser::ReturnType::Kind::TYPED_RETURN_TYPE)
        {
            return nullptr;
        }

        auto typedReturnType = std::static_pointer_cast<SignatureParser::TypedReturnType>(declaredReturnType);

        // `ref Task` is a byref to a task, not an async method returning one
        if (typedReturnType->_isByRef || typedReturnType->_type == nullptr)
        {
            return nullptr;
        }

        auto declaredType = typedReturnType->_type;

        // Name resolution runs on the JIT path and CorTokenResolver throws for TypeSpec tokens and
        // unhandled token types. Degrade to "unrecognized" -- which the caller turns into a skip --
        // rather than letting it escape into a profiler callback.
        try
        {
            if (declaredType->_kind == SignatureParser::Type::Kind::GENERIC)
            {
                auto genericType = std::static_pointer_cast<SignatureParser::GenericType>(declaredType);

                // a task type has exactly one type argument; anything else is a different type
                // that merely shares the name
                if (genericType->_type == nullptr
                    || genericType->_genericArgumentTypes == nullptr
                    || genericType->_genericArgumentTypes->size() != 1)
                {
                    return nullptr;
                }

                if (!IsGenericTaskTypeName(genericType->_type->ToString(tokenResolver)))
                {
                    return nullptr;
                }

                // exactly one level of unwrapping: Task<Task<int>> yields Task<int>
                return std::make_shared<SignatureParser::TypedReturnType>(genericType->_genericArgumentTypes->at(0), false);
            }

            if (!IsNonGenericTaskTypeName(declaredType->ToString(tokenResolver)))
            {
                return nullptr;
            }

            return std::make_shared<SignatureParser::VoidReturnType>();
        }
        catch (...)
        {
            return nullptr;
        }
    }

    // Same question, asked from raw signature bytes, for callers that have not already parsed the
    // method signature (the instrumentors decide whether to skip before any manipulator exists).
    // A signature that will not parse is "unrecognized" like any other unknown shape.
    inline SignatureParser::ReturnTypePtr GetEffectiveReturnTypeFromSignature(
        ByteVectorPtr methodSignature,
        SignatureParser::ITokenResolverPtr tokenResolver)
    {
        if (methodSignature == nullptr)
        {
            return nullptr;
        }

        try
        {
            auto parsedSignature = SignatureParser::SignatureParser::ParseMethodSignature(methodSignature->begin(), methodSignature->end());
            if (parsedSignature == nullptr)
            {
                return nullptr;
            }

            return GetEffectiveReturnType(parsedSignature->_returnType, tokenResolver);
        }
        catch (...)
        {
            return nullptr;
        }
    }
}}}}
