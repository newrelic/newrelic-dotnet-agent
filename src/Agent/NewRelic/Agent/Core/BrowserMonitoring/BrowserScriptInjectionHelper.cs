// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.BrowserMonitoring
{
    public static class BrowserScriptInjectionHelper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="buffer">UTF-8 encoded buffer representing the current page</param>
        /// <param name="baseStream"></param>
        /// <param name="getRumBytesFunc"></param>
        /// <returns></returns>
        public static async Task InjectBrowserScriptAsync(byte[] buffer, Stream baseStream, Func<byte[]> getRumBytesFunc)
        {
            var index = BrowserScriptInjectionIndexHelper.TryFindInjectionIndex(buffer);

            if (index == -1)
            {
                // not found, can't inject anything
                await baseStream.WriteAsync(buffer, 0, buffer.Length);
                return;
            }

            var rumBytes = getRumBytesFunc();

            // Write everything up to the insertion index
            await baseStream.WriteAsync(buffer, 0, index);

            // Write the RUM script
            await baseStream.WriteAsync(rumBytes, 0, rumBytes.Length);

            // Write the rest of the doc, starting after the insertion index
            await baseStream.WriteAsync(buffer, index, buffer.Length - index);
        }
    }
}
