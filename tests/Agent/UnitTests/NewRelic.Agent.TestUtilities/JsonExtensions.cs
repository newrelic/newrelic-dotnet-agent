// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using NUnit.Framework;

namespace NewRelic.Agent.TestUtilities
{
    public static class JsonExtensions
    {
        /// <summary>
        /// Strips all whitespace and newlines from a JSON string, excluding any that are in quotes.
        /// This is for testing only, won't work for badly formatted JSON, and doesn't support
        /// escaped quotation marks.
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static string Condense(this string json)
        {
            if (json.Contains("\\\""))
            {
                Assert.Fail("Oops! Need to add support for escaped quote characters in JsonExtensions.Condense()");
            }
            StringBuilder output = new StringBuilder();
            bool inString = false;
            foreach (var c in json)
            {
                switch(c)
                {
                    case '\r':
                    case '\n':
                        continue;
                    case '\t':
                    case ' ':
                        if (!inString) continue;
                        break;
                    case '\"':
                        inString = !inString;
                        break;
                }
                output.Append(c);
            }
            return output.ToString();
        }
    }
}
