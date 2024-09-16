// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <string>
#include "Exceptions.h"
#include "../Logging/Logger.h"
#include "Win32Helpers.h"
#include "../MethodRewriter/IFunction.h"
#include "../MethodRewriter/MethodRewriter.h"
#include "CommonDefinitions.h"
#include "CorTokenizer.h"
#include "CorTokenResolver.h"
#include "FunctionHeaderInfo.h"
#include "FunctionPreprocessor.h"
#include "Win32Helpers.h"

namespace NewRelic { namespace Profiler
{
    using NewRelic::Profiler::MethodRewriter::FunctionHeaderInfoPtr;

#ifdef DEBUG
//#define DEBUG_PREPROCESSOR 1 // when enabled the profiler will instrument tons of methods to help identify issues with method manipulation
//#define WRITE_BYTES_TO_DISK 1
#endif

    // Turns a FunctionID into a set of data describing a method.  This class is _*NOT*_ thread safe!
    class Function : public MethodRewriter::IFunction
    {
    private:
        CComPtr<ICorProfilerInfo4> _profilerInfo;
        CComPtr<IMetaDataEmit2> _metaDataEmit;
        CComPtr<IMetaDataImport2> _metaDataImport;
        CComPtr<IMetaDataAssemblyEmit> _metaDataAssemblyEmit;
        CComPtr<IMetaDataAssemblyImport> _metaDataAssemblyImport;

        CorTokenizerPtr _tokenizer;
        CorTokenResolverPtr _tokenResolver;

        FunctionID _functionId;

        xstring_t _moduleName;
        xstring_t _assemblyName;
        xstring_t _appDomainName;
        xstring_t _functionName;
        xstring_t _typeName;

        ModuleID _moduleId; 
        ClassID _classId;
        mdToken _metaDataToken;
        mdTypeDef _typeDefinitionToken;
        DWORD _classAttributes;
        DWORD _methodAttributes;
        FunctionHeaderInfoPtr _functionHeaderInfo;
        bool _shouldTrace;
        bool _valid;
        bool _isCoreClr;
        bool _injectMethodInstrumentation;
        uint32_t _tracerFlags;
        ASSEMBLYMETADATA _assemblyProps;

        ByteVectorPtr _signature;
        ByteVectorPtr _method;
        std::function<HRESULT(Function&, LPCBYTE, ULONG)> _setILFunctionBody;
        std::function<HRESULT(Function&)> _rejitFunction;

    public:

        static void StaticThrowOnError(HRESULT result)
        {
            if (result < 0)
            {
                //LogError("Win32 function call failed.  Function: " #function "  HRESULT: "); 
                throw NewRelic::Profiler::Win32Exception(result);
            }
        }

