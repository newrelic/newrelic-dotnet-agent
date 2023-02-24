// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <sstream>
#include <cstring>
#include <cctype>

#include "Scanner.h"

#include "Exceptions.h"

namespace sicily {
    Scanner::Scanner(const xstring_t& data)
        : data_(nullptr), datalen_(data.size()), pos_(0)
    {
        data_ = new xchar_t[data.size() + 1];
        std::copy (data.begin(), data.end(), data_);
    }

    Scanner::~Scanner()
    {
        delete[] data_;
    }

    TokenType
    Scanner::Peek(SemInfo &sem)
    {
        if (tokenbuf_.empty()) {
            TokenType type = Next(sem);
            Unget(type, sem);
            return type;
        }
        else {
            token t = tokenbuf_.back();
            sem = t.second;
            return t.first;
        }
    }

    void
    Scanner::Skip()
    {
        SemInfo sem;
        Next(sem);
    }

    bool
    Scanner::Maybe(TokenType type, SemInfo &sem)
    {
        TokenType actual = Next(sem);
        if (actual == type) {
            return true;
        }
        Unget(actual, sem);
        return false;
    }

    bool
    Scanner::Maybe(TokenType type)
    {
        SemInfo sem;
        return Maybe(type, sem);
    }

    void
    Scanner::Expect(TokenType type, SemInfo& sem)
    {
        TokenType actual = Next(sem);
        if (actual != type) {
            throw UnexpectedTokenException(type, actual);
        }
    }

    void
    Scanner::Expect(TokenType type)
    {
        SemInfo sem;
        Expect(type, sem);
    }

    TokenType
    Scanner::Next(SemInfo &sem)
    {
        if (tokenbuf_.size() > 0) {
            token t = tokenbuf_.back();
            tokenbuf_.pop_back();
            sem = t.second;
            return t.first;
        }

        for (;;) {
            if (pos_ >= datalen_) {
                return TOK_END;
            }

            switch (data_[pos_]) {
                case L'[':
                    {
                        pos_++;
                        return TOK_LSQBRACKET;
                    }
                case L']':
                    {
                        pos_++;
                        return TOK_RSQBRACKET;
                    }
                case L'(':
                    {
                        pos_++;
                        return TOK_LBRACKET;
                    }
                case L')':
                    {
                        pos_++;
                        return TOK_RBRACKET;
                    }
                case L':':
                    {
                        pos_++;
                        if (pos_ >= datalen_) {
                            throw UnexpectedEndOfStreamException();
                        }
                        else if (data_[pos_] != L':') {
                            throw UnexpectedCharacterException(L':', data_[pos_]);
                        }
                        else {
                            pos_++;
                            return TOK_DOUBLECOLON;
                        }
                    }
                case L'!':
                    {
                        pos_++;
                        if (pos_ >= datalen_ || data_[pos_] != L'!') {
                            return TOK_BANG;
                        }
                        else {
                            pos_++;
                            return TOK_DOUBLEBANG;
                        }
                    }
                case L'.':
                    {
                        pos_++;
                        return TOK_FULLSTOP;
                    }
                case L'<':
                    {
                        pos_++;
                        return TOK_LT;
                    }
                case L'>':
                    {
                        pos_++;
                        return TOK_GT;
                    }
                case L',':
                    {
                        pos_++;
                        return TOK_COMMA;
                    }
                case L'`':
                    {
                        pos_++;
                        return TOK_BACKTICK;
                    }
                case L'/':
                    {
                        pos_++;
                        return TOK_SLASH;
                    }
                case L'&':
                    {
                        pos_++;
                        return TOK_AMPERSAND;
                    }
                case L' ':
                case L'\t':
                case L'\r':
                case L'\n':
                    {
                        pos_++;
                        break;
                    }
                default:
                    if (std::isalpha(data_[pos_]) || data_[pos_] == L'_' || data_[pos_] == L'.') {
                        return ScanIdent(sem);
                    }
                    else if (std::isdigit(data_[pos_])) {
                        return ScanNum(sem);
                    }
                    else {
                        throw UnhandledCharacterException(data_[pos_]);
                    }
            };
        }
    }

    void
    Scanner::Unget(TokenType type, const SemInfo &sem)
    {
        tokenbuf_.push_back(token(type, sem));
    }

    TokenType
    Scanner::ScanIdent(SemInfo &sem)
    {
        size_t begin = pos_;
        while (pos_ < datalen_ &&
                (std::isalnum(data_[pos_]) ||
                 data_[pos_] == L'_' ||
                 data_[pos_] == L'.')) {
            pos_++;
        }
        sem.id_ = xstring_t(data_ + begin, pos_ - begin);
        if (sem.id_ == _X("instance")) {
            return TOK_INSTANCE;
        }
        else if (sem.id_ == _X("class")) {
            return TOK_CLASS;
        }
        else if (sem.id_ == _X("valuetype")) {
            return TOK_VALUETYPE;
        }
        else if (sem.id_ == _X("object")) {
            return TOK_OBJECT;
        }
        else if (sem.id_ == _X("void")) {
            return TOK_VOID;
        }
        else if (sem.id_ == _X("bool")) {
            return TOK_BOOL;
        }
        else if (sem.id_ == _X("string")) {
            return TOK_STRING;
        }
        else if (sem.id_ == _X("uint32")) {
            return TOK_UINT32;
        }
        else if (sem.id_ == _X("int32")) {
            return TOK_INT32;
        }
        else {
            return TOK_ID;
        }
    }

    TokenType
    Scanner::ScanNum(SemInfo &sem)
    {
        int value = 0;
        while (pos_ < datalen_ && std::isdigit(data_[pos_])) {
            value *= 10;
            value += (int)(data_[pos_] - (char)0x30);
            pos_++;
        }
        sem.num_ = value;
        return TOK_NUM;
    }

};

