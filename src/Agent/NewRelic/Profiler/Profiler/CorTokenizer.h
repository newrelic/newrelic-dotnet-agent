// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include "../Common/Strings.h"
#include <memory>
#include <vector>
#include <map>
#include <string>
#include <stdint.h>
#include "../Common/OnDestruction.h"
#include "Win32Helpers.h"
#include "../Sicily/codegen/ITokenizer.h"

namespace NewRelic { namespace Profiler
{
    typedef std::vector<uint8_t> ByteVector;
    
    // this tokenizer is not safe to re-use across assemblies, it caches tokens that are assembly specific
    class CorTokenizer : public sicily::codegen::ITokenizer
    {
    public:
        CorTokenizer(CComPtr<IMetaDataAssemblyEmit> metaDataAssemblyEmit, CComPtr<IMetaDataEmit2> metaDataEmit, CComPtr<IMetaDataImport2> metaDataImport, CComPtr<IMetaDataAssemblyImport> metaDataAssemblyImport) :
            metaDataEmit(metaDataEmit),
            metaDataAssemblyEmit(metaDataAssemblyEmit),
            metaDataImport(metaDataImport),
            metaDataAssemblyImport(metaDataAssemblyImport)
        { }

        virtual uint32_t GetAssemblyRefToken(const xstring_t& assemblyName) override
        {
            HCORENUM enumerator = nullptr;
            mdAssemblyRef assemblyToken;
            ULONG assembliesFound = 0;
            OnDestruction enumerationCloser([&] { metaDataAssemblyImport->CloseEnum(enumerator); });
            while (SUCCEEDED(metaDataAssemblyImport->EnumAssemblyRefs(&enumerator, &assemblyToken, 1, &assembliesFound)) && assembliesFound > 0)
            {
                auto foundAssemblyName = GetAssemblyName(assemblyToken);
                if (Strings::EndsWith(foundAssemblyName, assemblyName))
                {
                    return assemblyToken;
                }
            }

            return S_FALSE;
        }

        virtual uint32_t GetTypeRefToken(const xstring_t& assemblyName, const xstring_t& fullyQualifiedName) override
        {
            auto resolutionScope = GetAssemblyRefToken(assemblyName);
            return GetTypeRefToken(resolutionScope, fullyQualifiedName);
        }

        virtual uint32_t GetTypeRefToken(const xstring_t& assemblyName, const xstring_t& name, const xstring_t& namespaceName) override
        {
            xstring_t fullyQualifiedName(namespaceName + _X(".") + name);
            return GetTypeRefToken(assemblyName, fullyQualifiedName);
        }

        virtual uint32_t GetTypeDefToken(const xstring_t& fullName) override
        {
            mdTypeDef typeToken;
            HRESULT hr = metaDataImport->FindTypeDefByName(ToWindowsString(fullName), 0, &typeToken);
            if (FAILED(hr))
            {
                throw NewRelic::Profiler::Win32Exception(hr);
            }
            return typeToken;
        }

        uint32_t GetTypeRefToken(uint32_t resolutionScope, const xstring_t& fullyQualifiedName)
        {
            uint32_t typeRefToken;
            ThrowOnError(metaDataEmit->DefineTypeRefByName, resolutionScope, ToWindowsString(fullyQualifiedName), &typeRefToken);
            return typeRefToken;
        }

        virtual uint32_t GetTypeSpecToken(const ByteVector& instantiationSignature) override
        {
            uint32_t typeSpecToken;
            ThrowOnError(metaDataEmit->GetTokenFromTypeSpec, instantiationSignature.data(), ULONG(instantiationSignature.size()), &typeSpecToken);
            return typeSpecToken;
        }