        // Returns the Function representing the given functionId, or nullptr if this function should not be instrumented.
        static std::shared_ptr<Function> Create(CComPtr<ICorProfilerInfo4> profilerInfo, const FunctionID functionId, std::shared_ptr<MethodRewriter::MethodRewriter> methodRewriter, bool injectMethodInstrumentation, std::function<HRESULT(Function&, LPCBYTE, ULONG)> setILFunctionBodyOrRejit, std::function<HRESULT(Function&)> rejitFunction)
        {
            AssemblyID assemblyId = 0;
            AppDomainID appDomainId = 0;
            ULONG signatureSize = 0;
            const uint8_t* signature = 0;

            ModuleID moduleId;
            ClassID classId;
            mdToken metaDataToken;
            mdTypeDef typeDefinitionToken;

            // get the basic information about this method that we will use to lookup stuff about the method
            StaticThrowOnError(profilerInfo->GetFunctionInfo(functionId, &classId, &moduleId, &metaDataToken));

            // get the assembly id
            StaticThrowOnError(profilerInfo->GetModuleInfo(moduleId, nullptr, 0, nullptr, nullptr, &assemblyId));

            // get the name of the assembly
            ULONG assemblyNameLength = 0;
            StaticThrowOnError(profilerInfo->GetAssemblyInfo(assemblyId, 0, &assemblyNameLength, nullptr, nullptr, nullptr));
            std::unique_ptr<WCHAR[]> assemblyName(new WCHAR[assemblyNameLength]);
            StaticThrowOnError(profilerInfo->GetAssemblyInfo(assemblyId, assemblyNameLength, nullptr, assemblyName.get(), &appDomainId, nullptr));

            CComPtr<IMetaDataImport2> metaDataImport;
            CComPtr<IMetaDataAssemblyImport> metaDataAssemblyImport;

            // get the interfaces we need and the metadata token
            StaticThrowOnError(profilerInfo->GetTokenAndMetaDataFromFunction(functionId, IID_IMetaDataImport2, (IUnknown**)&metaDataImport, nullptr));
            StaticThrowOnError(profilerInfo->GetTokenAndMetaDataFromFunction(functionId, IID_IMetaDataAssemblyImport, (IUnknown**)&metaDataAssemblyImport, nullptr));

            if (metaDataImport == nullptr || metaDataAssemblyImport == nullptr)
            {
                LogError("Unable to get function information for function ID ", functionId);
                throw FailedToGetFunctionInformationException();
            }

            uint32_t tracerFlags = 0;
            bool hasTransactionOrTraceAttribute = false;

            // don't look for trace attributes in Microsoft code or our agent code
            if (!ShouldSkipAssemblyAttributes(assemblyName.get()))
            {
                hasTransactionOrTraceAttribute = HasTransactionOrTraceAttribute(metaDataImport, metaDataToken, tracerFlags);
                if (hasTransactionOrTraceAttribute)
                {
                    tracerFlags |= NewRelic::Profiler::Configuration::TracerFlags::AttributeInstrumentation;
                }
            }
            else
            {
                LogTrace(L"Not searching ", ToStdWString(assemblyName.get()), L" for transaction or trace attributes");
            }


            bool logAll = nrlog::Level::LEVEL_TRACE >= nrlog::StdLog.GetLevel();
            // Normally we bail out of looking up function information as soon as we determine that we have not been
            // asked to instrument a function (shouldInstrument).  We look up the assembly and if it's not in the list 
            // of assemblies to instrument, we exit early.  Otherwise we move forward to the function name and check it, etc.
            // However, we special case the agent api because we always want to instrument it, and if logging is
            // turned up all the way we always look up all function info so that it gets logged at TRACE level.
            // Support uses that logging to help customers create / debug custom instrumentation.

            bool skipShouldInstrumentChecks = logAll || hasTransactionOrTraceAttribute || ToStdWString(assemblyName.get()) == _X("NewRelic.Api.Agent");
#ifdef DEBUG_PREPROCESSOR
            skipShouldInstrumentChecks = true;
#endif

            if (!skipShouldInstrumentChecks && !methodRewriter.get()->ShouldInstrumentAssembly(ToStdWString(assemblyName.get()))) {
                assemblyName.release();
                return nullptr;
            }

            // get the name of the method
            ULONG functionNameLength = 0;
            DWORD methodAttributes;
            StaticThrowOnError(metaDataImport->GetMethodProps(metaDataToken, nullptr, nullptr, 0, &functionNameLength, &methodAttributes, nullptr, nullptr, nullptr, nullptr));
            
            std::unique_ptr<WCHAR[]> functionName(new WCHAR[functionNameLength]);
            StaticThrowOnError(metaDataImport->GetMethodProps(metaDataToken, &typeDefinitionToken, functionName.get(), functionNameLength, nullptr, nullptr, &signature, &signatureSize, nullptr, nullptr));

            if (!skipShouldInstrumentChecks && !methodRewriter.get()->ShouldInstrumentFunction(ToStdWString(functionName.get()))) {
                LogTrace(ToStdWString(functionName.get()), L" is not an instrumented function");
                assemblyName.release();
                functionName.release();
                return nullptr;
            }

            // get the name of the class
            xstring_t typeName = ToStdWString(GetClassNameFromToken(metaDataImport, typeDefinitionToken).get());
            // get the class attributes
            DWORD classAttributes;
            StaticThrowOnError(metaDataImport->GetTypeDefProps(typeDefinitionToken, nullptr, 0, nullptr, &classAttributes, nullptr));

            mdTypeDef parentTypeDefinitionToken = typeDefinitionToken;
            // walk the parent hierarchy until we hit a non-nested class, building the type name along the way
            while (classAttributes & (CorTypeAttr::tdNestedPublic | CorTypeAttr::tdNestedFamily))
            {
                mdTypeDef nestedTypeToken = 0;
                StaticThrowOnError(metaDataImport->GetNestedClassProps(parentTypeDefinitionToken, &nestedTypeToken));

                // get the name of the parent class
                typeName = ToStdWString(GetClassNameFromToken(metaDataImport, nestedTypeToken).get()) + _X("+") + typeName;

                // get the attributes for the parent class, in case it is also nested in which case we loop again
                classAttributes = 0;
                StaticThrowOnError(metaDataImport->GetTypeDefProps(nestedTypeToken, nullptr, 0, nullptr, &classAttributes, nullptr));

                // prep for the next iteration of the loop
                parentTypeDefinitionToken = nestedTypeToken;
            }

            if (!skipShouldInstrumentChecks && !methodRewriter.get()->ShouldInstrumentType(typeName)) {
                LogTrace(typeName, L" is not an instrumented type");

                assemblyName.release();
                functionName.release();
                return nullptr;
            }

            return std::make_shared<Function>(profilerInfo, functionId, metaDataImport, metaDataAssemblyImport, methodRewriter,
                appDomainId, signatureSize, signature, moduleId, classId, metaDataToken, typeDefinitionToken, ToStdWString(assemblyName.get()), 
                typeName, ToStdWString(functionName.get()), classAttributes, methodAttributes, tracerFlags, 
                hasTransactionOrTraceAttribute, injectMethodInstrumentation, setILFunctionBodyOrRejit, rejitFunction);
        }

