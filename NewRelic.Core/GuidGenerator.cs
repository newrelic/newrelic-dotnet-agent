using System;
using System.Security.Cryptography;

namespace NewRelic.Core
{
	public static class GuidGenerator
	{
		/// The research in the DOTNET-3423 story shows that RngCryptoServiceProvider
		/// library is threadsafe and is more performant than Random library when generating
		/// random numbers during thread contention.
		private static readonly RNGCryptoServiceProvider RngCryptoServiceProvider = new RNGCryptoServiceProvider();

		/// <summary>
		/// Returns a newrelic style guid.
		/// https://source.datanerd.us/agents/agent-specs/blob/2ad6637ded7ec3784de40fbc88990e06525127b8/Cross-Application-Tracing-PORTED.md#guid
		/// </summary>
		/// <returns></returns>
		public static string GenerateNewRelicGuid()
		{
			var rndBytes = new byte[8];
			RngCryptoServiceProvider.GetBytes(rndBytes);
			return $"{BitConverter.ToUInt64(rndBytes, 0):x16}";
		}

		public static string GenerateNewRelicTraceId()
		{
			var rndBytes = new byte[16];
			RngCryptoServiceProvider.GetBytes(rndBytes);

			var firstHalf = new byte[8];
			var secondHalf = new byte[8];

			Array.Copy(rndBytes, 0, firstHalf, 0, 8);
			Array.Copy(rndBytes, 8, secondHalf, 0, 8);

			return $"{BitConverter.ToUInt64(firstHalf, 0):x16}{BitConverter.ToUInt64(secondHalf, 0):x16}";
		}
	}
}
