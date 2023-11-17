// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;
using System.Text.RegularExpressions;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.BrowserMonitoring
{
    internal static class BrowserScriptInjectionIndexHelper
    {

        private static readonly Regex XUaCompatibleFilter = new Regex(@"(<\s*meta[^>]+http-equiv[\s]*=[\s]*['""]x-ua-compatible['""][^>]*>)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);
        private static readonly Regex CharsetFilter = new Regex(@"(<\s*meta[^>]+charset\s*=[^>]*>)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

        /// <summary>
        /// Returns the index into the (UTF-8 encoded) buffer where the RUM script should be injected, or -1 if no suitable location is found
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        /// <remarks>
        /// Specification for Javascript insertion: https://newrelic.atlassian.net/wiki/spaces/eng/pages/50299103/BAM+Agent+Auto-Instrumentation
        /// </remarks>
        public static int TryFindInjectionIndex(byte[] content)
        {
            try
            {
                var contentAsString = Encoding.UTF8.GetString(content);

                var openingHeadTagIndex = FindFirstOpeningHeadTag(contentAsString);

                // No <HEAD> tag. Attempt to insert before <BODY> tag (not a great fallback option).
                if (openingHeadTagIndex == -1)
                {
                    return FindIndexBeforeBodyTag(content, contentAsString);
                }

                // Since we have a head tag (top of 'page'), search for <X_UA_COMPATIBLE> and for <CHARSET> tags in Head section
                var xUaCompatibleFilterMatch = XUaCompatibleFilter.Match(contentAsString, openingHeadTagIndex);
                var charsetFilterMatch = CharsetFilter.Match(contentAsString, openingHeadTagIndex);

                // Try to find </HEAD> tag. (It's okay if we don't find it!)
                var closingHeadTagIndex = contentAsString.IndexOf("</head>", StringComparison.InvariantCultureIgnoreCase);

                // Find which of the two tags occurs latest (if at all) and ensure that at least
                // one of the matches occurs prior to the closing head tag
                if ((xUaCompatibleFilterMatch.Success || charsetFilterMatch.Success) &&
                    (xUaCompatibleFilterMatch.Index < closingHeadTagIndex || charsetFilterMatch.Index < closingHeadTagIndex))
                {
                    var match = charsetFilterMatch;
                    if (xUaCompatibleFilterMatch.Index > charsetFilterMatch.Index)
                    {
                        match = xUaCompatibleFilterMatch;
                    }

                    // find the index just after the end of the regex match in the UTF-8 buffer
                    var contentSubString = contentAsString.Substring(match.Index, match.Length);
                    var utf8HeadMatchIndex = IndexOfByteArray(content, contentSubString, out var substringBytesLength);

                    return utf8HeadMatchIndex + substringBytesLength;
                }

                // found opening head tag but no meta tags, insert immediately after the opening head tag
                // Find first '>' after the opening head tag, which will be end of head opening tag.
                var indexOfEndHeadOpeningTag = contentAsString.IndexOf('>', openingHeadTagIndex);

                // The <HEAD> tag may be malformed or simply be another type of tag, if so do not use it
                if (!(indexOfEndHeadOpeningTag > openingHeadTagIndex))
                    return -1;

                // Get the whole open HEAD tag string
                var headOpeningTag = contentAsString.Substring(openingHeadTagIndex, (indexOfEndHeadOpeningTag - openingHeadTagIndex) + 1);
                var utf8HeadOpeningTagIndex = IndexOfByteArray(content, headOpeningTag, out var headOpeningTagBytesLength);
                return utf8HeadOpeningTagIndex + headOpeningTagBytesLength;
            }
            catch (Exception e)
            {
                Log.LogMessage(LogLevel.Error, e, "Unexpected exception in TryFindInjectionIndex().");
                return -1;
            }
        }

        private static int FindIndexBeforeBodyTag(byte[] content, string contentAsString)
        {
            const string bodyOpenTag = "<body";

            var indexOfBodyTag = contentAsString.IndexOf(bodyOpenTag, StringComparison.InvariantCultureIgnoreCase);
            if (indexOfBodyTag < 0)
                return -1;

            // find the body tag start index in the UTF-8 buffer
            var bodyFromContent = contentAsString.Substring(indexOfBodyTag, bodyOpenTag.Length);
            var utf8BodyTagIndex = IndexOfByteArray(content, bodyFromContent, out _);
            return utf8BodyTagIndex;
        }

        private static int FindFirstOpeningHeadTag(string content)
        {
            int indexOpeningHead = -1;

            var indexTemp = content.IndexOf("<head", StringComparison.InvariantCultureIgnoreCase);
            if (indexTemp < 0)
                return -1;

            if (content[indexTemp + 5] == '>' || content[indexTemp + 5] == ' ')
            {
                indexOpeningHead = indexTemp;
            }

            return indexOpeningHead;
        }

        /// <summary>
        /// Returns an index into a byte array to find a string in the byte array.
        /// Exact match using the encoding provided or UTF-8 by default.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="stringToFind"></param>
        /// <param name="stringToFindBytesLength"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        private static int IndexOfByteArray(byte[] buffer, string stringToFind, out int stringToFindBytesLength, Encoding encoding = null)
        {
            stringToFindBytesLength = 0;
            encoding ??= Encoding.UTF8;

            if (buffer.Length == 0 || string.IsNullOrEmpty(stringToFind))
                return -1;

            var stringToFindBytes = encoding.GetBytes(stringToFind);
            stringToFindBytesLength = stringToFindBytes.Length;

            return buffer.AsSpan().IndexOf(stringToFindBytes);
        }
    }
}
