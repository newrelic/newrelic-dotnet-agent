// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <corprof.h>
#include <corhdr.h>
#include "../Logging/Logger.h"
#include "../Common/OnDestruction.h"
#include "../ModuleInjector/IModule.h"
#include "../SignatureParser/ByteVectorManipulator.h"
#include "CorTokenizer.h"
#include "Win32Helpers.h"

#define SYSTEM_PRIVATE_CORELIB_ASSEMBLYNAME _X("System.Private.CoreLib")
#define MSCORLIB_ASSEMBLYNAME _X("mscorlib")

namespace NewRelic { namespace Profiler
{
    class Module : public ModuleInjector::IModule
    {
    public:
        Module(CComPtr<ICorProfilerInfo4> profilerInfo, const ModuleID& moduleId, bool isCoreClr) :
            _profilerInfo(profilerInfo),
            _moduleId(moduleId),
            _isCoreClr(isCoreClr)
        {
            // get the module's name
            ULONG moduleNameLength = 0;
            ThrowOnError(_profilerInfo->GetModuleInfo, _moduleId, nullptr, 0, &moduleNameLength, nullptr, nullptr);
            std::unique_ptr<xchar_t[]> moduleNameChars(new xchar_t[moduleNameLength]);
            ThrowOnError(_profilerInfo->GetModuleInfo, _moduleId, nullptr, moduleNameLength, nullptr, moduleNameChars.get(), nullptr);
            _moduleName = xstring_t(moduleNameChars.get());

            // get the necessary metadata interfaces
            ThrowOnError(_profilerInfo->GetModuleMetaData, moduleId, CorOpenFlags::ofWrite, IID_IMetaDataEmit2, (IUnknown**)&_metaDataEmit);
            ThrowOnError(_profilerInfo->GetModuleMetaData, moduleId, CorOpenFlags::ofRead, IID_IMetaDataImport2, (IUnknown**)&_metaDataImport);
            ThrowOnError(_profilerInfo->GetModuleMetaData, moduleId, CorOpenFlags::ofWrite, IID_IMetaDataAssemblyEmit, (IUnknown**)&_metaDataAssemblyEmit);
            ThrowOnError(_profilerInfo->GetModuleMetaData, moduleId, CorOpenFlags::ofRead, IID_IMetaDataAssemblyImport, (IUnknown**)&_metaDataAssemblyImport);

            if (_metaDataEmit == nullptr || _metaDataImport == nullptr || _metaDataAssemblyImport == nullptr)
            {
                LogWarn(L"Failed to get metadata from the module, it is likely a resource module.  ModuleName: ", _moduleName);
                throw NewRelic::Profiler::MessageException(_X("Failed to get a metadata from the module."));
            }

            // in .NET 2, the name may be empty for dynamic modules so fallback to the scope properties for the name
            if (_moduleName.empty())
            {
                ThrowOnError(_metaDataImport->GetScopeProps, nullptr, 0, &moduleNameLength, nullptr);
                moduleNameChars = std::unique_ptr<xchar_t[]>(new xchar_t[moduleNameLength]);
                ThrowOnError(_metaDataImport->GetScopeProps, moduleNameChars.get(), moduleNameLength, nullptr, nullptr);
                _moduleName = xstring_t(moduleNameChars.get());
            }

            CheckIfThisIsAFrameworkAssembly();
            IdentifyFrameworkAssemblyReferences();

            _tokenizer = CreateCorTokenizer(_metaDataAssemblyEmit, _metaDataEmit, _metaDataImport, _metaDataAssemblyImport, _isCoreClr);
        }

        virtual xstring_t GetModuleName() override { return _moduleName; }

        virtual bool NeedsReferenceToCoreLib() override
        {
            return !(_isMscorlib || _isNetStandard || _isSystemPrivateCoreLib || _hasCoreLibReference);
        }

        virtual bool GetIsThisTheCoreLibAssembly() override { return _isCoreLibAssembly; }

        virtual void InjectPlatformInvoke(const xstring_t& methodName, const xstring_t& className, const xstring_t& moduleName, const ByteVector& signature) override
        {
            auto interfaceAttributes = CorMethodAttr::mdStatic | CorMethodAttr::mdPublic | CorMethodAttr::mdPinvokeImpl;
            auto implementationAttributes = CorMethodImpl::miPreserveSig;
            auto mappingFlags = CorPinvokeMap::pmCallConvCdecl | CorPinvokeMap::pmNoMangle;

            auto injectionTargetClassToken = GetTypeToken(className);

            auto profilerDllToken = AddModuleReference(moduleName);
            auto injectedMethodToken = AddMethodDefinition(methodName, injectionTargetClassToken, signature, interfaceAttributes, implementationAttributes, 0);
            SetMethodAsPlatformInvoke(injectedMethodToken, mappingFlags, methodName, profilerDllToken);
        }

