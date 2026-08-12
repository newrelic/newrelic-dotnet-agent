/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include "../../Common/xplat.h"
#include <vector>
#include <memory>
#include <stdint.h>

namespace sicily
{
    namespace codegen
    {
        typedef std::vector<uint8_t> ByteVector;
        typedef std::shared_ptr<ByteVector> ByteVectorPtr;

        class ITokenizer
        {
        public:
            virtual uint32_t GetAssemblyRefToken(const xstring_t& assemblyName) = 0;
            virtual uint32_t GetTypeRefToken(const xstring_t& assemblyName, const xstring_t& fullyQualifiedName) = 0;
            virtual uint32_t GetTypeRefToken(const xstring_t& assemblyName, const xstring_t& name, const xstring_t& namespaceName) = 0;
            virtual uint32_t GetTypeDefToken(const xstring_t& fullName) = 0;
            virtual uint32_t GetTypeSpecToken(const ByteVector& instantiationSignature) = 0;
            virtual uint32_t GetMemberRefOrDefToken(uint32_t parent, const xstring_t& methodName, const ByteVector& signature) = 0;
            virtual uint32_t GetMethodDefinitionToken(const uint32_t& typeDefinitionToken, const xstring_t& name, const ByteVector& signature) = 0;
            // Resolves a field by name alone. Unlike GetMethodDefinitionToken above, no signature
            // is passed, so three constraints apply:
            //   - The field's declared type is NOT validated. A sicily string whose field type
            //     disagrees with the real field still resolves. The type is inert: it never
            //     reaches the emitted IL, so a wrong type produces identical bytecode rather
            //     than a fault.
            //   - typeDefinitionToken must be a TypeDef in the module being rewritten.
            //     IMetaDataImport::FindField takes an mdTypeDef; a TypeRef will not resolve.
            //     Today this holds by construction -- the only field signatures are for
            //     __NRInitializer__, whose TypeDef is created in the corelib module
            //     (ModuleInjector::InjectIntoModule) and whose field IL is emitted only into
            //     that same module (HelperInstrumentor::Instrument). Those two corelib checks
            //     are separate copies of the same predicate; keep them in step.
            //   - Inherited fields are not found, only ones declared directly on the type.
            virtual uint32_t GetFieldDefinitionToken(const uint32_t& typeDefinitionToken, const xstring_t& name) = 0;
            virtual uint32_t GetMethodSpecToken(uint32_t methodDefOrRefOrSpecToken, const ByteVector& instantiationSignature) = 0;
            virtual uint32_t GetStringToken(const xstring_t& string) = 0;
            virtual ~ITokenizer() {}
        };

        typedef std::shared_ptr<ITokenizer> ITokenizerPtr;
    }
}
