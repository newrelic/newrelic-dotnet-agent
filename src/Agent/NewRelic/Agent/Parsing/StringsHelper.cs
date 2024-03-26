// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using NewRelic.Agent.Helpers;

namespace NewRelic.Parsing
{
    public static class StringsHelper
    {
        public static string CleanUri(string uri)
        {
            if (uri == null)
                return string.Empty;

            var index = uri.IndexOf('?');
            return (index > 0)
                ? uri.Substring(0, index)
                : uri;
        }

        public static string CleanUri(Uri uri)
        {
            if (uri == null)
                return string.Empty;

            // Can't clean up relative URIs (Uri.GetComponents will throw an exception for relative URIs)
            if (!uri.IsAbsoluteUri)
                return CleanUri(uri.ToString());

            try
            {
                return uri.GetComponents(
                        UriComponents.Scheme |
                        UriComponents.HostAndPort |
                        UriComponents.Path,
                        UriFormat.UriEscaped);
            }
            catch (InvalidOperationException) // can throw in .NET 6+ if the uri was created with UriCreationOptions.DangerousDisablePathAndQueryCanonicalization = true
            {
                return CleanUri(uri.ToString());
            }
        }

        public static string FixDatabaseObjectName(string s)
        {
            int index = s.IndexOf('.');
            if (index > 0)
            {
                return new StringBuilder(s.Length)
                    .Append(RemoveBookendsAndLower(s.Substring(0, index)))
                    .Append('.')
                    .Append(FixDatabaseName(s.Substring(index + 1)))
                    .ToString();
            }
            else
            {
                return RemoveBookendsAndLower(s);
            }
        }

        /// <summary>
        /// Remove "bookend" characters (brackets, quotes, parenthesis) and convert to lower case.
        /// </summary>
        private static string RemoveBookendsAndLower(string s)
        {
            return RemoveBracketsQuotesParenthesis(s).ToLower();
        }

        private static string FixDatabaseName(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            bool first = true;
            foreach (string segment in s.Split(StringSeparators.Period))
            {
                if (!first)
                {
                    sb.Append(StringSeparators.Period);
                }
                else
                {
                    first = false;
                }
                sb.Append(RemoveBookendsAndLower(segment));
            }
            return sb.ToString();
        }

        private static readonly KeyValuePair<char, char>[] Bookends = new KeyValuePair<char, char>[] {
            new KeyValuePair<char, char>('[',']'),
            new KeyValuePair<char, char>('"','"'),
            new KeyValuePair<char, char>('\'','\''),
            new KeyValuePair<char, char>('(',')'),
            new KeyValuePair<char, char>('`','`')
        };

        public static string RemoveBracketsQuotesParenthesis(string value)
        {
            if (value.Length < 3)
                return value;

            var first = 0;
            var last = value.Length - 1;
            foreach (var kvp in Bookends)
            {
                while (value[first] == kvp.Key && value[last] == kvp.Value)
                {
                    first++;
                    last--;
                }
            }
            if (first != 0)
            {
                var length = value.Length - first * 2;
                value = value.Substring(first, length);
            }

            return value;
        }
    }
}
