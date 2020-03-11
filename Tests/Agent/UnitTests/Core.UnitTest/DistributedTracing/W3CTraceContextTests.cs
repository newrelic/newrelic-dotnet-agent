using NewRelic.Core.DistributedTracing;
using NUnit.Framework;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.DistributedTracing
{
	public class W3CTraceContextTests
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

			Assert.AreEqual(tracestate.Version, 0);
			Assert.AreEqual((int)tracestate.ParentType, 0);
			Assert.AreEqual(tracestate.AccountId, "33");
			Assert.AreEqual(tracestate.AppId, "5043");
			Assert.AreEqual(tracestate.SpanId, "27ddd2d8890283b4");
			Assert.AreEqual(tracestate.TransactionId, "5569065a5b1313bd");
			Assert.AreEqual(tracestate.Sampled, 1);
			Assert.AreEqual(tracestate.Priority, 1.23456f);
			Assert.AreEqual(tracestate.Timestamp, 1518469636025);

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

			Assert.AreEqual(tracestate.Version, 0);
			Assert.AreEqual((int)tracestate.ParentType, 0);
			Assert.AreEqual(tracestate.AccountId, "33");
			Assert.AreEqual(tracestate.AppId, "5043");
			Assert.AreEqual(tracestate.SpanId, "27ddd2d8890283b4");
			Assert.AreEqual(tracestate.TransactionId, "5569065a5b1313bd");
			Assert.AreEqual(tracestate.Sampled, 1);
			Assert.AreEqual(tracestate.Priority, 1.23456f);
			Assert.AreEqual(tracestate.Timestamp, 1518469636025);

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

			Assert.AreEqual(tracestate.Version, 0);
			Assert.AreEqual((int)tracestate.ParentType, 0);
			Assert.AreEqual(tracestate.AccountId, "55");
			Assert.AreEqual(tracestate.AppId, "5043");
			Assert.AreEqual(tracestate.SpanId, "1238890283aasdfs");
			Assert.AreEqual(tracestate.TransactionId, "4569065a5b131bbg");
			Assert.AreEqual(tracestate.Sampled, 1);
			Assert.AreEqual(tracestate.Priority, 1.23456f);
			Assert.AreEqual(tracestate.Timestamp, 1518469636020);

			Assert.That(tracestate.VendorstateEntries.Count == 0, Is.True);
		}

		//Valid tracestate entry
		[TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025", "33", ExpectedResult = true)]
		[TestCase("33@nr=0-1-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025", "33", ExpectedResult = true)]
		[TestCase("33@nr=0-2-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025", "33", ExpectedResult = true)]

		//Valid tracestate with blank fields
		[TestCase("33@nr=0-0-33-5043-----1518469636025", "33", ExpectedResult = true)]

		//Invalid tracestate - parentType should be either 0, 1 or 2
		[TestCase("33@nr=0-3-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025", "33", ExpectedResult = false)]

		//Invalid tracestate - Missing required fields
		[TestCase("33@nr=-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025", "33", ExpectedResult = false)]
		[TestCase("33@nr=0--33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025", "33", ExpectedResult = false)]
		[TestCase("33@nr=0-0--5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025", "33", ExpectedResult = false)]
		[TestCase("33@nr=0-0-33--27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-1518469636025", "33", ExpectedResult = false)]
		[TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.23456-", "33", ExpectedResult = false)]

		//Invalid tracestate - Priority has trailing 0s
		[TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.2000-1518469636025", "33", ExpectedResult = false)]
		//Invalid tracestate - Priority isn't rounded to 6 decimal places
		[TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1.12345678-1518469636025", "33", ExpectedResult = false)]
		//Invalid tracestate - Priority is in unaccepted format (Example: 1e-2)
		[TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-1e-2-1518469636025", "33", ExpectedResult = false)]
		//Valid tracestate - Priority doesn't have integer part
		[TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-.123-1518469636025", "33", ExpectedResult = true)]
		//Valid tracestate - Priority has only integer part
		[TestCase("33@nr=0-0-33-5043-27ddd2d8890283b4-5569065a5b1313bd-1-123-1518469636025", "33", ExpectedResult = true)]

		//Invalid tracestate - Value has non ASCII characters
		[TestCase("33@nr=¢µÈÈÂÂÂÂÂ", "33", ExpectedResult = false)]
		//Invalid tracestate - Value has ','
		[TestCase("33@nr=abc,abc", "33", ExpectedResult = false)]
		//Invalid tracestate - Value has '='
		[TestCase("33@nr=abc=abc", "33", ExpectedResult = false)]
		//Invalid tracestate - No entries is parsable
		[TestCase("aaa,bbb,ccc", "33", ExpectedResult = false)]
		public bool GetW3CTracestateFromHeaders_NewRelicTracestateEntry_Tests(string headerString, string trustedAccountKey)
		{
			var testHeaders = new List<string>()
			{
				headerString
			};

			var tracestate = W3CTracestate.GetW3CTracestateFromHeaders(testHeaders, trustedAccountKey);
			if (tracestate != null)
			{
				return true;
			}

			return false;
		}
	}
}
