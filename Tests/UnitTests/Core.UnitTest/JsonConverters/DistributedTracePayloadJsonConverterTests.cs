using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.DistributedTracing;
using Newtonsoft.Json;
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
			[Values("12345")] string account,
			[Values("1111")] string app,
			[Random(0ul, 0xfffffffffffffffful, 1), Values(ulong.MinValue, ulong.MaxValue)] ulong parentId,
			[Random(0ul, 0xfffffffffffffffful, 1), Values(ulong.MinValue, ulong.MaxValue)] ulong guid,
			[Random(0ul, 0xfffffffffffffffful, 1), Values(ulong.MinValue, ulong.MaxValue)] ulong traceId,
			[Random(0f, 1.0f, 1), Values(0.0f, 1.0f, 0.1234567f)] float priority,
			[Values(true, false)] bool sampled,
			[Values(new[] { 1970, 1, 1, 0, 0, 0 }, new[] { 2018, 12, 31, 23, 59, 59 })] int[] time)
		{
			var input = new DistributedTracePayload(type, account, app, $"{parentId:X8}", $"{guid:X8}", $"{traceId:X8}", priority, sampled, 
				new DateTime(time[0], time[1], time[2], time[3], time[4], time[5], DateTimeKind.Utc));
			var serialized = DistributedTracePayload.ToJson(input);
			var deserialized = DistributedTracePayload.FromJson(serialized);
			Assert.That(deserialized.Version, Is.Not.Null);
			Assert.That(deserialized.Version, Has.Exactly(2).Items);
			Assert.That(deserialized.Version[0], Is.EqualTo(0));
			Assert.That(deserialized.Version[1], Is.EqualTo(1));

			Assert.That(deserialized.Type, Is.EqualTo(input.Type));
			Assert.That(deserialized.Account, Is.EqualTo(input.Account));
			Assert.That(deserialized.App, Is.EqualTo(input.App));
			Assert.That(deserialized.ParentId, Is.EqualTo(input.ParentId));
			Assert.That(deserialized.Guid, Is.EqualTo(input.Guid));
			Assert.That(deserialized.Priority, Is.EqualTo(input.Priority));
			Assert.That(deserialized.Sampled, Is.EqualTo(input.Sampled));
			Assert.That(deserialized.Time, Is.EqualTo(input.Time));
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
						""pa"": ""5fa3c01498e244a6"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577
						}
				}", new ThrowsNothingConstraint()).SetName("{m}_valid_nothrow"),
			new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""ty"": ""App"",
						""ac"": ""9123"",            
						""ap"": ""51424"",
						""___pa"": ""5fa3c01498e244a6"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577
						}
				}", new ThrowsNothingConstraint()).SetName("{m}_missing_parentid_nothrow"),
			new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""ty"": ""App"",
						""ac"": ""9123"",            
						""ap"": ""51424"",
						""pa"": ""5fa3c01498e244a6"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""___pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577
						}
				}", new ThrowsNothingConstraint()).SetName("{m}_missing_priority_nothrow"),
			new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""ty"": ""App"",
						""ac"": ""9123"",            
						""ap"": ""51424"",
						""pa"": ""5fa3c01498e244a6"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""pr"": 0.1234,
						""___sa"": false,
						""ti"": 1482959525577
						}
				}", new ThrowsNothingConstraint()).SetName("{m}_missing_sampled_nothrow"),

			new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""___ty"": ""App"",
						""ac"": ""9123"",            
						""ap"": ""51424"",
						""pa"": ""5fa3c01498e244a6"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577
						}
				}", Throws.Exception.TypeOf<JsonException>()).SetName("{m}_missing_type_throws"),
			new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""ty"": ""App"",
						""___ac"": ""9123"",            
						""ap"": ""51424"",
						""pa"": ""5fa3c01498e244a6"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577
						}
				}", Throws.Exception.TypeOf<JsonException>()).SetName("{m}_missing_account_throws"),
			new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""ty"": ""App"",
						""ac"": ""9123"",            
						""___ap"": ""51424"",
						""pa"": ""5fa3c01498e244a6"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577
						}
				}", Throws.Exception.TypeOf<JsonException>()).SetName("{m}_missing_appid_throws"),
			new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""ty"": ""App"",
						""ac"": ""9123"",            
						""ap"": ""51424"",
						""pa"": ""5fa3c01498e244a6"",
						""___id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""pr"": 0.1234,
						""sa"": false,
						""ti"": 1482959525577
						}
				}", Throws.Exception.TypeOf<JsonException>()).SetName("{m}_missing_guid_throws"),
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
				}", Throws.Exception.TypeOf<JsonException>()).SetName("{m}_missing_traceid_throws"),
			new TestCaseData(@"
				{
					""v"": [0,1],
					""d"": {
						""ty"": ""App"",
						""ac"": ""9123"",            
						""ap"": ""51424"",
						""pa"": ""5fa3c01498e244a6"",
						""id"": ""27856f70d3d314b7"",
						""tr"": ""3221bf09aa0bcf0d"",
						""pr"": 0.1234,
						""sa"": false,
						""___ti"": 1482959525577
						}
				}", Throws.Exception.TypeOf<JsonException>()).SetName("{m}_missing_timestamp_throws"),
		};

		[TestCaseSource(nameof(RequiredOptionalFields))]
		public void DistributedTracePayload_OptionalAndRequired(string json, IConstraint constraint)
		{
			Assert.That(() => DistributedTracePayload.FromJson(json), constraint);
		}
	}
	}