        // We don't want to search Microsoft assemblies for our trace attributes
        static bool ShouldSkipAssemblyAttributes(xstring_t assemblyName)
        {
            return Strings::StartsWith(assemblyName, _X("System.")) ||
                Strings::StartsWith(assemblyName, _X("Microsoft.")) ||
                Strings::StartsWith(assemblyName, _X("NewRelic."));
        }

        // Check for the API Transaction and Trace attributes.
        static bool HasTransactionOrTraceAttribute(CComPtr<IMetaDataImport2> metaDataImport, mdToken metaDataToken, uint32_t& tracerFlags)
        {
            const BYTE *pVal = NULL;
            ULONG cbVal = 0;

            HRESULT result = metaDataImport->GetCustomAttributeByName(metaDataToken, _X("NewRelic.Api.Agent.TransactionAttribute"), (const void**)&pVal, &cbVal);
            // It is not safe to use the SUCCEEDED() macro to check result in this case. Contrary to the documentation, GetCustomAttributeByName sometimes
            // returns S_FALSE (1), and we don't want to consider that a successful result in this case.
            if (result == S_OK) {
                auto transactionFlag = NewRelic::Profiler::Configuration::TracerFlags::OtherTransaction;
                //  11 huh?   Yeah, whatever dude.  I don't know how to properly deserialize the attribute
                // properties, I just know that the last bit is a 1 or 0 reflecting the boolean "Web" value.
                if (cbVal == 11)
                {
                    bool isWeb = pVal[10] == 1;
                    if (isWeb)
                    {
                        transactionFlag = NewRelic::Profiler::Configuration::TracerFlags::WebTransaction;
                    }
                }

                tracerFlags |= transactionFlag;

                return true;
            }
            result = metaDataImport->GetCustomAttributeByName(metaDataToken, _X("NewRelic.Api.Agent.TraceAttribute"), (const void**)&pVal, &cbVal);
            // Same as above, we can't use SUCCEEDED to check result in this case.
            return result == S_OK;
        }

