using System;
using System.Security.Cryptography;

namespace NewRelic.Agent.Core.Utilities
{
    public static class GuidGenerator
    {
	    private static readonly RNGCryptoServiceProvider RngCryptoServiceProvider = new RNGCryptoServiceProvider();

	    /// <summary>
		/// Returns a newrelic style guid.
		/// https://source.datanerd.us/agents/agent-specs/blob/2ad6637ded7ec3784de40fbc88990e06525127b8/Cross-Application-Tracing-PORTED.md#guid
		/// </summary>
		/// <returns></returns>
	    public static String GenerateNewRelicGuid()
	    {
		    var rndBytes = new Byte[8];
		    RngCryptoServiceProvider.GetBytes(rndBytes);
		    return $"{BitConverter.ToUInt64(rndBytes, 0):X16}";
	    }
    }
}
