// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NewRelic.Providers.Wrapper.AspNetCore6Plus
{
    internal static class BrowserScriptInjectionHelper
    {
        // could also use </body> or some other tag
        private const string HeadCloseTag = "</head>";
        private static readonly byte[] _headCloseTagBytes = Encoding.UTF8.GetBytes(HeadCloseTag);

        public static Task InjectBrowserScriptAsync(ReadOnlyMemory<byte> buffer, HttpContext context, Stream baseStream, byte[] rumBytes)
        {
            return InjectBrowserScriptAsync(buffer.ToArray(), context, baseStream, rumBytes);
        }

        /// <summary>
        /// Injects the script just before the </head> tag
        /// </summary>
        public static async Task InjectBrowserScriptAsync(byte[] buffer, HttpContext context, Stream baseStream, byte[] rumBytes)
        {
            // TODO: Implement additional logic to determine an appropriate place to inject the script, similar to the code in BrowserMonitoringWriter.WriteScriptHeaders()
            var index = buffer.LastIndexOf(_headCloseTagBytes);

            if (index == -1)
            {
                // not found, can't inject anything
                await baseStream.WriteAsync(buffer, 0, buffer.Length);
                return;
            }

            // Write everything before the </head> tag
            await baseStream.WriteAsync(buffer, 0, index - 1);

            // Write the injected script
            await baseStream.WriteAsync(rumBytes, 0, rumBytes.Length);

            // Write the rest of the doc, starting at the </head> tag
            await baseStream.WriteAsync(buffer, index, buffer.Length - index);
        }
    }
}