        Function(
            CComPtr<ICorProfilerInfo4> profilerInfo,
            const FunctionID functionId,
            CComPtr<IMetaDataImport2> metaDataImport,
            CComPtr<IMetaDataAssemblyImport> metaDataAssemblyImport,
            std::shared_ptr<MethodRewriter::MethodRewriter> methodRewriter,
            AppDomainID appDomainId,
            ULONG signatureSize,
            const uint8_t* signature,
            ModuleID moduleId,
            ClassID classId,
            mdToken metaDataToken,
            mdTypeDef typeDefinitionToken,
            xstring_t assemblyName,
            xstring_t typeName,
            xstring_t functionName,
            DWORD classAttributes,
            DWORD methodAttributes,
            uint32_t tracerFlags,
            bool shouldTrace,
            bool injectMethodInstrumentation,
            std::function<HRESULT(Function&, LPCBYTE, ULONG)> setILFunctionBody,
            std::function<HRESULT(Function&)> rejitFunction) :
            _functionId(functionId),
            _functionName(functionName),
            _profilerInfo(profilerInfo),
            _signature(new ByteVector()),
            _method(new ByteVector()),
            _metaDataImport(metaDataImport),
            _metaDataAssemblyImport(metaDataAssemblyImport),
            _moduleId(moduleId),
            _classId(classId),
            _assemblyName(assemblyName),
            _typeName(typeName),
            _metaDataToken(metaDataToken),
            _typeDefinitionToken(typeDefinitionToken),
            _classAttributes(classAttributes),
            _methodAttributes(methodAttributes),
            _shouldTrace(shouldTrace),
            _valid(true),
            _isCoreClr(false),
            _tracerFlags(tracerFlags),
            _injectMethodInstrumentation(injectMethodInstrumentation),
            _setILFunctionBody(setILFunctionBody),
            _rejitFunction(rejitFunction)
        {
            ProcessID processId = 0;
            ULONG methodSize = 0;
            const uint8_t* method;

            // get the interfaces we need and the metadata token
            ThrowOnError(_profilerInfo->GetTokenAndMetaDataFromFunction, functionId, IID_IMetaDataEmit2, (IUnknown**)&_metaDataEmit, nullptr);
            ThrowOnError(_profilerInfo->GetTokenAndMetaDataFromFunction, functionId, IID_IMetaDataAssemblyEmit, (IUnknown**)&_metaDataAssemblyEmit, nullptr);

            if (_metaDataEmit == nullptr || _metaDataAssemblyEmit == nullptr)
            {
                LogError("Unable to get function information for function ID ", functionId);
                throw FailedToGetFunctionInformationException();
            }

            // get the name of the module
            ULONG moduleNameLength = 0;
            ThrowOnError(_profilerInfo->GetModuleInfo, _moduleId, nullptr, 0, &moduleNameLength, nullptr, nullptr);
            std::unique_ptr<WCHAR[]> moduleName(new WCHAR[moduleNameLength]);
            ThrowOnError(_profilerInfo->GetModuleInfo, _moduleId, nullptr, moduleNameLength, nullptr, moduleName.get(), nullptr);

            // get the name of the AppDomain
            ULONG appDomainNameLength = 0;
            ThrowOnError(_profilerInfo->GetAppDomainInfo, appDomainId, 0, &appDomainNameLength, nullptr, nullptr);
            std::unique_ptr<WCHAR[]> appDomainName(new WCHAR[appDomainNameLength]);
            ThrowOnError(_profilerInfo->GetAppDomainInfo, appDomainId, appDomainNameLength, nullptr, appDomainName.get(), &processId);

            _appDomainName = ToStdWString(appDomainName.get());
            _isCoreClr = _appDomainName == _X("clrhost");

            // create the tokenizer that will be used to generate instructions to inject
            _tokenizer = CreateCorTokenizer(_metaDataAssemblyEmit, _metaDataEmit, _metaDataImport, _metaDataAssemblyImport, _isCoreClr);

            // create the token resolver that will be used to get strings from tokens
            _tokenResolver.reset(new CorTokenResolver(_metaDataImport));

            // REVIEW : Why do we get the function bytes upfront before we know that we want to instrument the function?
            // get the bytes that make up this method
            ThrowOnError(_profilerInfo->GetILFunctionBody, _moduleId, _metaDataToken, &method, &methodSize);

            _moduleName = ToStdWString(moduleName.get());
            _method->assign(method, method + methodSize);
            _signature->assign(signature, signature + signatureSize);

            _functionHeaderInfo = CreateFunctionHeaderInfo(_method);

            const BYTE *pVal = NULL;
            ULONG cbVal = 0;

            HRESULT attributeResult = _metaDataImport->GetCustomAttributeByName(_metaDataToken, _X("System.Runtime.CompilerServices.AsyncStateMachineAttribute"), (const void**)&pVal, &cbVal);
            // It is not safe for us to use the SUCCEEDED macro on the result returned from GetCustomAttributeByName
            if (attributeResult == S_OK)
            {
                LogDebug(L"Async method detected: ", this->ToString());
                _tracerFlags |= NewRelic::Profiler::Configuration::TracerFlags::AsyncMethod;
            }

            mdAssembly mda = 0;
            ThrowOnError(_metaDataAssemblyImport->GetAssemblyFromScope, &mda);

            _assemblyProps = ASSEMBLYMETADATA();
            ThrowOnError(_metaDataAssemblyImport->GetAssemblyProps, mda, 0, 0, 0, nullptr, 0, nullptr, &_assemblyProps, 0);

#ifdef DEBUG_PREPROCESSOR
            auto isMsCorLib = assemblyName == _X("mscorlib");
            if (!isMsCorLib && 
                assemblyName != _X("System") &&
                !IsMdHasSecurity(methodAttributes) &&
                !typeName.empty() &&
                !IsMdSpecialName(methodAttributes) &&
                methodRewriter->ShouldInstrumentAssembly(assemblyName))
            {
                _shouldTrace = true;
            }
#endif
        }