        virtual uint32_t GetMemberRefOrDefToken(uint32_t parent, const xstring_t& methodName, const ByteVector& signature) override
        {
            // try to find the member reference already defined for this module
            auto foundMemberRef = FindMemberReference(parent, methodName, signature);
            if (foundMemberRef != mdMemberRefNil)
                return foundMemberRef;

            // try to find the member as a definition defined on this type, this occurs when the type is defined by this module
            auto foundMethodDef = FindMethodDefinition(parent, methodName, signature);
            if (foundMethodDef != mdMethodDefNil)
                return foundMethodDef;

            // we couldn't find it already defined, so define a new reference
            mdMemberRef createdMemberReference = mdMemberRefNil;
            ThrowOnError(metaDataEmit->DefineMemberRef, parent, ToWindowsString(methodName), signature.data(), ULONG(signature.size()), &createdMemberReference);
            return createdMemberReference;
        }

        virtual uint32_t GetMethodDefinitionToken(const uint32_t& typeDefinitionToken, const xstring_t& name, const ByteVector& signature) override
        {
            uint32_t methodDefinitionToken;
            ThrowOnError(metaDataImport->FindMethod, typeDefinitionToken, ToWindowsString(name), signature.data(), (uint32_t)signature.size(), &methodDefinitionToken);
            return methodDefinitionToken;
        }

        virtual uint32_t GetMethodSpecToken(uint32_t methodDefOrRefOrSpecToken, const ByteVector& instantiationSignature) override
        {
            uint32_t methodSpecToken;
            ThrowOnError(metaDataEmit->DefineMethodSpec, methodDefOrRefOrSpecToken, instantiationSignature.data(), ULONG(instantiationSignature.size()), &methodSpecToken);

            return methodSpecToken;
        }

        virtual uint32_t GetStringToken(const xstring_t& string) override
        {
            xstring_t wstring(string.begin(), string.end());
            uint32_t stringToken= 0;
            ThrowOnError(metaDataEmit->DefineUserString, ToWindowsString(wstring), ULONG(wstring.size()), &stringToken);

            return stringToken;
        }

    protected:
        CComPtr<IMetaDataAssemblyEmit> metaDataAssemblyEmit;
    private:
        CComPtr<IMetaDataEmit2> metaDataEmit;
        CComPtr<IMetaDataImport2> metaDataImport;
        CComPtr<IMetaDataAssemblyImport> metaDataAssemblyImport;

        xstring_t GetAssemblyName(const mdAssemblyRef& assemblyReferenceToken)
        {
            ULONG assemblyNameLength = 0;
            ThrowOnError(metaDataAssemblyImport->GetAssemblyRefProps, assemblyReferenceToken, nullptr, nullptr, nullptr, 0, &assemblyNameLength, nullptr, nullptr, nullptr, nullptr);
            std::unique_ptr<WCHAR[]> assemblyName(new WCHAR[assemblyNameLength]);
            ThrowOnError(metaDataAssemblyImport->GetAssemblyRefProps, assemblyReferenceToken, nullptr, nullptr, assemblyName.get(), assemblyNameLength, nullptr, nullptr, nullptr, nullptr, nullptr);
            return ToStdWString(assemblyName.get());
        }

        xstring_t GetMemberReferenceName(const mdMemberRef& memberReference)
        {
            ULONG memberNameLength = 0;
            metaDataImport->GetMemberRefProps(memberReference, nullptr, nullptr, 0, &memberNameLength, nullptr, nullptr);
            std::unique_ptr<WCHAR[]> memberName(new WCHAR[memberNameLength]);
            metaDataImport->GetMemberRefProps(memberReference, nullptr, memberName.get(), memberNameLength, nullptr, nullptr, nullptr);
            return ToStdWString(memberName.get());
        }

        ByteVector GetMemberReferenceSignature(const mdMemberRef& memberReference)
        {
            const COR_SIGNATURE* signature;
            ULONG signatureLength = 0;
            metaDataImport->GetMemberRefProps(memberReference, nullptr, nullptr, 0, nullptr, &signature, &signatureLength);
            return ByteVector(signature, signature + signatureLength);
        }

        xstring_t GetMethodDefinitionName(const mdMethodDef& methodDefinition)
        {
            ULONG methodNameLength = 0;
            metaDataImport->GetMethodProps(methodDefinition, nullptr, nullptr, 0, &methodNameLength, nullptr, nullptr, nullptr, nullptr, nullptr);
            std::unique_ptr<WCHAR[]> methodName(new WCHAR[methodNameLength]);
            metaDataImport->GetMethodProps(methodDefinition, nullptr, methodName.get(), methodNameLength, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr);
            return ToStdWString(methodName.get());
        }

