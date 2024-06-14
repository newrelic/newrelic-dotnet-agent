// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include "../Common/OnDestruction.h"
#include "../SignatureParser/ITokenResolver.h"
#include "../Logging/Logger.h"
#include "Exceptions.h"
#include "Win32Helpers.h"

namespace NewRelic { namespace Profiler
{
    class CorTokenResolver : public SignatureParser::ITokenResolver
    {
    private:
        CComPtr<IMetaDataImport2> _metaDataImport;

    public:
        CorTokenResolver(CComPtr<IMetaDataImport2> metaDataImport) : _metaDataImport(metaDataImport) {}

        xstring_t GetTypeStringFromTypeDef(uint32_t typeDefOrRefOrSpecToken)
        {
            ULONG typeNameLength;
            _metaDataImport->GetTypeDefProps(typeDefOrRefOrSpecToken, nullptr, 0, &typeNameLength, nullptr, nullptr);
            std::unique_ptr<WCHAR[]> typeName(new WCHAR[typeNameLength]);
            _metaDataImport->GetTypeDefProps(typeDefOrRefOrSpecToken, typeName.get(), typeNameLength, nullptr, nullptr, nullptr);

            return ToStdWString(typeName.get());
        }

        xstring_t GetTypeStringsFromTypeRef(uint32_t typeDefOrRefOrSpecToken)
        {
            mdAssemblyRef assemblyRef;

            ULONG typeNameLength;
            _metaDataImport->GetTypeRefProps(typeDefOrRefOrSpecToken, nullptr, nullptr, 0, &typeNameLength);
            std::unique_ptr<WCHAR[]> typeName(new WCHAR[typeNameLength]);
            _metaDataImport->GetTypeRefProps(typeDefOrRefOrSpecToken, &assemblyRef, typeName.get(), typeNameLength, nullptr);

            return ToStdWString(typeName.get());
        }

        xstring_t GetTypeStringsFromTypeSpec(uint32_t typeDefOrRefOrSpecToken)
        {
            uint8_t* signature = 0;
            ULONG signatureLength;
            _metaDataImport->GetTypeSpecFromToken(typeDefOrRefOrSpecToken, (PCCOR_SIGNATURE*)(&signature), &signatureLength);
            
            LogError(L"TypeSpec parameters not supported.");
            throw ProfilerException();

            //return xstring_t(_X("MyAssemblyName"), _X(""), _X("MyClassName"));
        }

        virtual xstring_t GetTypeStringsFromTypeDefOrRefOrSpecToken(uint32_t typeDefOrRefOrSpecToken) override
        {
            uint8_t tokenType = (typeDefOrRefOrSpecToken >> 24) & 0xff;
            switch (tokenType)
            {
                // TypeRef
                case 0x01:
                {
                    return GetTypeStringsFromTypeRef(typeDefOrRefOrSpecToken);
                }

                // TypeDef
                case 0x02:
                {
                    return GetTypeStringFromTypeDef(typeDefOrRefOrSpecToken);
                }

                // TypeSpec
                case 0x1b:
                {
                    return GetTypeStringsFromTypeSpec(typeDefOrRefOrSpecToken);
                }

                default:
                {
                    LogError(L"Attempted to lookup type strings for an unhandled token type.  Token type: ", tokenType);
                    throw ProfilerException();
                }
            }
        }

        virtual uint32_t GetTypeGenericArgumentCount(uint32_t typeDefOrMethodDefToken) override
        {
            uint32_t count = 0;
            HCORENUM enumerator = nullptr;
            mdGenericParam genericParam;
            ULONG resultCount = 0;
            OnDestruction enumerationCloser([&] { _metaDataImport->CloseEnum(enumerator); });
            while (SUCCEEDED(_metaDataImport->EnumGenericParams(&enumerator, typeDefOrMethodDefToken, &genericParam, 1, &resultCount)) && resultCount != 0)
            {
                ++count;
            }
            return count;
        }
    };
    typedef std::shared_ptr<CorTokenResolver> CorTokenResolverPtr;
}}