        virtual bool Preprocess() override
        {
            if (_functionHeaderInfo == nullptr) {
                return false;
            }

            auto preprocessor = new NewRelic::Profiler::MethodRewriter::FunctionPreprocessor(_functionHeaderInfo, _method);
            auto newFunction = preprocessor->Process();

#ifdef WRITE_BYTES_TO_DISK
            if (newFunction != nullptr) {
                ofstream myfile;
                auto fileName = GetTypeName() + _X(".") + GetFunctionName() + _X(".bin");
                myfile.open(fileName.c_str(), ios::out | ios::app | ios::binary);
                myfile.write((const char *)newFunction->data(), newFunction->size());
                myfile.close();
            }
#endif // WRITE_BYTES_TO_DISK
            _valid = newFunction != nullptr;
            if (_valid)
            {
                _method = newFunction;
            }
            return _valid;
        }

        virtual uint32_t GetTracerFlags() override
        {
            return _tracerFlags;
        }

        virtual bool IsValid() override
        {
            return _valid;
        }

        virtual bool IsCoreClr() override
        {
            return _isCoreClr;
        }

        virtual bool ShouldTrace() override
        {
            return _shouldTrace;
        }

        virtual ASSEMBLYMETADATA GetAssemblyProps() override
        {
            return _assemblyProps;
        }

        virtual bool ShouldInjectMethodInstrumentation() override
        {
            if (!_injectMethodInstrumentation)
            {
                if (FAILED(_rejitFunction(*this)))
                {
                    LogError(L"ReJIT failed for ", ToString());
                }
            }
            return !_injectMethodInstrumentation;
        }

        virtual uintptr_t GetFunctionId() override
        {
            return _functionId;
        }

        ModuleID GetModuleID()
        {
            return _moduleId;
        }

        virtual xstring_t GetModuleName() override
        {
            return _moduleName;
        }

        virtual xstring_t GetAssemblyName() override
        {
            return _assemblyName;
        }