        ByteVector GetMethodDefinitionSignature(const mdMethodDef& methodDefinition)
        {
            const COR_SIGNATURE* signature;
            ULONG signatureLength = 0;
            metaDataImport->GetMethodProps(methodDefinition, nullptr, nullptr, 0, nullptr, nullptr, &signature, &signatureLength, nullptr, nullptr);
            return ByteVector(signature, signature + signatureLength);
        }

        mdMemberRef FindMemberReference(const mdToken& parent, const xstring_t& methodNameToFind, const ByteVector& methodSignatureToFind)
        {
            HCORENUM enumerator = nullptr;
            mdMemberRef memberReference = mdMemberRefNil;
            ULONG resultCount = 0;
            OnDestruction enumerationCloser([&] { metaDataAssemblyImport->CloseEnum(enumerator); });
            while (SUCCEEDED(metaDataImport->EnumMemberRefs(&enumerator, parent, &memberReference, 1, &resultCount)) && resultCount != 0)
            {
                auto foundMemberName = GetMemberReferenceName(memberReference);
                auto foundSignature = GetMemberReferenceSignature(memberReference);
                if (foundMemberName == methodNameToFind && foundSignature == methodSignatureToFind)
                    return memberReference;
            }

            return mdMemberRefNil;
        }

        mdMethodDef FindMethodDefinition(const mdTypeDef& parent, const xstring_t& methodNameToFind, const ByteVector& methodSignatureToFind)
        {
            if ((parent & 0xff000000) != CorTokenType::mdtTypeDef)
                return mdMethodDefNil;

            HCORENUM enumerator = nullptr;
            OnDestruction Conan([&] {if (enumerator) metaDataImport->CloseEnum(enumerator); });
            mdMethodDef methodDefinition = mdMethodDefNil;
            ULONG resultCount = 0;
            while (SUCCEEDED(metaDataImport->EnumMethods(&enumerator, parent, &methodDefinition, 1, &resultCount)) && resultCount != 0)
            {
                auto foundMethodName = GetMethodDefinitionName(methodDefinition);
                auto foundSignature = GetMethodDefinitionSignature(methodDefinition);
                if (foundMethodName == methodNameToFind && foundSignature == methodSignatureToFind)
                    return methodDefinition;
            }

            return mdMethodDefNil;
        }

    };

    class DotnetFrameworkCorTokenizer : public CorTokenizer
    {
    public:
        DotnetFrameworkCorTokenizer(CComPtr<IMetaDataAssemblyEmit> metaDataAssemblyEmit, CComPtr<IMetaDataEmit2> metaDataEmit, CComPtr<IMetaDataImport2> metaDataImport, CComPtr<IMetaDataAssemblyImport> metaDataAssemblyImport) :
            CorTokenizer(metaDataAssemblyEmit, metaDataEmit, metaDataImport, metaDataAssemblyImport),
            mscorlibAssemblyRefToken(mdAssemblyRefNil)
        { }

        virtual uint32_t GetAssemblyRefToken(const xstring_t& assemblyName) override
        {
            // we only support calling methods in mscorlib
            if (assemblyName != _X("mscorlib"))
            {
                LogError("Attempted to get an assembly ref token to something other than mscorlib. Since mscorlib can only call mscorlib, there are no other valid assemlbly refs available.  ", assemblyName);
                throw AssemblyNotSupportedException(assemblyName);
            }
            if (mscorlibAssemblyRefToken != mdAssemblyRefNil)
                return mscorlibAssemblyRefToken;

            auto token = CorTokenizer::GetAssemblyRefToken(assemblyName);
            if (token == S_FALSE)
            {
                token = CorTokenizer::GetAssemblyRefToken(_X("netstandard"));
                if (token == S_FALSE)
                {
                    token = CorTokenizer::GetAssemblyRefToken(_X("System.Runtime"));
                }
            }
            if (token != S_FALSE)
            {
                return token;
            }

            LogError(L"Unable to locate ", assemblyName, L" assembly reference in module.");
            throw NewRelic::Profiler::MessageException(_X("Unable to locate assembly reference in module."));
        }

