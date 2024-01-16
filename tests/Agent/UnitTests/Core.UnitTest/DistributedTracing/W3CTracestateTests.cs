// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core.DistributedTracing;

namespace NewRelic.Agent.Core.DistributedTracing
{
    public class W3CTracestateTests
    {
        [TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025,dd=YzRiMTIxODk1NmVmZTE4ZQ,44@nr=0-0-55-5043-1238890283aasdfs-4569065a5b131bbg-1-1.23456-1518469636020", "33")]
        [TestCase(" 33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025,dd=YzRiMTIxODk1NmVmZTE4ZQ,44@nr=0-0-55-5043-1238890283aasdfs-4569065a5b131bbg-1-1.23456-1518469636020", "33")]
        [TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025, dd=YzRiMTIxODk1NmVmZTE4ZQ, 44@nr=0-0-55-5043-1238890283aasdfs-4569065a5b131bbg-1-1.23456-1518469636020", "33")]
        [TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025, dd=YzRiMTIxODk1NmVmZTE4ZQ, aaaaaaaaaaaaaaa, 44@nr=0-0-55-5043-1238890283aasdfs-4569065a5b131bbg-1-1.23456-1518469636020", "33")]
        [TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025, dd=YzRiMTIxODk1NmVmZTE4ZQ, 44@nr=0-0-55-5043-1238890283aasdfs-4569065a5b131bbg-1-1.23456-1518469636020, aaaaaaaaaaaaaa", "33")]
        public void GetW3CTracestateFromHeaders_ValidTracestateString_Tests(string headerString, string trustedAccountKey)
        {
            var testHeaders = new List<string>()
            {
                headerString
            };

            var tracestate = W3CTracestate.GetW3CTracestateFromHeaders(testHeaders, trustedAccountKey);

            ClassicAssert.AreEqual(tracestate.Version, 0);
            ClassicAssert.AreEqual((int)tracestate.ParentType, 0);
            ClassicAssert.AreEqual(tracestate.AccountId, "33");
            ClassicAssert.AreEqual(tracestate.AppId, "5043");
            ClassicAssert.AreEqual(tracestate.SpanId, "27ddd2d8890283b4");
            ClassicAssert.AreEqual(tracestate.TransactionId, "5569065a5b1313bd");
            ClassicAssert.AreEqual(tracestate.Sampled, 1);
            ClassicAssert.AreEqual(tracestate.Priority, 1.23456f);
            ClassicAssert.AreEqual(tracestate.Timestamp, 1518469636025);

            Assert.That(tracestate.VendorstateEntries.Count == 2, Is.True);
            Assert.That(tracestate.VendorstateEntries.Contains("dd=YzRiMTIxODk1NmVmZTE4ZQ"), Is.True);
            Assert.That(tracestate.VendorstateEntries.Contains("44@nr=0-0-55-5043-1238890283aasdfs-4569065a5b131bbg-1-1.23456-1518469636020"), Is.True);
            Assert.That(tracestate.VendorstateEntries.Contains($"{trustedAccountKey}@nr"), Is.False);
        }

        [Test]
        public void GetW3CTracestateFromHeaders_DuplicateNRKeysInSameHeader_AcceptFirstOne_Test()
        {

            var testHeaders = new List<string>()
            {
                "33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025, othervendorkey1=othervendorvalue1, 33@nr=0-0-55-5043-1238890283aasdfs-4569065a5b131bbg-1-1.23456-1518469636020",
            };

            var trustedAccountKey = "33";

            var tracestate = W3CTracestate.GetW3CTracestateFromHeaders(testHeaders, trustedAccountKey);

            ClassicAssert.AreEqual(tracestate.Version, 0);
            ClassicAssert.AreEqual((int)tracestate.ParentType, 0);
            ClassicAssert.AreEqual(tracestate.AccountId, "33");
            ClassicAssert.AreEqual(tracestate.AppId, "5043");
            ClassicAssert.AreEqual(tracestate.SpanId, "27ddd2d8890283b4");
            ClassicAssert.AreEqual(tracestate.TransactionId, "5569065a5b1313bd");
            ClassicAssert.AreEqual(tracestate.Sampled, 1);
            ClassicAssert.AreEqual(tracestate.Priority, 1.23456f);
            ClassicAssert.AreEqual(tracestate.Timestamp, 1518469636025);

            Assert.That(tracestate.VendorstateEntries.Count == 1, Is.True);
            Assert.That(tracestate.VendorstateEntries.Contains("othervendorkey1=othervendorvalue1"), Is.True);
        }

        [Test]
        public void GetW3CTracestateFromHeaders_DuplicateNRKeysInDifferentHeaders_AcceptLastOne_Test()
        {
            var testHeaders = new List<string>()
            {
                "33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025",
                "33@nr=0-0-55-5043-1238890283aasdfs-4569065a5b131bbg-1-1.23456-1518469636020",
            };

            var trustedAccountKey = "33";

            var tracestate = W3CTracestate.GetW3CTracestateFromHeaders(testHeaders, trustedAccountKey);

            ClassicAssert.AreEqual(tracestate.Version, 0);
            ClassicAssert.AreEqual((int)tracestate.ParentType, 0);
            ClassicAssert.AreEqual(tracestate.AccountId, "55");
            ClassicAssert.AreEqual(tracestate.AppId, "5043");
            ClassicAssert.AreEqual(tracestate.SpanId, "1238890283aasdfs");
            ClassicAssert.AreEqual(tracestate.TransactionId, "4569065a5b131bbg");
            ClassicAssert.AreEqual(tracestate.Sampled, 1);
            ClassicAssert.AreEqual(tracestate.Priority, 1.23456f);
            ClassicAssert.AreEqual(tracestate.Timestamp, 1518469636020);

            Assert.That(tracestate.VendorstateEntries.Count == 0, Is.True);
        }

        //Valid tracestate - only has New Relic entry
        [TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025", "33", null, 1, 1.23456f, IngestErrorType.None)]

        //Valid tracestate entry with different parent types
        [TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025,aa=1111,bb=222", "33", "aa=1111,bb=222", 1, 1.23456f, IngestErrorType.None)]
        [TestCase("33@nr=0-1-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025,aa=1111,bb=222", "33", "aa=1111,bb=222", 1, 1.23456f, IngestErrorType.None)]
        [TestCase("33@nr=0-2-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025,aa=1111,bb=222", "33", "aa=1111,bb=222", 1, 1.23456f, IngestErrorType.None)]

        //Valid tracestate with blank fields
        [TestCase("33@nr=0-0-33-5043-----1518469636025,aa=1111,bb=222", "33", "aa=1111,bb=222", null, null, IngestErrorType.None)]

        //Invalid tracestate - parentType should be either 0, 1 or 2
        [TestCase("33@nr=0-3-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025,aa=1111,bb=222", "33", "aa=1111,bb=222", null, null, IngestErrorType.TraceStateInvalidNrEntry)]

        //Invalid tracestate - Missing required fields
        [TestCase("33@nr=-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025,aa=1111,bb=222", "33", "aa=1111,bb=222", null, null, IngestErrorType.TraceStateInvalidNrEntry)]
        [TestCase("33@nr=0--33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025,aa=1111,bb=222", "33", "aa=1111,bb=222", null, null, IngestErrorType.TraceStateInvalidNrEntry)]
        [TestCase("33@nr=0-0--5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025,aa=1111,bb=222", "33", "aa=1111,bb=222", null, null, IngestErrorType.TraceStateInvalidNrEntry)]
        [TestCase("33@nr=0-0-33--27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025,aa=1111,bb=222", "33", "aa=1111,bb=222", null, null, IngestErrorType.TraceStateInvalidNrEntry)]
        [TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-,aa=1111,bb=222", "33", "aa=1111,bb=222", null, null, IngestErrorType.TraceStateInvalidNrEntry)]

        //Valid tracestate - Sampled value is defferent from 0 or 1 - Treats Sampled as null
        [TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-2-1.23456-1518469636025", "33", null, null, 1.23456f, IngestErrorType.None)]

        //Valid tracestate - Priority has trailing 0s which should be truncated
        [TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.2000-1518469636025,aa=1111,bb=222", "33", "aa=1111,bb=222", 1, 1.2f, IngestErrorType.None)]
        //Valid tracestate - Priority isn't rounded to 6 decimal places - Treats Priority as null
        [TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.12345678-1518469636025,aa=1111,bb=222", "33", "aa=1111,bb=222", 1, null, IngestErrorType.None)]
        //Valid tracestate - Priority doesn't have integer part
        [TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-.123-1518469636025,aa=1111,bb=222", "33", "aa=1111,bb=222", 1, .123f, IngestErrorType.None)]
        //Valid tracestate - Priority has only integer part
        [TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-123-1518469636025,aa=1111,bb=222", "33", "aa=1111,bb=222", 1, 123f, IngestErrorType.None)]

        //Invalid tracestate - Priority is in unaccepted format (Example: 1e-2, 1,234) so will be null, but Invalid status is due to it parsing into 10 fields (not 9) which is not valid for the version (0)
        [TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1e-2-1518469636025,aa=1111,bb=222", "33", "aa=1111,bb=222", null, null, IngestErrorType.TraceStateInvalidNrEntry)]
        [TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1,234-1518469636025,aa=1111,bb=222", "33", "aa=1111,bb=222", null, null, IngestErrorType.TraceStateInvalidNrEntry)]

        //Invalid tracestate - Value has non ASCII characters
        [TestCase("33@nr=¢µÈÈÂÂÂÂÂ,aa=1111,bb=222", "33", "aa=1111,bb=222", null, null, IngestErrorType.TraceStateInvalidNrEntry)]
        //Invalid tracestate - Value has ','
        [TestCase("33@nr=abc,abc,aa=1111,bb=222", "33", "aa=1111,bb=222", null, null, IngestErrorType.TraceStateInvalidNrEntry)]
        //Invalid tracestate - Value has '='
        [TestCase("33@nr=abc=abc,aa=1111,bb=222", "33", "aa=1111,bb=222", null, null, IngestErrorType.TraceStateInvalidNrEntry)]

        //Invalid tracestate - trusted newrelic entry doesn't exist
        [TestCase("44@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025,aa=1111,bb=222", "33", "44@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025,aa=1111,bb=222", null, null, IngestErrorType.TraceStateNoNrEntry)]
        [TestCase("aa=1111,bb=222", "33", "aa=1111,bb=222", null, null, IngestErrorType.TraceStateNoNrEntry)]

        //Invalid tracestate - No entries is parsable - expects a null tracestate
        [TestCase("aaa,bbb,ccc", "33", null, null, null, IngestErrorType.TraceStateNoNrEntry)]
        public void GetW3CTracestateFromHeaders_NewRelicTracestateEntry_Tests(string headerString, string trustedAccountKey,
            string expectedOtherVendors, int? expectedSampled, float? expectedPriority, IngestErrorType expectedIngestError)
        {
            var testHeaders = new List<string>()
            {
                headerString
            };

            var tracestate = W3CTracestate.GetW3CTracestateFromHeaders(testHeaders, trustedAccountKey);

            if (expectedIngestError == IngestErrorType.None)
            {
                Assert.That(tracestate, Is.Not.Null);
            }
            else if (expectedOtherVendors != null)
            {
                Assert.That(tracestate.VendorstateEntries.Count > 0);
                ClassicAssert.AreEqual(expectedOtherVendors, string.Join(",", tracestate.VendorstateEntries));
            }

            Assert.That(tracestate.Priority == expectedPriority, $@"Expects Priority {expectedPriority} but gets Priority {tracestate.Priority } instead.");
            Assert.That(tracestate.Sampled == expectedSampled, $@"Expects Sampled {expectedSampled} but gets Sampled {tracestate.Sampled } instead.");
            Assert.That(expectedIngestError == tracestate.Error, $@"Expects Error {expectedIngestError} but gets Error {tracestate.Error } instead.");
        }
    }
}
