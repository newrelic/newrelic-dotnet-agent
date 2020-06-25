/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <string>
#include "Exceptions.h"
#include "Scanner.h"
#include "ast/Types.h"

namespace sicily {
    class Parser
    {
        public:
            Parser();
            ~Parser();

            ast::TypePtr Parse(Scanner &scanner);

        private:
            ast::TypePtr ParseMethodSignature(Scanner &scanner, bool instanceMethod, bool methodRequired);
            ast::TypePtr ParseTypeSignature(Scanner &scanner, bool allowRawClassName=false);
            ast::ClassTypePtr ParseClassTypeSignature(Scanner &scanner, bool isRaw=false, ast::ClassType::ClassKind classKind=ast::ClassType::ClassKind::CLASS);
            ast::TypeListPtr ParseTypeList(Scanner &scanner);
            // throws an exception if a qualified name is not found
            void ParseQualifiedName(Scanner &scanner, xstring_t &name);
    };

    struct UnexpectedEndTokenException : ParserException {};
    struct ExpectedTypeDescriptorException : ParserException
    {
        ExpectedTypeDescriptorException(xstring_t found) : found_(found) {}
        xstring_t found_;
    };
    struct UnhandledTokenException : ParserException
    {
        UnhandledTokenException(TokenType token) : token_(token) {}
        TokenType token_;
    };
    struct GenericArgumentCountMismatchException : ParserException
    {
        GenericArgumentCountMismatchException(size_t expected, size_t found) : expected_(expected), found_(found) {}
        size_t expected_;
        size_t found_;
    };
    struct UnexpectedTypeKindException : ParserException
    {
        UnexpectedTypeKindException(ast::Type::Kind expected, ast::Type::Kind found) : expected_(expected), found_(found) {}
        ast::Type::Kind expected_;
        ast::Type::Kind found_;
    };
};
