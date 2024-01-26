// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core.DistributedTracing;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using System;

namespace NewRelic.Agent.Core.JsonConverters
{
    [TestFixture]
    public class DistributedTracePayloadJsonConverterTests
    {
        [Test, Combinatorial]
        public void DistributedTracePayload_RoundTrip(
            [Values("App", "Mobile")] string type,
            [Values("12345")] string accountId,
            [Values("1111")] string appId,
            [Random(0ul, 0xfffffffffffffffful, 1), Values(ulong.MinValue, ulong.MaxValue)] ulong parentId,
            [Random(0ul, 0xfffffffffffffffful, 1), Values(ulong.MinValue, ulong.MaxValue)] ulong guid,
            [Random(0ul, 0xfffffffffffffffful, 1), Values(ulong.MinValue, ulong.MaxValue)] ulong traceId,
            [Values("56789")] string trustKey,
            [Random(0f, 1.0f, 1), Values(0.0f, 1.0f, 0.1234567f)] float priority,
            [Values(true, false)] bool sampled,
            [Values(new[] { 1970, 1, 1, 0, 0, 1 }, new[] { 2018, 12, 31, 23, 59, 59 })] int[] time,
            [Random(0ul, 0xfffffffffffffffful, 1), Values(ulong.MinValue, ulong.MaxValue)] ulong _transactionId)
        {
            var input = DistributedTracePayload.TryBuildOutgoingPayload(type, accountId, appId, $"{guid:X8}", $"{traceId:X8}", trustKey, priority, sampled,
                new DateTime(time[0], time[1], time[2], time[3], time[4], time[5], DateTimeKind.Utc), $"{_transactionId:X8}");
            var serialized = input.ToJson();
            var deserialized = DistributedTracePayload.TryBuildIncomingPayloadFromJson(serialized);
            Assert.That(deserialized.Version, Is.Not.Null);
            Assert.That(deserialized.Version, Has.Exactly(2).Items);
            Assert.Multiple(() =>
            {
                Assert.That(deserialized.Version[0], Is.EqualTo(0));
                Assert.That(deserialized.Version[1], Is.EqualTo(1));

                Assert.That(deserialized.Type, Is.EqualTo(input.Type));
                Assert.That(deserialized.AccountId, Is.EqualTo(input.AccountId));
                Assert.That(deserialized.AppId, Is.EqualTo(input.AppId));
                Assert.That(deserialized.Guid, Is.EqualTo(input.Guid));
                Assert.That(deserialized.TraceId, Is.EqualTo(input.TraceId));
                Assert.That(deserialized.TrustKey, Is.EqualTo(input.TrustKey));
                Assert.That(deserialized.Sampled, Is.EqualTo(input.Sampled));
                Assert.That(deserialized.Timestamp, Is.EqualTo(input.Timestamp));
                Assert.That(deserialized.TransactionId, Is.EqualTo(input.TransactionId));
            });
        }


        [Test]
        public void DistributedTracePayloadWithZeroBasedTimestampThrows()
        {
            var type = "App";
            var accountId = "12345";
            var appId = "1111";
            var parentId = $"{0xfffffffffffffffful:X9}";
            var guid = $"{0xfffffffffffffffful:X9}";
            var traceId = $"{0xfffffffffffffffful:X9}";
            var trustKey = "56789";
            var priority = 0f;
            var sampled = true;
            var time = new int[] { 1970, 1, 1, 0, 0, 0 };
            var timestamp = new DateTime(time[0], time[1], time[2], time[3], time[4], time[5], DateTimeKind.Utc);
            var transactionId = $"{0xfffffffffffffffful:X9}";

            var input = DistributedTracePayload.TryBuildOutgoingPayload(type, accountId, appId, $"{guid:X8}", $"{traceId:X8}", trustKey, priority, sampled, timestamp, transactionId);
            var serialized = input.ToJson();

            Assert.Throws<DistributedTraceAcceptPayloadParseException>(() => DistributedTracePayload.TryBuildIncomingPayloadFromJson(serialized));
        }

