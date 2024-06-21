// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <cstdlib>
#include <sstream>

#include "Parser.h"

#include "Exceptions.h"

namespace sicily {
    Parser::Parser()
    {
    }

    Parser::~Parser()
    {
    }

    ast::TypePtr
    Parser::Parse(Scanner &scanner)
    {
        TokenType t;
        SemInfo sem;

        t = scanner.Peek(sem);

        switch (t) {
            case TOK_INSTANCE:
                {
                    scanner.Skip();
                    ast::TypePtr type = ParseMethodSignature(scanner, true, true);
                    scanner.Expect(TOK_END);
                    return type;
                }
            case TOK_END:
                {
                    throw UnexpectedEndTokenException();
                }
            case TOK_LSQBRACKET:
                {
                    // this is a bit hacky, we should probably hint to the parser whether it is looking for a method or a type
                    ast::TypePtr type = ParseClassTypeSignature(scanner, true, ast::ClassType::ClassKind::CLASS);
                    scanner.Expect(TOK_END);
                    return type;
                }
            default:
                ast::TypePtr type = ParseMethodSignature(scanner, false, false);
                scanner.Expect(TOK_END);
                return type;
        };
    }

    ast::TypePtr
    Parser::ParseMethodSignature(Scanner &scanner, bool instanceMethod, bool methodRequired)
    {
        ast::TypeListPtr argTypes = nullptr;
        ast::TypeListPtr genericTypes = nullptr;
        ast::TypePtr returnType = nullptr;
        ast::ClassTypePtr targetType = nullptr;
        ast::TypePtr requiredModifierType = nullptr;
        SemInfo sem;
        xstring_t name;

        returnType = ParseTypeSignature(scanner);

        if (scanner.Peek(sem) == TOK_END) {
            if (methodRequired) {
                throw UnexpectedEndTokenException();
            }
            return returnType;
        }

        if (scanner.Maybe(TOK_MODREQ))
        {
            scanner.Expect(TOK_LBRACKET);
            requiredModifierType = ParseTypeSignature(scanner, true);
            scanner.Expect(TOK_RBRACKET);
        }

        auto tempTargetType = ParseTypeSignature(scanner, true);
        if (tempTargetType->GetKind() != ast::Type::Kind::kCLASS && tempTargetType->GetKind() != ast::Type::Kind::kGENERICCLASS)
        {
            throw UnexpectedTypeKindException(tempTargetType->GetKind(), ast::Type::Kind::kCLASS);
        }
        targetType = std::dynamic_pointer_cast<ast::ClassType, ast::Type>(tempTargetType);

        scanner.Expect(TOK_DOUBLECOLON);

        //
        // FULLSTOP to handle `.ctor` etc.
        //
        if (scanner.Maybe(TOK_FULLSTOP)) {
            name += _X(".");
        }
        scanner.Expect(TOK_ID, sem);
        name += sem.id_;

        if (scanner.Maybe(TOK_LT)) {
            genericTypes = ParseTypeList(scanner);
            scanner.Expect(TOK_GT);
        }

        if (scanner.Peek(sem) == TOK_END) {
            return std::make_shared<ast::FieldType>(ast::FieldType(targetType, name, returnType, requiredModifierType));
        }

        scanner.Expect(TOK_LBRACKET);
        if (!scanner.Maybe(TOK_RBRACKET)) {
            argTypes = ParseTypeList(scanner);
            scanner.Expect(TOK_RBRACKET);
        }

        if (argTypes == nullptr) argTypes = std::make_shared<ast::TypeList>();
        if (genericTypes == nullptr) genericTypes = std::make_shared<ast::TypeList>();

        return std::make_shared<ast::MethodType>(ast::MethodType(targetType, name, returnType, instanceMethod, argTypes, genericTypes));
    }

