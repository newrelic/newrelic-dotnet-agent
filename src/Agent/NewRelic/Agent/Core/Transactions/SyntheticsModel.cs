using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Core.Transactions
{
	/// <summary>
	/// ASYNC PROJECT NOTE: Consider moving some of these methods in this model to NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Synthetics.SyntheticsHeaderHandler
	/// once the legacy agent is deprecated - this will ensure that we have a lightweight model that has a single responsibility while allowing the SyntheticsHeaderHandler 
	/// to do all of the data processing. 
	/// </summary>
	[JsonConverter(typeof(JsonArrayConverter))]
	public class SyntheticsHeader
	{
		public const int MaxEventCount = 200;
		public const int MaxTraceCount = 20;
		public const string HeaderKey = "X-NewRelic-Synthetics";

		public String EncodingKey;
		public const Int64 SupportedHeaderVersion = 1;

		[JsonArrayIndex(Index = 0)]
		public readonly Int64 Version;

		[JsonArrayIndex(Index = 1)]
		public readonly Int64 AccountId;

		[NotNull]
		[JsonArrayIndex(Index = 2)]
		public readonly String ResourceId;

		[NotNull]
		[JsonArrayIndex(Index = 3)]
		public readonly String JobId;

		[NotNull]
		[JsonArrayIndex(Index = 4)]
		public readonly String MonitorId;

		public SyntheticsHeader(Int64 version, Int64 accountId, [NotNull] String resourceId, [NotNull] String jobId, [NotNull] String monitorId)
		{
			Version = version;
			AccountId = accountId;
			ResourceId = resourceId;
			JobId = jobId;
			MonitorId = monitorId;
		}

		public Boolean IsValidSyntheticsDataForSave()
		{
			return (!string.IsNullOrEmpty(ResourceId) && !string.IsNullOrEmpty(JobId) && !string.IsNullOrEmpty(MonitorId));
		}

		[CanBeNull]
		public static SyntheticsHeader TryCreate(IEnumerable<Int64> trustedAccountIds, String obfuscatedHeader, String encodingKey)
		{
			try
			{
				if (trustedAccountIds == null)
					throw new ArgumentNullException("trustedAccountIds");
				if (obfuscatedHeader == null)
					throw new ArgumentNullException("obfuscatedHeader");
				if (encodingKey == null)
					throw new ArgumentNullException("encodingKey");

				var serializedHeader = Deobfuscate(obfuscatedHeader, encodingKey);
				
				// Manually deserialize the version number first because it is easy to do and if it fails it provides more specific info than a general deserialization failure
				var version = DeserializeVersion(serializedHeader);
				if (IsUnsupportedVersion(version))
					return null;

				var syntheticsHeader = JsonConvert.DeserializeObject<SyntheticsHeader>(serializedHeader);
				if (syntheticsHeader == null)
					throw new JsonSerializationException("Failed to deserialize " + HeaderKey + " header. Expected object but got null");

				syntheticsHeader.EncodingKey = encodingKey;

				if (IsUntrustedAccount(syntheticsHeader, trustedAccountIds))
					return null;

				return syntheticsHeader;
			}
			catch (Exception exception)
			{
				Log.Warn(exception);
				return null;
			}
		}

		[Pure]
		public String TryGetObfuscated()
		{
			try
			{
				var serializedHeader = JsonConvert.SerializeObject(this);
				if (serializedHeader == null)
					throw new JsonSerializationException("Failed to serialize synthetics header.  Expected string out, received null.");

				return Obfuscate(serializedHeader, EncodingKey);
			}
			catch (Exception exception)
			{
				Log.Warn(exception);
				return null;
			}
		}

		[NotNull]
		private static String Obfuscate([NotNull] String serializedHeader, [NotNull] String encodingKey)
		{
			return Strings.Base64Encode(serializedHeader, encodingKey);
		}

		[NotNull]
		private static String Deobfuscate([NotNull] String obfuscatedHeader, [NotNull] String encodingKey)
		{
			return Strings.Base64Decode(obfuscatedHeader, encodingKey);
		}

		private static Int64 DeserializeVersion([NotNull] String jsonSerializedHeader)
		{
			if (jsonSerializedHeader == null)
				throw new ArgumentNullException("jsonSerializedHeader");

			var parsed = JArray.Parse(jsonSerializedHeader);
			if (parsed == null)
				throw new JsonSerializationException("Failed to parse X-NewRelic-Synthetics header as an array: " + jsonSerializedHeader);

			var versionToken = parsed[0];
			if (versionToken == null)
				throw new JsonSerializationException("Failed to get version from first item in X-NewRelic-Synthetics header array: " + jsonSerializedHeader);
			if (versionToken.Type != JTokenType.Integer)
				throw new JsonSerializationException("Failed to parse version as an integer in X-NewRelic-Synthetics header array: " + jsonSerializedHeader);
			return versionToken.ToObject<Int64>();
		}

		/// <summary>
		/// IsUnsupportedVersion: https://source.datanerd.us/agents/agent-specs/blob/master/Synthetics-PORTED.md#verify-version
		// TDDO: evaluate to see if this will need to change for backwards compatibility: i.e.version =< SupportedHeaderVersion
		// if version = 1 & SupportedHeaderVersion = 2 then true
		// if version = 2 & SupportedHeaderVersion = 2 then true
		// if version = 3 & SupportedHeaderVersion = 2 then false
		/// </summary>
		private static Boolean IsUnsupportedVersion(Int64 version)
		{
			return (version != SupportedHeaderVersion);
		}

		/// <summary>
		/// IsUntrustedAccount: https://source.datanerd.us/agents/agent-specs/blob/master/Synthetics-PORTED.md#verify-account-id
		/// </summary>
		private static Boolean IsUntrustedAccount([NotNull] SyntheticsHeader syntheticsHeader, [NotNull] IEnumerable<Int64> trustedAccountIds)
		{
			return !(trustedAccountIds.Contains(syntheticsHeader.AccountId));
		}
	}
}