        virtual void InjectStaticSecuritySafeMethod(const xstring_t& methodName, const xstring_t& className, const ByteVector& signature) override
        {
            auto interfaceAttributes = CorMethodAttr::mdStatic | CorMethodAttr::mdPublic | CorMethodAttr::mdHideBySig;
            auto implementationAttributes = CorMethodImpl::miIL | CorMethodImpl::miNoInlining;
            auto permissionBlob = GetPermissionBlob();
            BYTEVECTOR(constructorSignature, CorCallingConvention::IMAGE_CEE_CS_CALLCONV_HASTHIS, 0, CorElementType::ELEMENT_TYPE_VOID);

            auto injectionTargetClassToken = GetTypeToken(className);
            auto constructorCodeAddress = GetMethodCodeAddress(injectionTargetClassToken, _X(".ctor"), constructorSignature);
            auto securitySafeCriticalConstructorToken = GetSecuritySafeCriticalConstructorToken();
            auto suppressUnmanagedCodeSecurityAttributeToken = GetSuppressUnmanagedCodeSecurityAttributeToken();
            
            auto injectedMethodToken = AddMethodDefinition(methodName, injectionTargetClassToken, signature, interfaceAttributes, implementationAttributes, constructorCodeAddress);
            AddAttributeToMethod(injectedMethodToken, securitySafeCriticalConstructorToken);
            AddAttributeToMethod(injectedMethodToken, suppressUnmanagedCodeSecurityAttributeToken);
            AddPermissionSetAssertToMethod(injectedMethodToken, permissionBlob);
        }

        virtual void InjectStaticSecuritySafeCtor(const xstring_t& methodName, const xstring_t& className, const ByteVector& signature) override
        {
            auto interfaceAttributes = CorMethodAttr::mdStatic | CorMethodAttr::mdPrivate | CorMethodAttr::mdHideBySig | CorMethodAttr::mdSpecialName | CorMethodAttr::mdRTSpecialName;
            auto implementationAttributes = CorMethodImpl::miIL | CorMethodImpl::miNoInlining;
            auto permissionBlob = GetPermissionBlob();
            BYTEVECTOR(constructorSignature, CorCallingConvention::IMAGE_CEE_CS_CALLCONV_HASTHIS, 0, CorElementType::ELEMENT_TYPE_VOID);

            auto injectionTargetClassToken = GetTypeToken(className);
            auto constructorCodeAddress = GetMethodCodeAddress(injectionTargetClassToken, _X(".ctor"), constructorSignature);
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

            //auto isVolatileTypeToken = GetTypeToken(_X("System.Runtime.CompilerServices.IsVolatile"));
            //auto isVolatileSignature = Profiler::SignatureParser::CompressToken(isVolatileTypeToken);
            auto dataFieldAttributes = CorFieldAttr::fdPublic | CorFieldAttr::fdStatic;
            BYTEVECTOR(dataFieldSignature, CorCallingConvention::IMAGE_CEE_CS_CALLCONV_FIELD, CorElementType::ELEMENT_TYPE_OBJECT /*, CorElementType::ELEMENT_TYPE_CMOD_REQD*/);
            //dataFieldSignature.insert(dataFieldSignature.end(), isVolatileSignature->begin(), isVolatileSignature->end());

            mdFieldDef dataFieldToken;
            ThrowOnError(_metaDataEmit->DefineField, nrHelperType, _X("_agentMethodFunc"), dataFieldAttributes, dataFieldSignature.data(), (uint32_t)dataFieldSignature.size(), 0, nullptr, 0, &dataFieldToken);
            ThrowOnError(_metaDataEmit->DefineField, nrHelperType, _X("_agentShimFunc"), dataFieldAttributes, dataFieldSignature.data(), (uint32_t)dataFieldSignature.size(), 0, nullptr, 0, &dataFieldToken);
            ThrowOnError(_metaDataEmit->DefineField, nrHelperType, _X("_agentShimMethodInfo"), dataFieldAttributes, dataFieldSignature.data(), (uint32_t)dataFieldSignature.size(), 0, nullptr, 0, &dataFieldToken);

            //BYTEVECTOR(intFieldSignature, CorCallingConvention::IMAGE_CEE_CS_CALLCONV_FIELD, CorElementType::ELEMENT_TYPE_I4);
            //ThrowOnError(_metaDataEmit->DefineField, nrHelperType, _X("_isInitialized"), dataFieldAttributes, intFieldSignature.data(), (uint32_t)intFieldSignature.size(), 0, nullptr, 0, &dataFieldToken);
        }

