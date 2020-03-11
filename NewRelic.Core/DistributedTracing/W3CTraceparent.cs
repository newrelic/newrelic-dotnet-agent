using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NewRelic.Core.DistributedTracing
{
	public class W3CTraceparent
	{
		public const byte SupportedVersion = 0;
		private const int TraceparentLengthV0 = 55;
		private const int VersionLengthV0 = 2;
		private const int TraceIdLengthV0 = 32;
		private const int ParentIdLengthV0 = 16;
		private const int TraceFlagsLengthV0 = 2;
		private const string ZerodOutTraceId = "00000000000000000000000000000000";
		private const string ZerodOutParentId = "0000000000000000";
		private const byte InvalidVersion255 = 255;
		private const string InvalidVersionff = "ff";

		private static readonly char[] _separator = new char[] { '-' };

		// Explicitly does not inlcude uppercase or the IgnoreCase option since W3C requires that treat uppercase as invalid
		// See note here: https://w3c.github.io/trace-context/#parent-id (applies to traceId and parentId)
		private static readonly Regex _hexRegex = new Regex(@"\A\b[0-9a-f]+\b\Z", RegexOptions.Compiled);

		/// <summary>
		/// Version is stored a number (byte) for ease of use, output as 1 byte, 2 character hexidecmimal, and cannot exceed 255.
		/// 
		/// When getting the output, use <code>.ToString("x2")</code> to get the correctly formatted hex string
		/// </summary>
		public byte Version { get; }

		/// <summary>
		/// TraceId is a 16 byte, 32 character string that is restricted to hexidecimal characters
		/// </summary>
		public string TraceId { get; } // 16 bytes

		/// <summary>
		/// ParentId is a 8 byte, 16 character string that is restricted to hexidecimal characters
		/// </summary>
		public string ParentId { get; } // 8 bytes

		/// <summary>
		/// TraceFlags is a 2 character string that represents an 8-bit field that controls tracing flags such as sampling, trace level, etc. 
		/// 
		/// As this is a bit field, you cannot interpret flags by decoding the hex value and looking at the resulting number.
		/// </summary>
		public string TraceFlags { get; } // 2 bytes/8 bits

		/// <summary>
		/// This is used to create the object and expects to only be called with validated values from the two CreateW3CTraceparent builders.
		/// </summary>
		/// <param name="version"></param>
		/// <param name="traceId"></param>
		/// <param name="parentId"></param>
		/// <param name="traceFlags"></param>
		private W3CTraceparent(byte version, string traceId, string parentId, string traceFlags)
		{
			Version = version;
			TraceId = traceId;
			ParentId = parentId;
			TraceFlags = traceFlags;
		}

		/// <summary>
		/// Creates a W3CTraceparentHeader object by parsing the traceparent header value.
		/// </summary>
		/// <param name="traceparentValue">The traceparent header value.</param>
		/// <returns></returns>
		public static W3CTraceparent GetW3CTraceparentFromHeader(string traceparentValue)
		{
			if (string.IsNullOrWhiteSpace(traceparentValue) || traceparentValue.Length < TraceparentLengthV0)
			{
				return null;
			}

			var traceparentData = traceparentValue.Split(_separator);
			if (traceparentData.Length != 4)
			{
				return null;
			}

			if (!TryParseVersion(traceparentData[0], out var parsedVersion)
				|| !ValidateTraceId(traceparentData[1])
				|| !ValidateParentId(traceparentData[2])
				|| !ValidateTraceFlags(traceparentData[3]))
			{
				return null;
			}

			return new W3CTraceparent(
				version: parsedVersion,
				traceId: traceparentData[1],
				parentId: traceparentData[2],
				traceFlags: traceparentData[3]);
		}

		public KeyValuePair<string, string> ToHeaderFormat()
		{
			return new KeyValuePair<string, string>("traceparent", this.ToString());
		}

		public override string ToString() => $"{Version.ToString("x2")}-{TraceId}-{ParentId}-{TraceFlags}";

		private static bool TryParseVersion(string version, out byte parsedVersion)
		{
			if (version.Length != VersionLengthV0
				|| version.Equals(InvalidVersionff, System.StringComparison.InvariantCulture)
				|| !_hexRegex.IsMatch(version))
			{
				parsedVersion = InvalidVersion255; // 255 is invalid, avoiding using a nullable here
				return false;
			}

			if (byte.TryParse(version,
				System.Globalization.NumberStyles.HexNumber,
				System.Globalization.CultureInfo.InvariantCulture,
				out var v))
			{
				parsedVersion = v;
				return true;
			}

			parsedVersion = InvalidVersion255; // 255 is invalid, avoiding using a nullable here
			return false;
		}

		private static bool ValidateTraceId(string traceId)
		{
			if (traceId.Length != TraceIdLengthV0 
				|| traceId == ZerodOutTraceId 
				|| !_hexRegex.IsMatch(traceId))
			{
				return false;
			}

			return true;
		}

		private static bool ValidateParentId(string parentId)
		{
			if (parentId.Length != ParentIdLengthV0
				|| parentId == ZerodOutParentId
				|| !_hexRegex.IsMatch(parentId))
			{
				return false;
			}

			return true;
		}

		private static bool ValidateTraceFlags(string traceFlags)
		{
			if (traceFlags.Length != TraceFlagsLengthV0 || !_hexRegex.IsMatch(traceFlags))
			{
				return false;
			}

			return true;
		}
	}
}
