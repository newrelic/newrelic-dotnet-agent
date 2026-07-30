// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Threading.Tasks;
using NewRelic.Agent.Api;

namespace NewRelic.Agent.Core.BrowserMonitoring;

public static class BrowserScriptInjectionHelper
{
    /// <summary>
    /// Determine where to inject the RUM script and write the buffer to the base stream.
    /// </summary>
    /// <param name="buffer">UTF-8 encoded buffer representing the current page</param>
    /// <param name="baseStream"></param>
    /// <param name="rumBytes"></param>
    /// <param name="transaction"></param>
    /// <returns>True if the RUM script was actually written into the stream; false if the buffer was written through unmodified.</returns>
    public static async Task<bool> InjectBrowserScriptAsync(byte[] buffer, Stream baseStream, byte[] rumBytes, ITransaction transaction)
    {
        var index = BrowserScriptInjectionIndexHelper.TryFindInjectionIndex(buffer);
        if (index == -1)
        {
            // not found, can't inject anything
            transaction?.LogFinest("Skipping RUM Injection: No suitable location found to inject script.");
            await TryWriteStreamAsync(baseStream, buffer, 0, buffer.Length, transaction);
            return false;
        }

        transaction?.LogFinest($"Injecting RUM script at byte index {index}.");

        // TryFindInjectionIndex returns an index within the buffer, or an index equal to the
        // buffer length when the matched tag ends exactly at the end of this buffer - in which
        // case the script is appended and the trailing write covers zero bytes.

        // Write everything up to the insertion index
        await TryWriteStreamAsync(baseStream, buffer, 0, index, transaction);
        // Write the RUM script
        var scriptWritten = await TryWriteStreamAsync(baseStream, rumBytes, 0, rumBytes.Length, transaction);
        // Write the rest of the doc, starting after the insertion index
        await TryWriteStreamAsync(baseStream, buffer, index, buffer.Length - index, transaction);

        return scriptWritten;
    }

    private static async Task<bool> TryWriteStreamAsync(Stream stream, byte[] buffer, int offset, int count, ITransaction transaction)
    {
        try
        {
            await stream.WriteAsync(buffer, offset, count);
            return true;
        }
        catch (ObjectDisposedException)
        {
            transaction?.LogFinest("RUM Injection aborted: Stream was disposed.");
            return false;
        }
    }
}