        virtual xstring_t GetAppDomainName() override
        {
            return _appDomainName;
        }

        virtual xstring_t GetTypeName() override
        {
            return _typeName;
        }

        virtual xstring_t GetFunctionName() override
        {
            return _functionName;
        }

        virtual mdToken GetMethodToken() override
        {
            return _metaDataToken;
        }

        virtual mdTypeDef GetTypeToken() override
        {
            return _typeDefinitionToken;
        }

        virtual DWORD GetClassAttributes() override
        {
            return _classAttributes;
        }

        virtual DWORD GetMethodAttributes() override
        {
            return _methodAttributes;
        }

        // get the signature for this method
        virtual ByteVectorPtr GetSignature() override
        {
            return _signature;
        }

        // returns the bytes that make up this method, this includes the header and the code
        virtual ByteVectorPtr GetMethodBytes() override
        {
            return _method;
        }

        virtual FunctionHeaderInfoPtr GetFunctionHeaderInfo() override
        {
            return _functionHeaderInfo;
        }

        // get the tokenizer that should be used to modify the code bytes
        virtual sicily::codegen::ITokenizerPtr GetTokenizer() override
        {
            return _tokenizer;
        }

        virtual SignatureParser::ITokenResolverPtr GetTokenResolver() override
        {
            return _tokenResolver;
        }

        // writes the method to be JIT compiled, method consists of header bytes and code bytes
        virtual void WriteMethod(const ByteVector& method) override
        {
            // allocate some space for our new method
            IMethodMalloc* methodAllocator;
            ThrowOnError(_profilerInfo->GetILFunctionBodyAllocator, _moduleId, &methodAllocator);
            uint8_t* allocatedSpace = (uint8_t*)methodAllocator->Alloc(ULONG(method.size()));

            // fill the bytes into the allocated space
            memcpy(allocatedSpace, method.data(), method.size());

            // set the function to use the new bytes as its bytes to JIT compile
            ThrowOnError(_setILFunctionBody, *this, allocatedSpace, (ULONG)method.size());
        }

        virtual xstring_t ToString() override
        {
            auto signature = GetSignature();
            auto parsedSignature = SignatureParser::SignatureParser::ParseMethodSignature(signature->begin(), signature->end());
            auto signatureString = parsedSignature->ToString(GetTokenResolver());
            
            return xstring_t(_X("(Module: ")) + _moduleName + _X(", AppDomain: ") + _appDomainName + _X(")[") + _assemblyName + _X("]") + _typeName + _X(".") + _functionName + _X("(") + signatureString + _X(")");
        }

        virtual ByteVectorPtr GetSignatureFromToken(mdToken token) override
        {
            ULONG signatureLength;
            uint8_t* signature;
            ThrowOnError(_metaDataImport->GetSigFromToken, token, (PCCOR_SIGNATURE*)&signature, &signatureLength);
            return std::make_shared<ByteVector>(signature, signature + signatureLength);
        }

        virtual mdToken GetTokenFromSignature(const ByteVector& signature) override
        {
            mdToken signatureToken = 0;
            ThrowOnError(_metaDataEmit->GetTokenFromSig, signature.data(), ULONG(signature.size()), &signatureToken);
            return signatureToken;
        }

        virtual bool IsGenericType() override
        {
            return(_classId == 0);
        }
    
        static std::unique_ptr<WCHAR[]> GetClassNameFromToken(CComPtr<IMetaDataImport2> metaDataImport, mdTypeDef typeDefinitionToken)
        {
            ULONG typeNameLength = 0;
            ThrowOnError(metaDataImport->GetTypeDefProps, typeDefinitionToken, nullptr, 0, &typeNameLength, nullptr, nullptr);
            std::unique_ptr<WCHAR[]> typeName(new WCHAR[typeNameLength]);
            ThrowOnError(metaDataImport->GetTypeDefProps, typeDefinitionToken, typeName.get(), typeNameLength, nullptr, nullptr, nullptr);
            return typeName;
        }

    };
}}
