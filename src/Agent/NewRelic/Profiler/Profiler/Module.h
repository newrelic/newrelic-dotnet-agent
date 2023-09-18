// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <corprof.h>
#include <CorHdr.h>
#include "../Logging/Logger.h"
#include "../Common/OnDestruction.h"
#include "../ModuleInjector/IModule.h"
#include "CorTokenizer.h"
#include "Win32Helpers.h"

namespace NewRelic { namespace Profiler
{
    class Module : public ModuleInjector::IModule
    {
    public:
        Module(CComPtr<ICorProfilerInfo4> profilerInfo, const ModuleID& moduleId) :
            _profilerInfo(profilerInfo),
            _moduleId(moduleId)
        {
            // get the module's name
            ULONG moduleNameLength = 0;
            ThrowOnError(_profilerInfo->GetModuleInfo, _moduleId, nullptr, 0, &moduleNameLength, nullptr, nullptr);
            std::unique_ptr<wchar_t[]> moduleNameChars(new wchar_t[moduleNameLength]);
            ThrowOnError(_profilerInfo->GetModuleInfo, _moduleId, nullptr, moduleNameLength, nullptr, moduleNameChars.get(), nullptr);
            _moduleName = std::wstring(moduleNameChars.get());

            // get the necessary metadata interfaces
            ThrowOnError(_profilerInfo->GetModuleMetaData, moduleId, CorOpenFlags::ofWrite, IID_IMetaDataEmit2, (IUnknown**)&_metaDataEmit);
            ThrowOnError(_profilerInfo->GetModuleMetaData, moduleId, CorOpenFlags::ofRead, IID_IMetaDataImport2, (IUnknown**)&_metaDataImport);
            ThrowOnError(_profilerInfo->GetModuleMetaData, moduleId, CorOpenFlags::ofWrite, IID_IMetaDataAssemblyEmit, (IUnknown**)&_metaDataAssemblyEmit);
            ThrowOnError(_profilerInfo->GetModuleMetaData, moduleId, CorOpenFlags::ofRead, IID_IMetaDataAssemblyImport, (IUnknown**)&_metaDataAssemblyImport);

            if (_metaDataEmit == nullptr || _metaDataImport == nullptr || _metaDataAssemblyImport == nullptr)
            {
                LogWarn(L"Failed to get metadata from the module, it is likely a resource module.  ModuleName: ", _moduleName);
                throw NewRelic::Profiler::MessageException(L"Failed to get a metadata from the module.");
            }

            // in .NET 2, the name may be empty for dynamic modules so fallback to the scope properties for the name
            if (_moduleName.empty())
            {
                ThrowOnError(_metaDataImport->GetScopeProps, nullptr, 0, &moduleNameLength, nullptr);
                moduleNameChars = std::unique_ptr<wchar_t[]>(new wchar_t[moduleNameLength]);
                ThrowOnError(_metaDataImport->GetScopeProps, moduleNameChars.get(), moduleNameLength, nullptr, nullptr);
                _moduleName = std::wstring(moduleNameChars.get());
            }

            CheckIfThisIsAFrameworkAssembly();
            IdentifyFrameworkAssemblyReferences();

            _tokenizer.reset(new CorTokenizer(_metaDataAssemblyEmit, _metaDataEmit, _metaDataImport, _metaDataAssemblyImport));
        }

        virtual std::wstring GetModuleName() override { return _moduleName; }

        virtual bool GetHasRefMscorlib() override { return _hasRefMscorlib; }
        virtual bool GetHasRefSysRuntime() override { return _hasRefSysRuntime; }
        virtual bool GetHasRefNetStandard() override { return _hasRefNetStandard; }

        virtual void SetMscorlibAssemblyRef(mdAssembly assemblyRefToken) override { _mscorlibAssemblyRefToken = assemblyRefToken; }

        virtual bool GetIsThisTheMscorlibAssembly() override { return _isMscorlib; }
        virtual bool GetIsThisTheNetStandardAssembly() override { return _isNetStandard; }

