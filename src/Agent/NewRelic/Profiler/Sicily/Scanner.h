/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <string>
#include <iostream>
#include <list>
#include "Exceptions.h"

namespace sicily {
    enum TokenType {
        TOK_ERROR = -2,
        TOK_END   = -1,
        TOK_LSQBRACKET = 0,
        TOK_RSQBRACKET,
        TOK_LBRACKET,
        TOK_RBRACKET,
        TOK_DOUBLECOLON,
        TOK_BANG,
        TOK_DOUBLEBANG,
        TOK_FULLSTOP,
        TOK_LT,
        TOK_GT,
        TOK_BACKTICK,
        TOK_SLASH,
        TOK_COMMA,
        TOK_ID,
        TOK_NUM,
        TOK_AMPERSAND,

        TOK_INSTANCE,
        TOK_CLASS,
        TOK_VALUETYPE,
        TOK_OBJECT,
        TOK_STRING,
        TOK_VOID,
        TOK_BOOL,
        TOK_UINT32,
        TOK_INT32,
        TOK_UINT64
    };

    struct SemInfo {
        xstring_t id_;
        int         num_;
        struct {
            size_t pos_;
            xstring_t message_;
        } error_;
    };

    class Scanner
    {
        public:
            Scanner(const xstring_t& data);
            ~Scanner();

            void Skip();

            bool Maybe(TokenType type);
            bool Maybe(TokenType type, SemInfo &sem);

            // throws an exception if the next token isn't the expected one
            void Expect(TokenType type);
            void Expect(TokenType type, SemInfo& sem);

            TokenType Peek(SemInfo &sem);
            TokenType Next(SemInfo &sem);
            void Unget(TokenType type, const SemInfo &sem);

        private:
            TokenType ScanIdent(SemInfo &sem);
            TokenType ScanNum(SemInfo &sem);

            typedef std::pair<TokenType, SemInfo> token;
            typedef std::list<token> tokenbuf;

            tokenbuf tokenbuf_;
            xchar_t *data_;
            size_t datalen_;
            size_t pos_;
    };

    struct UnexpectedEndOfStreamException : ScannerException {};
    struct UnexpectedCharacterException : ScannerException
    {
        UnexpectedCharacterException(xchar_t expected, xchar_t found) : expected_(expected), found_(found) {}
        xchar_t expected_;
        xchar_t found_;
    };
    struct UnhandledCharacterException : ScannerException
    {
        UnhandledCharacterException(xchar_t found) : found_(found) {}
        xchar_t found_;
    };
    struct UnexpectedTokenException : ScannerException
    {
        UnexpectedTokenException(TokenType expected, TokenType found) : expected_(expected), found_(found) {}
        TokenType expected_;
        TokenType found_;
    };
};