        virtual void InjectCoreLibSecuritySafeMethodReference(const xstring_t& methodName, const xstring_t& className, const ByteVector& signature) override
        {
            auto typeReferenceOrDefinitionToken = GetOrCreateTypeReferenceToken(_coreLibAssemblyRefToken, className);
            GetOrCreateMemberReferenceToken(typeReferenceOrDefinitionToken, methodName, signature);
        }

        virtual bool InjectReferenceToCoreLib() override
        {
            const auto coreLibName = _isCoreClr ? SYSTEM_PRIVATE_CORELIB_ASSEMBLYNAME : MSCORLIB_ASSEMBLYNAME;
            constexpr const BYTE pubTokenCoreClr[] = { 0x7C, 0xEC, 0x85, 0xD7, 0xBE, 0xA7, 0x79, 0x8E };
            constexpr const BYTE pubTokenNetFramework[] = { 0xB7, 0x7A, 0x5C, 0x56, 0x19, 0x34, 0xE0, 0x89 };

            try
            {
                LogDebug(L"Attempting to Inject reference to ", coreLibName, L" into Module  ", GetModuleName());

                // if the assembly wasn't in the existing references try to define a new one
                ASSEMBLYMETADATA amd;
                ZeroMemory(&amd, sizeof(amd));
                amd.usMajorVersion = _isCoreClr ? 6 : 4;
                amd.usMinorVersion = 0;
                amd.usBuildNumber = 0;
                amd.usRevisionNumber = 0;

                auto pubToken = _isCoreClr ? pubTokenCoreClr : pubTokenNetFramework;

                auto injectResult = _metaDataAssemblyEmit->DefineAssemblyRef(pubToken, sizeof(pubToken), coreLibName, &amd, NULL, 0, 0, &_coreLibAssemblyRefToken);
                if (injectResult == S_OK)
                {
                    LogDebug(L"Attempting to Inject reference to ", coreLibName, L" into Module  ", GetModuleName(), L" - Success: ", _coreLibAssemblyRefToken);

                    return true;
                }
                else
                {
                    LogDebug(L"Attempting to Inject reference to ", coreLibName, L" into Module  ", GetModuleName(), L" - FAIL: ", injectResult);

                    return false;
                }
            }
            catch (NewRelic::Profiler::Win32Exception& ex)
            {
                LogError(L"Attempting to Inject reference to ", coreLibName, L" into Module  ", GetModuleName(), L" - ERROR: ", ex._message);
                return false;
            }
        }

        virtual sicily::codegen::ITokenizerPtr GetTokenizer() override
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
        xstring_t _moduleName;
        mdAssemblyRef _coreLibAssemblyRefToken;
        const bool _isCoreClr;
        bool _hasCoreLibReference;

        bool _isMscorlib;
        bool _isNetStandard;
        bool _isSystemPrivateCoreLib;
        bool _isCoreLibAssembly;

        mdMethodDef GetSecuritySafeCriticalConstructorToken()
        {
            return GetMethodTokenForDefaultConstructor(_X("System.Security.SecuritySafeCriticalAttribute"));
        }

        mdMethodDef GetSuppressUnmanagedCodeSecurityAttributeToken()
        {
            return GetMethodTokenForDefaultConstructor(_X("System.Security.SuppressUnmanagedCodeSecurityAttribute"));
        }

        mdMethodDef GetMethodTokenForDefaultConstructor(const xstring_t& className)
        {
            BYTEVECTOR(constructorSignature, CorCallingConvention::IMAGE_CEE_CS_CALLCONV_HASTHIS, 0, CorElementType::ELEMENT_TYPE_VOID);

            auto typeToken = GetTypeToken(className);
            return GetMethodToken(typeToken, _X(".ctor"), constructorSignature);
        }

        mdTypeDef GetTypeToken(const xstring_t& typeName)
        {
            mdTypeDef typeToken;
            ThrowOnError(_metaDataImport->FindTypeDefByName, typeName.c_str(), mdTypeDefNil, &typeToken);
            return typeToken;
        }

        mdModuleRef AddModuleReference(const xstring_t& moduleName)
        {
            mdModuleRef moduleToken;
            ThrowOnError(_metaDataEmit->DefineModuleRef, moduleName.c_str(), &moduleToken);
            return moduleToken;
        }