        virtual CComPtr<IMetaDataAssemblyEmit> GetMetaDataAssemblyEmit() override{ return _metaDataAssemblyEmit; }

        virtual void InjectPlatformInvoke(const std::wstring& methodName, const std::wstring& className, const std::wstring& moduleName, const ByteVector& signature) override
        {
            auto interfaceAttributes = CorMethodAttr::mdStatic | CorMethodAttr::mdPublic | CorMethodAttr::mdPinvokeImpl;
            auto implementationAttributes = CorMethodImpl::miPreserveSig;
            auto mappingFlags = CorPinvokeMap::pmCallConvCdecl | CorPinvokeMap::pmNoMangle;

            auto injectionTargetClassToken = GetTypeToken(className);

            auto profilerDllToken = AddModuleReference(moduleName);
            auto injectedMethodToken = AddMethodDefinition(methodName, injectionTargetClassToken, signature, interfaceAttributes, implementationAttributes, 0);
            SetMethodAsPlatformInvoke(injectedMethodToken, mappingFlags, methodName, profilerDllToken);
        }

        virtual void InjectStaticSecuritySafeMethod(const std::wstring& methodName, const std::wstring& className, const ByteVector& signature) override
        {
            auto interfaceAttributes = CorMethodAttr::mdStatic | CorMethodAttr::mdPublic | CorMethodAttr::mdHideBySig;
            auto implementationAttributes = CorMethodImpl::miIL | CorMethodImpl::miNoInlining;
            auto permissionBlob = GetPermissionBlob();
            BYTEVECTOR(constructorSignature, CorCallingConvention::IMAGE_CEE_CS_CALLCONV_HASTHIS, 0, CorElementType::ELEMENT_TYPE_VOID);

            auto injectionTargetClassToken = GetTypeToken(className);
            auto constructorCodeAddress = GetMethodCodeAddress(injectionTargetClassToken, L".ctor", constructorSignature);
            auto securitySafeCriticalConstructorToken = GetSecuritySafeCriticalConstructorToken();
            auto suppressUnmanagedCodeSecurityAttributeToken = GetSuppressUnmanagedCodeSecurityAttributeToken();
            
            auto injectedMethodToken = AddMethodDefinition(methodName, injectionTargetClassToken, signature, interfaceAttributes, implementationAttributes, constructorCodeAddress);
            AddAttributeToMethod(injectedMethodToken, securitySafeCriticalConstructorToken);
            AddAttributeToMethod(injectedMethodToken, suppressUnmanagedCodeSecurityAttributeToken);
            AddPermissionSetAssertToMethod(injectedMethodToken, permissionBlob);
        }

        virtual void InjectNRHelperType() override
        {
            auto objectTypeToken = GetTypeToken(_X("System.Object"));
            mdTypeDef nrHelperType;
            ThrowOnError(_metaDataEmit->DefineTypeDef, _X("__NRInitializer__"), CorTypeAttr::tdAbstract | CorTypeAttr::tdSealed, objectTypeToken, NULL, &nrHelperType);

            auto dataFieldAttributes = CorFieldAttr::fdPublic | CorFieldAttr::fdStatic;
            BYTEVECTOR(dataFieldSignature, CorCallingConvention::IMAGE_CEE_CS_CALLCONV_FIELD, CorElementType::ELEMENT_TYPE_OBJECT);
            mdFieldDef dataFieldToken;
            ThrowOnError(_metaDataEmit->DefineField, nrHelperType, _X("_methodCache"), dataFieldAttributes, dataFieldSignature.data(), (uint32_t)dataFieldSignature.size(), 0, nullptr, 0, &dataFieldToken);
        }