        //prefixing a field name with "___" makes the field "missing" (e.g. pa is missing because we've made the field name ___pa)
        private static readonly TestCaseData[] RequiredOptionalFields =
        {
            new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""ty"": ""App"",
						""ac"": ""99999123"",
						""ap"": ""51424"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""tk"": ""98765"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577,
						""tx"": ""7721bf09a70bcf7d""
						}
				}", new ThrowsNothingConstraint()).SetName("{m}_valid_nothrow"),
            new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""ty"": ""App"",
						""ac"": ""99999123"",
						""ap"": ""51424"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""__tk"": ""98765"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577,
						""tx"": ""7721bf09a70bcf7d""
						}
				}", new ThrowsNothingConstraint()).SetName("{m}_missing_trustKey_nothrow"),
            new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
												""ty"": ""App"",
						""ac"": ""99999123"",
						""ap"": ""51424"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""tk"": ""98765"",
						""__pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577,
						""tx"": ""7721bf09a70bcf7d""
						}
				}", new ThrowsNothingConstraint()).SetName("{m}_missing_priority_nothrow"),
            new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""ty"": ""App"",
						""ac"": ""99999123"",
						""ap"": ""51424"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""tk"": ""98765"",
						""pr"": 0.1234,
						""__sa"": false,
						""ti"": 1482959525577,
						""tx"": ""7721bf09a70bcf7d""
						}
				}", new ThrowsNothingConstraint()).SetName("{m}_missing_sampled_nothrow"),
            new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""ty"": ""App"",
						""ac"": ""99999123"",
						""ap"": ""51424"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""tk"": ""98765"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577,
						""__tx"": ""7721bf09a70bcf7d""
						}
				}", new ThrowsNothingConstraint()).SetName("{m}_missing_transactionId_nothrow"),

            new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""__ty"": ""App"",
						""ac"": ""99999123"",
						""ap"": ""51424"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""tk"": ""98765"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577,
						""tx"": ""7721bf09a70bcf7d""
						}
				}", Throws.Exception.TypeOf<DistributedTraceAcceptPayloadParseException>()).SetName("{m}_missing_type_throws"),
            new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""ty"": ""App"",
						""__ac"": ""99999123"",
						""ap"": ""51424"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""tk"": ""98765"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577,
						""tx"": ""7721bf09a70bcf7d""
						}
				}", Throws.Exception.TypeOf<DistributedTraceAcceptPayloadParseException>()).SetName("{m}_missing_account_throws"),
            new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""ty"": ""App"",
						""ac"": ""99999123"",
						""__ap"": ""51424"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""tk"": ""98765"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577,
						""tx"": ""7721bf09a70bcf7d""
						}
				}", Throws.Exception.TypeOf<DistributedTraceAcceptPayloadParseException>()).SetName("{m}_missing_appid_throws"),
            new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""ty"": ""App"",
						""ac"": ""99999123"",
						""ap"": ""51424"",
						""__id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""tk"": ""98765"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577,
						""tx"": ""7721bf09a70bcf7d""
						}
				}", new ThrowsNothingConstraint()).SetName(("{m}_missing_guid_nothrow")),
            new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""ty"": ""App"",
						""ac"": ""9123"",            
						""ap"": ""51424"",
						""pa"": ""5fa3c01498e244a6"",
						""id"": ""27856f70d3d314b7"",
						""___tr"": ""3221bf09aa0bcf0d"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577
						}
				}", Throws.Exception.TypeOf<DistributedTraceAcceptPayloadParseException>()).SetName("{m}_missing_traceid_throws"),
            new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""ty"": ""App"",
						""ac"": ""99999123"",
						""ap"": ""51424"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""tk"": ""98765"",
						""pr"": 0.1234,
						""sa"": false,
						""__ti"": 1482959525577,
						""tx"": ""7721bf09a70bcf7d""
						}
				}", Throws.Exception.TypeOf<DistributedTraceAcceptPayloadParseException>()).SetName("{m}_missing_timestamp_throws"),
            new TestCaseData(@"
				{
					""v"": [0,1],
					""___d"": {
						""ty"": ""App"",
						""ac"": ""99999123"",
						""ap"": ""51424"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""tk"": ""98765"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577,
						""tx"": ""7721bf09a70bcf7d""
						}
				}", Throws.Exception.TypeOf<DistributedTraceAcceptPayloadParseException>()).SetName("{m}_missing_d_object_throws"),
            new TestCaseData(@"
				{
					""___v"": [0,1],
					""d"": {
						""ty"": ""App"",
						""ac"": ""99999123"",
						""ap"": ""51424"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""tk"": ""98765"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577,
						""tx"": ""7721bf09a70bcf7d""
						}
				}", Throws.Exception.TypeOf<DistributedTraceAcceptPayloadParseException>()).SetName("{m}_missing_v_object_throws"),
            new TestCaseData(@"",
                Throws.Exception.TypeOf<DistributedTraceAcceptPayloadNullException>()).SetName("{m}_empty_json_string_throws"),
            new TestCaseData(null,
                Throws.Exception.TypeOf<DistributedTraceAcceptPayloadNullException>()).SetName("{m}_null_json_string_throws"),
            new TestCaseData(@"
				{
					""v"": [0,999],
					""d"": {
						""ty"": ""App"",
						""ac"": ""99999123"",
						""ap"": ""51424"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""tk"": ""98765"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577,
						""tx"": ""7721bf09a70bcf7d""
						}
				}", new ThrowsNothingConstraint()).SetName("{m}_valid_minor_version_999_nothrow"),
            new TestCaseData($@"
				{{
					""v"": [{DistributedTracePayload.SupportedMajorVersion+1},0],
					""d"": {{
						""ty"": ""App"",
						""ac"": ""99999123"",
						""ap"": ""51424"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""tk"": ""98765"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577,
						""tx"": ""7721bf09a70bcf7d""
						}}
				}}", Throws.Exception.TypeOf<DistributedTraceAcceptPayloadVersionException>()).SetName("{m}_valid_major_version_too_big_throw"),

            new TestCaseData($@"[1,2,3]
				{{
					""v"": [0,1],
					""d"": {{ 
						""ty"": ""App"",
						""ac"": ""99999123"",
						""ap"": ""51424"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""tk"": ""98765"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577,
						""tx"": ""7721bf09a70bcf7d""
						}}
				}}", Throws.Exception.TypeOf<DistributedTraceAcceptPayloadParseException>()).SetName("{m}_valid_json_array_precedes_throw"),
            new TestCaseData($@"""a"":
				{{
					""v"": [0,1],
					""d"": {{ 
						""ty"": ""App"",
						""ac"": ""99999123"",
						""ap"": ""51424"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""tk"": ""98765"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577,
						""tx"": ""7721bf09a70bcf7d""
						}}
				}}", Throws.Exception.TypeOf<DistributedTraceAcceptPayloadParseException>()).SetName("{m}_valid_json_field_precedes_throw"),
        };

        [TestCaseSource(nameof(RequiredOptionalFields))]
        public void DistributedTracePayload_OptionalAndRequired(string json, IConstraint constraint)
        {
            Assert.That(() => DistributedTracePayload.TryBuildIncomingPayloadFromJson(json), constraint);
        }
    }
}