    ast::TypePtr
    Parser::ParseTypeSignature(Scanner &scanner, bool allowRawClassName)
    {
        SemInfo sem;
        TokenType type = scanner.Next(sem);
        bool byRef = scanner.Maybe(TOK_AMPERSAND);
        ast::TypePtr result;

        switch (type) {
            case TOK_OBJECT:
                result = std::make_shared<ast::PrimitiveType>(ast::PrimitiveType::PrimitiveKind::kOBJECT, byRef);
                break;
            case TOK_VOID:
                result = std::make_shared<ast::PrimitiveType>(ast::PrimitiveType::PrimitiveKind::kVOID, byRef);
                break;
            case TOK_BOOL:
                result = std::make_shared<ast::PrimitiveType>(ast::PrimitiveType::PrimitiveKind::kBOOL, byRef);
                break;
            case TOK_UINT32:
                result = std::make_shared<ast::PrimitiveType>(ast::PrimitiveType::PrimitiveKind::kU4, byRef);
                break;
            case TOK_INT32:
                result = std::make_shared<ast::PrimitiveType>(ast::PrimitiveType::PrimitiveKind::kI4, byRef);
                break;
            case TOK_UINT64:
                result = std::make_shared<ast::PrimitiveType>(ast::PrimitiveType::PrimitiveKind::kU8, byRef);
                break;
            case TOK_STRING:
                result = std::make_shared<ast::PrimitiveType>(ast::PrimitiveType::PrimitiveKind::kSTRING, byRef);
                break;
            case TOK_CLASS:
                result = ParseClassTypeSignature(scanner);
                break;
            case TOK_VALUETYPE:
                result = ParseClassTypeSignature(scanner, false, ast::ClassType::ClassKind::VALUETYPE);
                break;
            case TOK_BANG:
                scanner.Expect(TOK_NUM, sem);
                result = std::make_shared<ast::GenericParamType>(ast::GenericParamType::GenericParamKind::kTYPE, sem.num_);
                break;
            case TOK_DOUBLEBANG:
                scanner.Expect(TOK_NUM, sem);
                result = std::make_shared<ast::GenericParamType>(ast::GenericParamType::GenericParamKind::kMETHOD, sem.num_);
                break;
            case TOK_LSQBRACKET:
            case TOK_ID:
                if (allowRawClassName) {
                    //
                    // raw class name
                    //
                    scanner.Unget(type, sem);
                    result = ParseClassTypeSignature(scanner, true);
                }
                else {
                    throw ExpectedTypeDescriptorException(sem.id_);
                }
                break;
            default:
                throw UnhandledTokenException(type);
        };

        while (scanner.Maybe(TOK_LSQBRACKET)) {
            //
            // `void [mscorlib]System.Bar` is incorrectly parsed
            // as `void[...` -> scanner needs to be a little smarter.
            //
            // Hack around the suckiness.
            //
            if (scanner.Maybe(TOK_ID, sem)) {
                scanner.Unget(TOK_ID, sem);
                scanner.Unget(TOK_LSQBRACKET, sem);
                break;
            }
            scanner.Expect(TOK_RSQBRACKET);
            result = std::make_shared<ast::ArrayType>(result);
        }

        return result;
    }

    ast::ClassTypePtr
    Parser::ParseClassTypeSignature(Scanner &scanner, bool isRaw, ast::ClassType::ClassKind classKind)
    {
        xstring_t assembly;
        xstring_t name;
        ast::TypeListPtr genericTypes = nullptr;

        if (scanner.Maybe(TOK_LSQBRACKET)) {
            ParseQualifiedName(scanner, assembly);

            scanner.Expect(TOK_RSQBRACKET);
        }

        ParseQualifiedName(scanner, name);

        while (scanner.Maybe(TOK_SLASH)) {
            xstring_t nameExtra;
            ParseQualifiedName(scanner, nameExtra);
            name += _X("/") + nameExtra;
        }

        if (scanner.Maybe(TOK_BACKTICK)) {
            SemInfo sem;
            scanner.Expect(TOK_NUM, sem);
            size_t nargs = sem.num_;
            scanner.Expect(TOK_LT);
            genericTypes = ParseTypeList(scanner);
            scanner.Expect(TOK_GT);
            if (genericTypes->GetSize() != nargs) {
                throw GenericArgumentCountMismatchException(nargs, genericTypes->GetSize());
            }
        }

        if (genericTypes == nullptr) return std::make_shared<ast::ClassType>(name, assembly, isRaw, classKind);
        else return std::make_shared<ast::GenericType>(name, assembly, genericTypes, isRaw, classKind);
    }

    ast::TypeListPtr
    Parser::ParseTypeList(Scanner &scanner)
    {
        auto types = std::make_shared<ast::TypeList>();
        for (;;) {
            ast::TypePtr type = ParseTypeSignature(scanner);
            types->Add(type);

            if (!scanner.Maybe(TOK_COMMA)) {
                break;
            }
        }
        return types;
    }

    void
    Parser::ParseQualifiedName(Scanner &scanner, xstring_t &name)
    {
        sicily::SemInfo sem;
        name = xstring_t();

        for (;;) {
            scanner.Expect(TOK_ID, sem);

            name += sem.id_.c_str();

            if (!scanner.Maybe(TOK_FULLSTOP)) {
                break;
            }

            name += _X(".");
        }
    }
};
