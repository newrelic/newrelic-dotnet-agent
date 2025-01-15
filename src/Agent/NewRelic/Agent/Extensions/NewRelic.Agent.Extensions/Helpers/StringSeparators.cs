// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Helpers
{
    public static class StringSeparators
    {
        public const char PathSeparatorChar = '/';
        public static readonly char[] PathSeparator = { PathSeparatorChar };

        public const char BackslashChar = '\\';
        public static readonly char[] Backslash = { BackslashChar };

        public const char CommaChar = ',';
        public static readonly char[] Comma = { CommaChar };

        public const char PeriodChar = '.';
        public static readonly char[] Period = { PeriodChar };

        public const char ColonChar = ':';
        public static readonly char[] Colon = { ColonChar };

        public const char HashChar = '#';
        public static readonly char[] Hash = { HashChar };

        public const char BackTickChar = '`';
        public static readonly char[] BackTick = { BackTickChar };

        public const char SemiColonChar = ';';
        public static readonly char[] SemiColon = { SemiColonChar };

        public const char QuestionMarkChar = '?';
        public static readonly char[] QuestionMark = { QuestionMarkChar };

        public const char AtSignChar = '@';
        public static readonly char[] AtSign = { AtSignChar };

        public const char EqualSignChar = '=';
        public static readonly char[] EqualSign = { EqualSignChar };

        public const char OpenParenChar = '(';
        public static readonly char[] OpenParen = { OpenParenChar };

        public const char CloseParenChar = ')';
        public static readonly char[] CloseParen = { CloseParenChar };

        public static readonly string[] StringNewLine = { System.Environment.NewLine };
    }
}