        virtual void InjectMscorlibSecuritySafeMethodReference(const std::wstring& methodName, const std::wstring& className, const ByteVector& signature) override
        {
            auto typeReferenceOrDefinitionToken = GetOrCreateTypeReferenceToken(_mscorlibAssemblyRefToken, className);
            GetOrCreateMemberReferenceToken(typeReferenceOrDefinitionToken, methodName, signature);
        }

        virtual sicily::codegen::ITokenizerPtr GetTokenizer()
        {
            return _tokenizer;
        }

    private:
        CComPtr<ICorProfilerInfo4> _profilerInfo;
        CComPtr<IMetaDataEmit2> _metaDataEmit;
        CComPtr<IMetaDataImport2> _metaDataImport;
        CComPtr<IMetaDataAssemblyImport> _metaDataAssemblyImport;
        CComPtr<IMetaDataAssemblyEmit> _metaDataAssemblyEmit;
        sicily::codegen::ITokenizerPtr _tokenizer;
        ModuleID _moduleId;
        std::wstring _moduleName;
        mdAssemblyRef _mscorlibAssemblyRefToken;
        bool _hasRefMscorlib;
        bool _hasRefSysRuntime;
        bool _hasRefNetStandard;

        bool _isMscorlib;
        bool _isNetStandard;

        mdMethodDef GetSecuritySafeCriticalConstructorToken()
        {
            return GetMethodTokenForDefaultConstructor(L"System.Security.SecuritySafeCriticalAttribute");
        }

        mdMethodDef GetSuppressUnmanagedCodeSecurityAttributeToken()
        {
            return GetMethodTokenForDefaultConstructor(L"System.Security.SuppressUnmanagedCodeSecurityAttribute");
        }

        mdMethodDef GetMethodTokenForDefaultConstructor(const std::wstring& className)
        {
            BYTEVECTOR(constructorSignature, CorCallingConvention::IMAGE_CEE_CS_CALLCONV_HASTHIS, 0, CorElementType::ELEMENT_TYPE_VOID);

            auto typeToken = GetTypeToken(className);
            return GetMethodToken(typeToken, L".ctor", constructorSignature);
        }

        mdTypeDef GetTypeToken(const std::wstring& typeName)
        {
            mdTypeDef typeToken;
            ThrowOnError(_metaDataImport->FindTypeDefByName, typeName.c_str(), mdTypeDefNil, &typeToken);
            return typeToken;
        }

        mdModuleRef AddModuleReference(const std::wstring& moduleName)
        {
            mdModuleRef moduleToken;
            ThrowOnError(_metaDataEmit->DefineModuleRef, moduleName.c_str(), &moduleToken);
            return moduleToken;
        }

        mdMethodDef AddMethodDefinition(const std::wstring& methodName, const mdTypeDef& typeToken, const ByteVector& signature, const uint32_t& interfaceAttributes, const uint32_t& implementationAttributes, const uint32_t& methodCodeAddress)
        {
            mdMethodDef methodToken;
            ThrowOnError(_metaDataEmit->DefineMethod, typeToken, methodName.c_str(), interfaceAttributes, signature.data(), (uint32_t)signature.size(), methodCodeAddress, implementationAttributes, &methodToken);
            return methodToken;
        }

        void SetMethodAsPlatformInvoke(const mdMethodDef& methodToken, const uint32_t& mappingFlags, const std::wstring& methodName, const mdModuleRef& nativeModuleToken)
        {
            ThrowOnError(_metaDataEmit->DefinePinvokeMap, methodToken, mappingFlags, methodName.c_str(), nativeModuleToken);
        }

        mdMethodDef GetMethodToken(const mdTypeDef& classToken, const std::wstring methodName, const ByteVector& signature)
        {
            mdMethodDef methodToken;
            ThrowOnError(_metaDataImport->FindMethod, classToken, methodName.c_str(), signature.data(), (uint32_t)signature.size(), &methodToken);
            return methodToken;
        }

