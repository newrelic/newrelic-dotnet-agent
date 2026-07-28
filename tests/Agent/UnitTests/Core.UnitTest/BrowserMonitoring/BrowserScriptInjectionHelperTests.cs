// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.BrowserMonitoring;

[TestFixture]
public class BrowserScriptInjectionHelperTests
{
    private static readonly byte[] RumBytes = Encoding.UTF8.GetBytes("<script>RUM</script>");

    private const string HtmlContent = "<html><head></head><body></body></html>";

    // The agent injects immediately after the opening head tag when the head has no
    // charset / x-ua-compatible meta tag, so the expected index is just past its '>'.
    private static readonly int ExpectedHeadIndex = HtmlContent.IndexOf('>', HtmlContent.IndexOf("<head", StringComparison.Ordinal)) + 1;

    [Test]
    public async Task InjectBrowserScriptAsync_ReturnsTrue_AndWritesScriptAtHeadTag_WhenLocationFound()
    {
        var buffer = Encoding.UTF8.GetBytes(HtmlContent);
        var expectedIndex = ExpectedHeadIndex;

        using var baseStream = new MemoryStream();

        var result = await BrowserScriptInjectionHelper.InjectBrowserScriptAsync(buffer, baseStream, RumBytes, null);

        var expectedBytes = new byte[buffer.Length + RumBytes.Length];
        Buffer.BlockCopy(buffer, 0, expectedBytes, 0, expectedIndex);
        Buffer.BlockCopy(RumBytes, 0, expectedBytes, expectedIndex, RumBytes.Length);
        Buffer.BlockCopy(buffer, expectedIndex, expectedBytes, expectedIndex + RumBytes.Length, buffer.Length - expectedIndex);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(baseStream.ToArray(), Is.EqualTo(expectedBytes));
        });
    }

    [Test]
    public async Task InjectBrowserScriptAsync_ReturnsFalse_AndWritesBufferUnchanged_WhenNoLocationFound()
    {
        var content = "<html><p>Hello</p></html>"; // no <head> and no <body>
        var buffer = Encoding.UTF8.GetBytes(content);

        using var baseStream = new MemoryStream();

        var result = await BrowserScriptInjectionHelper.InjectBrowserScriptAsync(buffer, baseStream, RumBytes, null);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(baseStream.ToArray(), Is.EqualTo(buffer));
        });
    }

    [Test]
    public async Task InjectBrowserScriptAsync_ReturnsTrue_AndAppendsScript_WhenInsertionIndexIsAtEndOfBuffer()
    {
        // The opening head tag ends exactly at the end of the buffer, so the insertion index
        // equals the buffer length. That is a valid split point - the script is appended and
        // the trailing write covers zero bytes. This case used to be treated as an invalid
        // index, which returned without writing the buffer at all and silently dropped this
        // write's content from the response.
        var buffer = Encoding.UTF8.GetBytes("<html><head>");

        using var baseStream = new MemoryStream();

        var result = await BrowserScriptInjectionHelper.InjectBrowserScriptAsync(buffer, baseStream, RumBytes, null);

        var expectedBytes = new byte[buffer.Length + RumBytes.Length];
        Buffer.BlockCopy(buffer, 0, expectedBytes, 0, buffer.Length);
        Buffer.BlockCopy(RumBytes, 0, expectedBytes, buffer.Length, RumBytes.Length);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(baseStream.ToArray(), Is.EqualTo(expectedBytes));
        });
    }

    [Test]
    public async Task InjectBrowserScriptAsync_ReturnsFalse_WhenBaseStreamIsDisposed()
    {
        var buffer = Encoding.UTF8.GetBytes(HtmlContent); // a valid injection location exists
        var transaction = Mock.Create<ITransaction>();

        var baseStream = new MemoryStream();
        baseStream.Dispose();

        // the disposed stream must be swallowed rather than surfaced to the response writer
        var result = await BrowserScriptInjectionHelper.InjectBrowserScriptAsync(buffer, baseStream, RumBytes, transaction);

        Assert.That(result, Is.False);
        Mock.Assert(() => transaction.LogFinest("RUM Injection aborted: Stream was disposed."), Occurs.AtLeastOnce());
    }

    [Test]
    public async Task InjectBrowserScriptAsync_LogsInjectionIndex_WhenTransactionIsSupplied()
    {
        // every other test passes a null transaction, which exercises the null-conditional
        // side of the transaction?.LogFinest(...) calls; this covers the non-null side
        var transaction = Mock.Create<ITransaction>();
        var buffer = Encoding.UTF8.GetBytes(HtmlContent);

        using var baseStream = new MemoryStream();

        var result = await BrowserScriptInjectionHelper.InjectBrowserScriptAsync(buffer, baseStream, RumBytes, transaction);

        Assert.That(result, Is.True);
        Mock.Assert(() => transaction.LogFinest($"Injecting RUM script at byte index {ExpectedHeadIndex}."), Occurs.Once());
    }

    [Test]
    public async Task InjectBrowserScriptAsync_LogsSkipReason_WhenNoLocationFound()
    {
        var transaction = Mock.Create<ITransaction>();
        var buffer = Encoding.UTF8.GetBytes("<html><p>Hello</p></html>");

        using var baseStream = new MemoryStream();

        var result = await BrowserScriptInjectionHelper.InjectBrowserScriptAsync(buffer, baseStream, RumBytes, transaction);

        Assert.That(result, Is.False);
        Mock.Assert(() => transaction.LogFinest("Skipping RUM Injection: No suitable location found to inject script."), Occurs.Once());
    }

    [Test]
    public async Task InjectBrowserScriptAsync_ReturnsFalse_AndWritesNothing_WhenBufferIsEmpty()
    {
        var buffer = Array.Empty<byte>();

        using var baseStream = new MemoryStream();

        var result = await BrowserScriptInjectionHelper.InjectBrowserScriptAsync(buffer, baseStream, RumBytes, null);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(baseStream.ToArray(), Is.Empty);
        });
    }
}
