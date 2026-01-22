// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text.RegularExpressions;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.BrowserMonitoring;

public class BrowserMonitoringWriter
{
    private readonly Func<string> _getJsScript;

    private static readonly Regex XUaCompatibleFilter = new Regex(@"(<\s*meta[^>]+http-equiv[\s]*=[\s]*['""]x-ua-compatible['""][^>]*>)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);
    private static readonly Regex CharsetFilter = new Regex(@"(<\s*meta[^>]+charset\s*=[^>]*>)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    public BrowserMonitoringWriter(Func<string> getJsScript)
    {
        _getJsScript = getJsScript;
    }

    private int? FindFirstOpeningHeadTag(string content)
    {
        int? indexOpeningHead = null;

        var indexTemp = content.IndexOf("<head", StringComparison.InvariantCultureIgnoreCase);
        if (indexTemp < 0)
            return null;

        if (content[indexTemp + 5] == '>' || content[indexTemp + 5] == ' ')
        {
            indexOpeningHead = indexTemp;
        }

        return indexOpeningHead;
    }

    private string AttemptInsertionPriorToBodyTag(string content)
    {
        var bodyWithJsHeader = string.Empty;

        var indexOfBodyTag = content.IndexOf("<body", StringComparison.InvariantCultureIgnoreCase);
        if (indexOfBodyTag < 0)
            return bodyWithJsHeader;

        var jsScriptWithBodyPrefix = string.Format("{0}<body", _getJsScript());
        bodyWithJsHeader = content.Replace("<body", jsScriptWithBodyPrefix, StringComparison.InvariantCultureIgnoreCase, 1);

        return bodyWithJsHeader;
    }

    // Specification for Javascript insertion: https://newrelic.atlassian.net/wiki/spaces/eng/pages/50299103/BAM+Agent+Auto-Instrumentation
    public virtual string WriteScriptHeaders(string content)
    {
        var openingHeadTagIndex = FindFirstOpeningHeadTag(content);

        // No <HEAD> tag. Attempt to insert before <BODY> tag (not a great fallback option).
        if (!openingHeadTagIndex.HasValue)
            return AttemptInsertionPriorToBodyTag(content);

        // Since we have a head tag (top of 'page'), search for <X_UA_COMPATIBLE> and for <CHARSET> tags in Head section
        var xUaCompatibleFilterMatch = XUaCompatibleFilter.Match(content, openingHeadTagIndex.Value);
        var charsetFilterMatch = CharsetFilter.Match(content, openingHeadTagIndex.Value);

        // We have a <HEAD> tag, but didn't find the other tags we wanted. Find </HEAD> tag.  It's okay if we don't find it!
        var closingHeadTagIndex = content.IndexOf("</head>", StringComparison.InvariantCultureIgnoreCase);

        // Check if we found the tags and based on which comes last AND that this happens INSIDE the HEAD tag - do a replace on that.
        if ((xUaCompatibleFilterMatch.Success || charsetFilterMatch.Success) && (xUaCompatibleFilterMatch.Index < closingHeadTagIndex || charsetFilterMatch.Index > closingHeadTagIndex))
        {
            var match = charsetFilterMatch;
            if(xUaCompatibleFilterMatch.Index > charsetFilterMatch.Index)
            {
                match = xUaCompatibleFilterMatch;
            }

            var contentSubString = content.Substring(match.Index, match.Length);
            var jsScriptWithContentSubString = string.Format("{0}{1}", contentSubString, _getJsScript());

            return content.Remove(match.Index, match.Length).Insert(match.Index, jsScriptWithContentSubString);
        }

        // Found both HEAD tags, no  meta tags, get index immediately after the <HEAD>. Find first '>' which will be end of head opening tag.
        var indexOfEndHeadOpeningTag = content.IndexOf('>', openingHeadTagIndex.Value);

        // The <HEAD> tag may be malformed or simply be another type of tag, if so do not use it
        if (!(indexOfEndHeadOpeningTag > openingHeadTagIndex))
            return string.Empty;

        // Get the whole open HEAD tag string
        var headOpeningTag = content.Substring(openingHeadTagIndex.Value, (indexOfEndHeadOpeningTag - openingHeadTagIndex.Value) + 1);

        // Insert immediately after the <HEAD> tag. 
        var jsScriptWithHeadPrefix = string.Format("{0}{1}", headOpeningTag, _getJsScript());
        return content.Replace(headOpeningTag, jsScriptWithHeadPrefix, StringComparison.InvariantCultureIgnoreCase, 1);
    }

}