        ULONG GetMethodCodeAddress(const mdTypeDef& classToken, const std::wstring methodName, const ByteVector& signature)
        {
            ULONG codeAddress;
            auto methodToken = GetMethodToken(classToken, methodName, signature);
            ThrowOnError(_metaDataImport->GetMethodProps, methodToken, nullptr, nullptr, 0, nullptr, nullptr, nullptr, nullptr, &codeAddress, nullptr);
            return codeAddress;
        }

        void AddAttributeToMethod(const mdMethodDef& methodToken, const mdMethodDef& attributeConstructorToken)
        {
            uint8_t customAttribute[] = { 1, 0, 0, 0 };

            mdCustomAttribute customAttributeToken;
            ThrowOnError(_metaDataEmit->DefineCustomAttribute, methodToken, attributeConstructorToken, customAttribute, lengthof(customAttribute), &customAttributeToken);
        }

        void AddPermissionSetAssertToMethod(const mdMethodDef& methodToken, const ByteVector& permissionBlob)
        {
            mdPermission permissionToken;
            ThrowOnError(_metaDataEmit->DefinePermissionSet, methodToken, CorDeclSecurity::dclAssert, permissionBlob.data(), (uint32_t)permissionBlob.size(), &permissionToken);
        }

        std::wstring GetAssemblyName(const mdAssemblyRef& assemblyReferenceToken)
        {
            ULONG assemblyNameLength = 0;
            ThrowOnError(_metaDataAssemblyImport->GetAssemblyRefProps, assemblyReferenceToken, nullptr, nullptr, nullptr, 0, &assemblyNameLength, nullptr, nullptr, nullptr, nullptr);
            std::unique_ptr<wchar_t[]> assemblyName(new wchar_t[assemblyNameLength]);
            ThrowOnError(_metaDataAssemblyImport->GetAssemblyRefProps, assemblyReferenceToken, nullptr, nullptr, assemblyName.get(), assemblyNameLength, nullptr, nullptr, nullptr, nullptr, nullptr);
            return assemblyName.get();
        }

        void IdentifyFrameworkAssemblyReferences()
        {
            _hasRefMscorlib = false;
            _hasRefNetStandard = false;
            _hasRefSysRuntime = false;

            HCORENUM enumerator = nullptr;
            mdAssemblyRef assemblyToken;
            ULONG assembliesFound = 0;
            OnDestruction enumerationCloser([&] { _metaDataAssemblyImport->CloseEnum(enumerator); });
            while (SUCCEEDED(_metaDataAssemblyImport->EnumAssemblyRefs(&enumerator, &assemblyToken, 1, &assembliesFound)) && assembliesFound > 0)
            {
                auto foundAssemblyName = GetAssemblyName(assemblyToken);

                if (Strings::EndsWith(foundAssemblyName, L"mscorlib"))
                {
                    _hasRefMscorlib = true;
                    _mscorlibAssemblyRefToken = assemblyToken;
                }
                else if (Strings::EndsWith(foundAssemblyName, L"netstandard"s))
                {
                    _hasRefNetStandard = true;
                }
                else if (Strings::EndsWith(foundAssemblyName, L"System.Runtime"s))
                {
                    _hasRefSysRuntime = true;
                }
            }
        }

        void CheckIfThisIsAFrameworkAssembly()
        {
            _isMscorlib = Strings::EndsWith(GetModuleName(), L"mscorlib.dll");
            _isNetStandard = Strings::EndsWith(GetModuleName(), L"netstandard.dll");
        }