    private:
        uint32_t mscorlibAssemblyRefToken;
    };


    class CoreCLRCorTokenizer : public CorTokenizer
    {
    public:
        CoreCLRCorTokenizer(CComPtr<IMetaDataAssemblyEmit> metaDataAssemblyEmit, CComPtr<IMetaDataEmit2> metaDataEmit, CComPtr<IMetaDataImport2> metaDataImport, CComPtr<IMetaDataAssemblyImport> metaDataAssemblyImport) :
            CorTokenizer(metaDataAssemblyEmit, metaDataEmit, metaDataImport, metaDataAssemblyImport),
            _typeNameToAssembly(std::make_shared<std::map<xstring_t,xstring_t>>())
        {
            _typeNameToAssembly->emplace(_X("System.Exception"), _X("System.Runtime"));
            _typeNameToAssembly->emplace(_X("System.Type"), _X("System.Runtime"));
            _typeNameToAssembly->emplace(_X("System.Reflection.Assembly"), _X("System.Reflection"));
            _typeNameToAssembly->emplace(_X("System.Reflection.MethodInfo"), _X("System.Reflection"));
            _typeNameToAssembly->emplace(_X("System.Reflection.MethodBase"), _X("System.Reflection"));
            _typeNameToAssembly->emplace(_X("System.Action`2"), _X("System.Core"));
            _typeNameToAssembly->emplace(_X("System.Console"), _X("System.Console"));
        }

        virtual uint32_t GetTypeRefToken(const xstring_t& assemblyName, const xstring_t& fullyQualifiedName) override
        {
            return CorTokenizer::GetTypeRefToken(ResolveAssemblyForType(assemblyName, fullyQualifiedName), fullyQualifiedName);
        }

        virtual uint32_t GetAssemblyRefToken(const xstring_t& requestedAssemblyName) override
        {
            xstring_t assemblyName = requestedAssemblyName == _X("mscorlib") ? _X("System.Runtime") : requestedAssemblyName;
            auto assemblyToken = CorTokenizer::GetAssemblyRefToken(assemblyName);

            if (assemblyToken == S_FALSE)
            {
                // if the assembly wasn't in the existing references try to define a new one
                ASSEMBLYMETADATA amd;
                ZeroMemory(&amd, sizeof(amd));
                amd.usMajorVersion = 0;
                amd.usMinorVersion = 0;
                amd.usBuildNumber = 0;
                amd.usRevisionNumber = 0;
                if (SUCCEEDED(metaDataAssemblyEmit->DefineAssemblyRef(NULL, 0, assemblyName.c_str(), &amd, NULL, 0, 0, &assemblyToken)))
                {
                    return assemblyToken;
                }
            }

            return assemblyToken;
        }

    private:
        std::shared_ptr<std::map<xstring_t, xstring_t>> _typeNameToAssembly;

        xstring_t ResolveAssemblyForType(xstring_t assemblyName, xstring_t fullQualifiedType)
        {
            auto coreAssembly = (*_typeNameToAssembly.get())[fullQualifiedType];
            return coreAssembly.empty() ? assemblyName : coreAssembly;
        }
    };

    typedef std::shared_ptr<CorTokenizer> CorTokenizerPtr;

    CorTokenizerPtr CreateCorTokenizer(CComPtr<IMetaDataAssemblyEmit> metaDataAssemblyEmit, CComPtr<IMetaDataEmit2> metaDataEmit, CComPtr<IMetaDataImport2> metaDataImport, CComPtr<IMetaDataAssemblyImport> metaDataAssemblyImport, bool isCoreCLR)
    {
        if (isCoreCLR) 
        {
            return std::make_shared<CoreCLRCorTokenizer>(metaDataAssemblyEmit, metaDataEmit, metaDataImport, metaDataAssemblyImport);
        }
        return std::make_shared<DotnetFrameworkCorTokenizer>(metaDataAssemblyEmit, metaDataEmit, metaDataImport, metaDataAssemblyImport);
    }

}}
