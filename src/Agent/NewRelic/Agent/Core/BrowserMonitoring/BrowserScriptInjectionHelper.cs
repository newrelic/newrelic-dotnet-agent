// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Threading.Tasks;
using NewRelic.Agent.Api;

namespace NewRelic.Agent.Core.BrowserMonitoring
{
    public static class BrowserScriptInjectionHelper
    {
        /// <summary>
        /// Determine where to inject the RUM script and write the buffer to the base stream.
        /// </summary>
        /// <param name="buffer">UTF-8 encoded buffer representing the current page</param>
        /// <param name="baseStream"></param>
        /// <param name="rumBytes"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        public static async Task InjectBrowserScriptAsync(byte[] buffer, Stream baseStream, byte[] rumBytes, ITransaction transaction)
        {
            var index = BrowserScriptInjectionIndexHelper.TryFindInjectionIndex(buffer);

            if (index == -1)
            {
                // not found, can't inject anything
                transaction?.LogFinest("Skipping RUM Injection: No suitable location found to inject script.");
                await baseStream.WriteAsync(buffer, 0, buffer.Length);
                return;
            }

            transaction?.LogFinest($"Injecting RUM script at byte index {index}.");

            if (index < buffer.Length) // validate index is less than buffer length
            {
                // Write everything up to the insertion index
                await baseStream.WriteAsync(buffer, 0, index);

                // Write the RUM script
                await baseStream.WriteAsync(rumBytes, 0, rumBytes.Length);

                // Write the rest of the doc, starting after the insertion index
                await baseStream.WriteAsync(buffer, index, buffer.Length - index);
            }
            else
                transaction?.LogFinest($"Skipping RUM Injection: Insertion index was invalid.");
        }
    }
}