        mdMethodDef AddMethodDefinition(const xstring_t& methodName, const mdTypeDef& typeToken, const ByteVector& signature, const uint32_t& interfaceAttributes, const uint32_t& implementationAttributes, const uint32_t& methodCodeAddress)
        {
            mdMethodDef methodToken;
            ThrowOnError(_metaDataEmit->DefineMethod, typeToken, methodName.c_str(), interfaceAttributes, signature.data(), (uint32_t)signature.size(), methodCodeAddress, implementationAttributes, &methodToken);
            return methodToken;
        }

        void SetMethodAsPlatformInvoke(const mdMethodDef& methodToken, const uint32_t& mappingFlags, const xstring_t& methodName, const mdModuleRef& nativeModuleToken)
        {
            ThrowOnError(_metaDataEmit->DefinePinvokeMap, methodToken, mappingFlags, methodName.c_str(), nativeModuleToken);
        }

        mdMethodDef GetMethodToken(const mdTypeDef& classToken, const xstring_t& methodName, const ByteVector& signature)
        {
            mdMethodDef methodToken;
            ThrowOnError(_metaDataImport->FindMethod, classToken, methodName.c_str(), signature.data(), (uint32_t)signature.size(), &methodToken);
            return methodToken;
        }

        ULONG GetMethodCodeAddress(const mdTypeDef& classToken, const xstring_t& methodName, const ByteVector& signature)
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

        xstring_t GetAssemblyName(const mdAssemblyRef& assemblyReferenceToken)
        {
            ULONG assemblyNameLength = 0;
            ThrowOnError(_metaDataAssemblyImport->GetAssemblyRefProps, assemblyReferenceToken, nullptr, nullptr, nullptr, 0, &assemblyNameLength, nullptr, nullptr, nullptr, nullptr);
            std::unique_ptr<xchar_t[]> assemblyName(new xchar_t[assemblyNameLength]);
            ThrowOnError(_metaDataAssemblyImport->GetAssemblyRefProps, assemblyReferenceToken, nullptr, nullptr, assemblyName.get(), assemblyNameLength, nullptr, nullptr, nullptr, nullptr, nullptr);
            return assemblyName.get();
        }

        void IdentifyFrameworkAssemblyReferences()
        {
            _hasCoreLibReference = false;

            const auto assemblyNameToFind = _isCoreClr ? SYSTEM_PRIVATE_CORELIB_ASSEMBLYNAME : MSCORLIB_ASSEMBLYNAME;

            HCORENUM enumerator = nullptr;
            mdAssemblyRef assemblyToken;
            ULONG assembliesFound = 0;
            OnDestruction enumerationCloser([&] { _metaDataAssemblyImport->CloseEnum(enumerator); });
            while (SUCCEEDED(_metaDataAssemblyImport->EnumAssemblyRefs(&enumerator, &assemblyToken, 1, &assembliesFound)) && assembliesFound > 0)
            {
                auto foundAssemblyName = GetAssemblyName(assemblyToken);

                if (Strings::EndsWith(foundAssemblyName, assemblyNameToFind))
                {
                    _hasCoreLibReference = true;
                    _coreLibAssemblyRefToken = assemblyToken;
                }
            }
        }

        void CheckIfThisIsAFrameworkAssembly()
        {
            _isMscorlib = Strings::EndsWith(GetModuleName(), _X("mscorlib.dll"));
            _isNetStandard = Strings::EndsWith(GetModuleName(), _X("netstandard.dll"));
            _isSystemPrivateCoreLib = Strings::EndsWith(GetModuleName(), _X("System.Private.CoreLib.dll"));
            _isCoreLibAssembly = _isCoreClr ? _isSystemPrivateCoreLib : _isMscorlib;
        }

        mdAssemblyRef GetAssemblyReference(const xstring_t& assemblyName)
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
            if (moduleName == _X("RefEmit_InMemoryManifestModule"))
                throw NewRelic::Profiler::NonCriticalException();

            LogError(L"Unable to locate ", assemblyName, " reference in module ", moduleName, ".");
            throw NewRelic::Profiler::MessageException(_X("Unable to locate assembly reference in module."));
        }

        mdToken GetOrCreateTypeReferenceToken(const mdAssemblyRef& assemblyReferenceToken, const xstring_t& className)
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

        mdMemberRef GetOrCreateMemberReferenceToken(const mdToken& typeReferenceOrDefinitionToken, const xstring_t& memberName, const ByteVector& signature)
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
