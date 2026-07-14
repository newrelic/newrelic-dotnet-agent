/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <tuple>
#include <string>
#include <vector>
#include <stdint.h>
#include <memory>
#include "../codegen/ITokenizer.h"
#include "../Exceptions.h"

namespace sicily
{
    namespace codegen
    {
        class RealisticTokenizer : public ITokenizer
        {
        public:
            RealisticTokenizer() {}
            virtual ~RealisticTokenizer() {}

            virtual uint32_t GetAssemblyRefToken(const std::wstring& assemblyName) override
            {
                AssemblyRefType assemblyRef(assemblyName);
                return GetToken(assemblyRef, assemblyRefs, 0x23);
            }

            virtual uint32_t GetTypeRefToken(const std::wstring& assemblyName, const std::wstring& fullyQualifiedName) override
            {
                auto lastDotPosition = fullyQualifiedName.find_last_of('.');
                auto namespaceName = fullyQualifiedName.substr(0, lastDotPosition);
                auto typeName = fullyQualifiedName.substr(lastDotPosition + 1, fullyQualifiedName.npos - (lastDotPosition + 1));

                return GetTypeRefToken(assemblyName, typeName, namespaceName);
            }

            virtual uint32_t GetTypeRefToken(const std::wstring& assemblyName, const std::wstring& name, const std::wstring& namespaceName) override
            {
                auto resolutionScope = GetAssemblyRefToken(assemblyName);
                std::wstring fullyQualifiedName(namespaceName + L"." + name);
                TypeRefType typeRef(resolutionScope, name, namespaceName);
                return GetToken(typeRef, typeRefs, 0x01);
            }

            virtual uint32_t GetTypeDefToken(const std::wstring& fullName) override
            {
                auto lastDotPosition = fullName.find_last_of('.');
                auto namespaceName = fullName.substr(0, lastDotPosition);
                auto typeName = fullName.substr(lastDotPosition + 1, fullName.npos - (lastDotPosition + 1));

                TypeDefType typeDef(typeName, namespaceName);
                return GetToken(typeDef, typeDefs, 0x02);
            }

            virtual uint32_t GetTypeSpecToken(const ByteVector& instantiationSignature) override
            {
                TypeSpecType typeSpec(instantiationSignature);
                return GetToken(typeSpec, typeSpecs, 0x1b);
            }

            virtual uint32_t GetMemberRefOrDefToken(uint32_t parent, const std::wstring& methodName, const ByteVector& signature) override
            {
                MemberRefType memberRef(parent, methodName, signature);
                return GetToken(memberRef, memberRefs, 0x0a);
            }

            virtual uint32_t GetMethodDefinitionToken(const uint32_t& typeDefinitionToken, const std::wstring& name, const ByteVector& signature) override
            {
                MethodDefType methodDef(typeDefinitionToken, name, signature);
                return GetToken(methodDef, methodDefs, 0x06);
            }

            virtual uint32_t GetFieldDefinitionToken(const uint32_t& typeDefinitionToken, const std::wstring& name) override
            {
                FieldDefType fieldDef(typeDefinitionToken, name);
                return GetToken(fieldDef, fieldDefs, 0x05);
            }

            virtual uint32_t GetMethodSpecToken(uint32_t methodDefOrRefOrSpecToken, const ByteVector& instantiationSignature) override
            {
                MethodSpecType methodSpec(methodDefOrRefOrSpecToken, instantiationSignature);
                return GetToken(methodSpec, methodSpecs, 0x2b);
            }

            virtual uint32_t GetStringToken(const std::wstring& string) override
            {
                StringType stringObject(string);
                return GetToken(stringObject, strings, 0x70);
            }

