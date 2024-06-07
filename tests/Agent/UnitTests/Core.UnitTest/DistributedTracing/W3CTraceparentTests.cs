// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core.DistributedTracing;
using NUnit.Framework;

namespace NewRelic.Agent.Core.DistributedTracing
{
    [TestFixture]
    public class W3CTraceparentTests
    {
        [TestCase("00", "0af7651916cd43dd8448eb211c80319c", "b7ad6b7169203331", "00")]
        [TestCase("00", "0af7651916cd43dd8448eb211c80319c", "b7ad6b7169203331", "ac")]
        [TestCase("ac", "0af7651916cd43dd8448eb211c80319c", "b7ad6b7169203331", "00")]
        [TestCase("ac", "0af7651916cd43dd8448eb211c80319c", "b7ad6b7169203331", "ac")]
        public void Header__Valid_Traceparent(string version, string traceId, string parentId, string traceFlags)
        {
            var traceparentValue = $"{version}-{traceId}-{parentId}-{traceFlags}";
            var traceparent = W3CTraceparent.GetW3CTraceParentFromHeader(traceparentValue);

            Assert.Multiple(() =>
            {
                Assert.That(traceparent.ToString(), Is.EqualTo(traceparentValue));
                Assert.That(traceparent.Version.ToString("x2"), Is.EqualTo(version));
                Assert.That(traceparent.TraceId, Is.EqualTo(traceId));
                Assert.That(traceparent.ParentId, Is.EqualTo(parentId));
                Assert.That(traceparent.TraceFlags, Is.EqualTo(traceFlags));
            });
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("        ")]
        public void Header_NullEmptyWhitespace(string traceparentValue)
        {
            Assert.That(W3CTraceparent.GetW3CTraceParentFromHeader(traceparentValue), Is.Null);
        }

        [Test]
        public void Header_ContainsZeroed_TraceId()
        {
            var traceparentValue = "00-00000000000000000000000000000000-b7ad6b7169203331-00";

            Assert.That(W3CTraceparent.GetW3CTraceParentFromHeader(traceparentValue), Is.Null);

        }

        [Test]
        public void Header_ContainsZeroed_ParentId()
        {
            var traceparentValue = "00-0af7651916cd43dd8448eb211c80319c-0000000000000000-00";

            Assert.That(W3CTraceparent.GetW3CTraceParentFromHeader(traceparentValue), Is.Null);
        }

        [Test]
        public void Header_VersionIs_FF()
        {
            var traceparentValue = "ff-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00";
            Assert.That(W3CTraceparent.GetW3CTraceParentFromHeader(traceparentValue), Is.Null);
        }

        [TestCase("00-0af7651916cd43dd8448ebc80319c-b7ad6b7169203331-00-00")]
        [TestCase("00-0af7651916cd43dd8448eb211c8019c-b7ad6b7169203331-00-")]
        [TestCase("-00-0af7651916cd43dd8448eb211c8019c-b7ad6b7169203331-00")]
        [TestCase("00-0af7651916cd43dd8448eb211c8019c--b7ad6b7169203331-00")]
        [TestCase("00-000000000000000000000000000000000b7ad6b7169203331-00")]
        public void Header_DoesNotHave_Four_Fields_HasCorrectLength(string traceparentValue)
        {
            Assert.That(W3CTraceparent.GetW3CTraceParentFromHeader(traceparentValue), Is.Null);
        }

        [TestCase("000-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00")]
        [TestCase("00-00AF7651916CD43DD8448EB211C80319C-b7ad6b7169203331-00")]
        [TestCase("00-0af7651916cd43dd8448eb211c80319c-0b7ad6b7169203331-00")]
        [TestCase("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-000")]
        [TestCase("0-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00")]
        [TestCase("00-af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00")]
        [TestCase("00-0af7651916cd43dd8448eb211c80319c-7ad6b7169203331-00")]
        [TestCase("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-0")]
        public void Header_WrongLength(string traceparentValue)
        {
            Assert.That(W3CTraceparent.GetW3CTraceParentFromHeader(traceparentValue), Is.Null);
        }

        [TestCase("AC-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00")]
        [TestCase("ac-0AF7651916CD43DD8448EB211C80319C-b7ad6b7169203331-00")]
        [TestCase("ac-0af7651916cd43dd8448eb211c80319c-B7AD6B7169203331-00")]
        [TestCase("ac-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-BB")]
        public void Header_WrongCasing(string traceparentValue)
        {
            Assert.That(W3CTraceparent.GetW3CTraceParentFromHeader(traceparentValue), Is.Null);
        }

        [TestCase("0z-0af7651916cd43dd8448ebc80319c-b7ad6b7169203331-00")]
        [TestCase("00-zaf7651916cd43dd8448ebc80319c-b7ad6b7169203331-00")]
        [TestCase("00-0af7651916cd43dd8448ebc80319c-z7ad6b7169203331-00")]
        [TestCase("00-0af7651916cd43dd8448ebc80319c-b7ad6b7169203331-z0")]
        [TestCase(",0-0af7651916cd43dd8448ebc80319c-b7ad6b7169203331-00")]
        [TestCase("00-,af7651916cd43dd8448ebc80319c-b7ad6b7169203331-00")]
        [TestCase("00-0af7651916cd43dd8448ebc80319c-,7ad6b7169203331-00")]
        [TestCase("00-0af7651916cd43dd8448ebc80319c-b7ad6b7169203331-,0")]
        [TestCase("0\"-0af7651916cd43dd8448ebc80319c-b7ad6b7169203331-00")]
        [TestCase("00-\"af7651916cd43dd8448ebc80319c-b7ad6b7169203331-00")]
        [TestCase("00-0af7651916cd43dd8448ebc80319c-\"7ad6b7169203331-00")]
        [TestCase("00-0af7651916cd43dd8448ebc80319c-b7ad6b7169203331-\"0")]
        public void Header_InvalidCharacters(string traceparentValue)
        {
            Assert.That(W3CTraceparent.GetW3CTraceParentFromHeader(traceparentValue), Is.Null);
        }
    }
}
