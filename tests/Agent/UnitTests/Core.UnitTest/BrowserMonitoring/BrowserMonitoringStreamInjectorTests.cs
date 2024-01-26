// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.BrowserMonitoring
{
    [TestFixture]
    public class BrowserMonitoringStreamInjectorTests
    {
        private const string SampleJavaScript = "<script>console.log('test');</script>";

        [Test]
        public void Constructor_SetsProperties()
        {
            var baseStream = Mock.Create<Stream>();
            var contentEncoding = Encoding.UTF8;
            Func<string> getJavascriptAgentScript = () => SampleJavaScript;

            var injector = new BrowserMonitoringStreamInjector(getJavascriptAgentScript, baseStream, contentEncoding);

            Assert.Multiple(() =>
            {
                Assert.That(injector.CanRead, Is.EqualTo(baseStream.CanRead));
                Assert.That(injector.CanSeek, Is.EqualTo(baseStream.CanSeek));
                Assert.That(injector.CanWrite, Is.EqualTo(baseStream.CanWrite));
                Assert.That(injector.Length, Is.EqualTo(baseStream.Length));
            });
        }

        [Test]
        public void Position_GetAndSet()
        {
            var baseStream = Mock.Create<Stream>();
            long position = 123;
            Mock.Arrange(() => baseStream.Position).Returns(() => position);
            Mock.ArrangeSet(() => baseStream.Position = Arg.IsAny<long>()).DoInstead<long>(value => position = value);

            var contentEncoding = Encoding.UTF8;
            Func<string> getJavascriptAgentScript = () => SampleJavaScript;

            var injector = new BrowserMonitoringStreamInjector(getJavascriptAgentScript, baseStream, contentEncoding);

            Assert.That(injector.Position, Is.EqualTo(123));
            injector.Position = 456;
            Assert.That(baseStream.Position, Is.EqualTo(456));
        }

        [Test]
        public void Close_ClosesBaseStream()
        {
            var baseStream = Mock.Create<Stream>();
            Mock.Arrange(() => baseStream.Close());
            var contentEncoding = Encoding.UTF8;
            Func<string> getJavascriptAgentScript = () => SampleJavaScript;

            var injector = new BrowserMonitoringStreamInjector(getJavascriptAgentScript, baseStream, contentEncoding);
            injector.Close();

            Mock.Assert(() => baseStream.Close(), Occurs.Once());
        }

        [Test]
        public void Flush_CallsBaseStreamFlush()
        {
            var baseStream = Mock.Create<Stream>();
            Mock.Arrange(() => baseStream.Flush());
            var contentEncoding = Encoding.UTF8;
            Func<string> getJavascriptAgentScript = () => SampleJavaScript;

            var injector = new BrowserMonitoringStreamInjector(getJavascriptAgentScript, baseStream, contentEncoding);
            injector.Flush();

            Mock.Assert(() => baseStream.Flush(), Occurs.Once());
        }

        [Test]
        public void Seek_CallsBaseStreamSeek()
        {
            var baseStream = Mock.Create<Stream>();
            Mock.Arrange(() => baseStream.Seek(123, SeekOrigin.Begin)).Returns(456);
            var contentEncoding = Encoding.UTF8;
            Func<string> getJavascriptAgentScript = () => SampleJavaScript;

            var injector = new BrowserMonitoringStreamInjector(getJavascriptAgentScript, baseStream, contentEncoding);
            var result = injector.Seek(123, SeekOrigin.Begin);

            Assert.That(result, Is.EqualTo(456));
            Mock.Assert(() => baseStream.Seek(123, SeekOrigin.Begin), Occurs.Once());
        }

        [Test]
        public void SetLength_CallsBaseStreamSetLength()
        {
            var baseStream = Mock.Create<Stream>();
            Mock.Arrange(() => baseStream.SetLength(123));
            var contentEncoding = Encoding.UTF8;
            Func<string> getJavascriptAgentScript = () => SampleJavaScript;
            var injector = new BrowserMonitoringStreamInjector(getJavascriptAgentScript, baseStream, contentEncoding);
            injector.SetLength(123);

            Mock.Assert(() => baseStream.SetLength(123), Occurs.Once());
        }

        [Test]
        public void Read_CallsBaseStreamRead()
        {
            var baseStream = Mock.Create<Stream>();
            byte[] buffer = new byte[100];
            Mock.Arrange(() => baseStream.Read(buffer, 10, 50)).Returns(30);
            var contentEncoding = Encoding.UTF8;
            Func<string> getJavascriptAgentScript = () => SampleJavaScript;

            var injector = new BrowserMonitoringStreamInjector(getJavascriptAgentScript, baseStream, contentEncoding);
            var result = injector.Read(buffer, 10, 50);

            Assert.That(result, Is.EqualTo(30));
            Mock.Assert(() => baseStream.Read(buffer, 10, 50), Occurs.Once());
        }

        [Test]
        public void Write_CallsBaseStreamWrite()
        {
            var baseStream = Mock.Create<Stream>();
            byte[] buffer = new byte[100];
            Mock.Arrange(() => baseStream.Write(buffer, 10, 50));
            var contentEncoding = Encoding.UTF8;
            Func<string> getJavascriptAgentScript = () => SampleJavaScript;

            var injector = new BrowserMonitoringStreamInjector(getJavascriptAgentScript, baseStream, contentEncoding);
            injector.Write(buffer, 10, 50);

            Mock.Assert(() => baseStream.Write(buffer, 10, 50), Occurs.Once());
        }

        [Test]
        public void Write_InjectsRUMScript()
        {
            var baseStream = new MemoryStream();
            var body = "<body><h1>Hello World!</h1></body>";
            byte[] buffer = Encoding.UTF8.GetBytes("<html><head></head>" + body + "</html>");
            var contentEncoding = Encoding.UTF8;
            Func<string> getJavascriptAgentScript = () => SampleJavaScript;

            var injector = new BrowserMonitoringStreamInjector(getJavascriptAgentScript, baseStream, contentEncoding);
            injector.Write(buffer, 0, buffer.Length);
            injector.Flush();

            baseStream.Position = 0;
            using (StreamReader reader = new StreamReader(baseStream, contentEncoding))
            {
                string content = reader.ReadToEnd();
                Assert.Multiple(() =>
                {
                    Assert.That(content, Does.Contain(SampleJavaScript), "JavaScript was not injected.");
                    Assert.That(content, Does.Contain(body), "body was not written without modification.");
                });
            }
        }

        [Test]
        public void Write_WritesContentWithoutModification_IfUnableToInjectRUM()
        {
            var baseStream = new MemoryStream();
            byte[] buffer = Encoding.UTF8.GetBytes("Sample content");
            var contentEncoding = Encoding.UTF8;
            Func<string> getJavascriptAgentScript = () => null; 

            // if WriteScriptHeaders() returns null, RUM isn't injected.
            var browserMonitoringWriter = Mock.Create<BrowserMonitoringWriter>();
            Mock.Arrange(() => browserMonitoringWriter.WriteScriptHeaders(Arg.IsAny<string>())).Returns((string)null);

            var injector = new BrowserMonitoringStreamInjector(getJavascriptAgentScript, baseStream, contentEncoding, browserMonitoringWriter);
            injector.Write(buffer, 0, buffer.Length);
            injector.Flush();

            baseStream.Position = 0;
            using (StreamReader reader = new StreamReader(baseStream, contentEncoding))
            {
                string content = reader.ReadToEnd();
                Assert.That(content, Is.EqualTo(Encoding.UTF8.GetString(buffer)), "Content should be written without modification.");
            }
        }

        [Test]
        public void TryGetInjectedBytes_ReturnsNullWhenDecodedBufferIsEmpty()
        {
            var baseStream = new MemoryStream();
            byte[] buffer = new byte[0]; // Empty buffer
            var contentEncoding = Encoding.UTF8;
            Func<string> getJavascriptAgentScript = () => SampleJavaScript;

            var injector = new BrowserMonitoringStreamInjector(getJavascriptAgentScript, baseStream, contentEncoding);
            injector.Write(buffer, 0, buffer.Length);
            injector.Flush();

            baseStream.Position = 0;
            using (StreamReader reader = new StreamReader(baseStream, contentEncoding))
            {
                string content = reader.ReadToEnd();
                Assert.That(content, Is.Empty, "Content should be empty.");
            }
        }
    }
}