        mdAssemblyRef GetAssemblyReference(const std::wstring& assemblyName)
        {
            HCORENUM enumerator = nullptr;
            mdAssemblyRef assemblyToken;
            ULONG assembliesFound = 0;
            OnDestruction enumerationCloser([&] { _metaDataAssemblyImport->CloseEnum(enumerator); });
            while (SUCCEEDED(_metaDataAssemblyImport->EnumAssemblyRefs(&enumerator, &assemblyToken, 1, &assembliesFound)) && assembliesFound > 0)
            {
                auto foundAssemblyName = GetAssemblyName(assemblyToken);
                if (Strings::EndsWith(foundAssemblyName, assemblyName))
                {
                    return assemblyToken;
                }
            }

            auto moduleName = GetModuleName();
            // When we run across RefEmit_InMemoryManifestModule we need to bail out but we don't want to log errors along the way.
            if (moduleName == L"RefEmit_InMemoryManifestModule")
                throw NewRelic::Profiler::NonCriticalException();

            LogError(L"Unable to locate ", assemblyName, " reference in module ", moduleName, ".");
            throw NewRelic::Profiler::MessageException(L"Unable to locate assembly reference in module.");
        }

        mdToken GetOrCreateTypeReferenceToken(const mdAssemblyRef& assemblyReferenceToken, const std::wstring& className)
        {
            if (assemblyReferenceToken == mdAssemblyRefNil)
            {
                mdTypeDef typeDefinitionToken;
                ThrowOnError(_metaDataImport->FindTypeDefByName, className.c_str(), 0, &typeDefinitionToken);
                return typeDefinitionToken;
            }
            else
            {
                mdTypeRef typeReferenceToken;
                ThrowOnError(_metaDataEmit->DefineTypeRefByName, assemblyReferenceToken, className.c_str(), &typeReferenceToken);
                return typeReferenceToken;
            }
        }

        mdMemberRef GetOrCreateMemberReferenceToken(const mdToken& typeReferenceOrDefinitionToken, const std::wstring& memberName, const ByteVector& signature)
        {
            mdMemberRef memberReferenceToken;
            ThrowOnError(_metaDataEmit->DefineMemberRef, typeReferenceOrDefinitionToken, memberName.c_str(), signature.data(), (uint32_t)signature.size(), &memberReferenceToken);
            return memberReferenceToken;
        }

        ByteVector GetPermissionBlob()
        {
            std::string securityPermissionAttributeString("System.Security.Permissions.SecurityPermissionAttribute, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            auto securityPermissionAttributeStringLengthCompressed = sicily::codegen::ByteCodeGenerator::CorSigCompressData((uint32_t)securityPermissionAttributeString.length());
            std::string unmanagedCodeNamedParameterString("UnmanagedCode");

            ByteVector permissionBlob;
            // permission blobs always start with the '.' character
            permissionBlob.push_back('.');
            // the number of attributes encoded in the blob (1)
            permissionBlob.push_back(1);
            // the size of the fully-qualified name of the attribute being added
            permissionBlob.insert(permissionBlob.end(), securityPermissionAttributeStringLengthCompressed.begin(), securityPermissionAttributeStringLengthCompressed.end());
            // the fully-qualified name of the attribute being added
            permissionBlob.insert(permissionBlob.end(), securityPermissionAttributeString.begin(), securityPermissionAttributeString.end());
            // why is this 0x12 0x01?  spec says it should be little endian count of the number of named arguments (0x01 0x00) http://stackoverflow.com/questions/20753526/how-is-the-visual-studio-compiler-compiling-security-attributes-to-cil
            permissionBlob.push_back(0x12);
            // the number of arguments to this attribute (1)
            permissionBlob.push_back(0x01);
            // whether the first named attribute is a property or field
            permissionBlob.push_back(CorSerializationType::SERIALIZATION_TYPE_PROPERTY);
            // the type of the first named attribute
            permissionBlob.push_back(CorElementType::ELEMENT_TYPE_BOOLEAN);
            // the size of the full name (it is in mscorlib so fully-qualified name is not necessary) of the first named attribute
            permissionBlob.push_back((uint8_t)unmanagedCodeNamedParameterString.size());
            // the full name of the first named attribute
            permissionBlob.insert(permissionBlob.end(), unmanagedCodeNamedParameterString.begin(), unmanagedCodeNamedParameterString.end());
            // the value of the first named attribute (true)
            permissionBlob.push_back(1);

            return permissionBlob;
        }
    };
}}