        private:
            // vector of (<assembly name>), element position is its AssemblyRef token
            typedef std::tuple<std::wstring> AssemblyRefType;
            std::vector<AssemblyRefType> assemblyRefs;
            // vector of (<AssemblyRef token> & <type name> & <type namespace>), element position is its TypeRef token
            typedef std::tuple<uint32_t, std::wstring, std::wstring> TypeRefType;
            std::vector<TypeRefType> typeRefs;
            // vector of (<type name> & <type namespace>), element position is its TypeDef token
            typedef std::tuple<std::wstring, std::wstring> TypeDefType;
            std::vector<TypeDefType> typeDefs;
            // vector of (<type instantiation signature>) to TypeSpec token
            typedef std::tuple<ByteVector> TypeSpecType;
            std::vector<TypeSpecType> typeSpecs;
            // vector of (<typeDef token> & <method name> & <method signature>), element position is its MethodDef token
            typedef std::tuple<uint32_t, std::wstring, ByteVector> MethodDefType;
            std::vector<MethodDefType> methodDefs;
            //vector of (<typeDef token> & <field name>), element postion is its FieldDef token
            typedef std::tuple<uint32_t, std::wstring> FieldDefType;
            std::vector<FieldDefType> fieldDefs;
            // vector of (<MethodRef token> & <method instantiation signature>), element position is its MethodSpec token
            typedef std::tuple<uint32_t, ByteVector> MethodSpecType;
            std::vector<MethodSpecType> methodSpecs;
            // vector of (<TypeRef token> & <method name> & <method signature>), element position is its MemberRef token
            typedef std::tuple<uint32_t, std::wstring, ByteVector> MemberRefType;
            std::vector<MemberRefType> memberRefs;
            // vector of (<string>), element position is its location in the string table
            typedef std::tuple<std::wstring> StringType;
            std::vector<StringType> strings;

        public:
            virtual AssemblyRefType GetAssemblyRef(uint32_t assemblyRefToken)
            {
                return GetType(assemblyRefToken, assemblyRefs, 0x23, L"AssemblyRef");
            }

            virtual TypeRefType GetTypeRef(uint32_t typeRefToken)
            {
                return GetType(typeRefToken, typeRefs, 0x01, L"TypeRef");
            }

            virtual TypeSpecType GetTypeSpec(uint32_t typeSpecToken)
            {
                return GetType(typeSpecToken, typeSpecs, 0x1b, L"TypeSpec");
            }

            virtual MethodDefType GetMethodDef(uint32_t methodDefToken)
            {
                return GetType(methodDefToken, methodDefs, 0x06, L"MethodDef");
            }

            virtual MethodSpecType GetMethodSpec(uint32_t methodSpecToken)
            {
                return GetType(methodSpecToken, methodSpecs, 0x2b, L"MethodSpec");
            }

            virtual MemberRefType GetMemberRef(uint32_t memberRefToken)
            {
                return GetType(memberRefToken, memberRefs, 0x0a, L"MemberRef");
            }

            virtual FieldDefType GetFieldDef(uint32_t fieldDefToken)
            {
                return GetType(fieldDefToken, fieldDefs, 0x05, L"FieldDef");
            }

        private:
            template <typename T> uint32_t GetToken(const T& t, std::vector<T>& v, uint8_t tableNumber)
            {
                uint32_t result = 0;

                for (uint32_t i = 0; i < v.size(); ++i)
                {
                    if (t == v[i]) result = i;
                }
            
                if (result == 0)
                {
                    result = uint32_t(v.size());
                    v.push_back(t);
                }

                result += 1;
                result |= (tableNumber << 24);

                return result;
            }

            template <typename T> T GetType(uint32_t token, const std::vector<T>& table, uint32_t expectedTableNumber, std::wstring T_name)
            {
                auto tableNumber = ((token & 0xff000000) >> 24);
                if (tableNumber != expectedTableNumber)
                {
                    std::wostringstream stream;
                    stream << T_name << "Token with invalid table number.  Expected " << std::hex << expectedTableNumber << " but found " << std::hex << tableNumber;
                    throw MessageException(stream.str());
                }

                auto index = (token & 0x00ffffff) - 1;
                return table[index];
            }
        };

        typedef std::shared_ptr<RealisticTokenizer> RealisticTokenizerPtr;
    }
}
