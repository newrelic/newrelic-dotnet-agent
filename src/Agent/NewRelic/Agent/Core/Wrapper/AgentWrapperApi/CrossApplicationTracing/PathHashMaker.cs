using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Dongle.Cryptography;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing
{
    public interface IPathHashMaker
    {
        /// <summary>
        /// Calculates and returns a path hash based on the given parameters.
        /// </summary>
        /// <returns>Returns a path hash based on the given parameters.</returns>
        String CalculatePathHash(String transactionName, String referringPathHash);
    }

    public class PathHashMaker : IPathHashMaker
    {
        private const Int32 TotalBits = 32;
        private const String PathHashSeparator = ";";
        public const Int32 AlternatePathHashMaxSize = 10;
        private readonly IConfigurationService _configurationService;

        public PathHashMaker(IConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        public String CalculatePathHash(String transactionName, String referringPathHash)
        {
            // The logic of this method is specced here: https://source.datanerd.us/agents/agent-specs/blob/master/Cross-Application-Tracing-PORTED.md#pathhash

            var appName = _configurationService.Configuration.ApplicationNames.First();
            if (appName == null)
                throw new NullReferenceException(nameof(appName));

            var referringPathHashInt = HexToIntOrZero(referringPathHash);
            var leftShift = (UInt32)referringPathHashInt << 1;
            var rightShift = (UInt32)referringPathHashInt >> (TotalBits - 1);
            var hash = GetHash(appName, transactionName);

            var rotatedReferringPathHash = (leftShift) | (rightShift);
            var pathHashInt = (Int32)(rotatedReferringPathHash ^ hash);
            return IntToHex(pathHashInt);
        }

        // Though currently unused, this function is left in place as a reference in case it is needed in the future.
        private static String ReversePathHash(String transactionName, String appName, String pathHash)
        {
            var pathHashInt = HexToInt(pathHash);
            var rotatedReferringPathHash = pathHashInt ^ GetHash(appName, transactionName);
            var rightShift = (UInt32)rotatedReferringPathHash >> 1;
            var leftShift = (UInt32)rotatedReferringPathHash << (TotalBits - 1);

            var reversedPathHashInt = (Int32)((rightShift) | (leftShift));
            return IntToHex(reversedPathHashInt);
        }

        private static Int32 GetHash(String appName, String txName)
        {
            var md5Hash = new MD5();
            var formattedInput = appName + PathHashSeparator + txName;
            md5Hash.ValueAsByte = Encoding.UTF8.GetBytes(formattedInput);
            var hashBytes = md5Hash.HashAsByteArray;
            var fromBytes = (hashBytes[12]) << 24 | (hashBytes[13]) << 16 | (hashBytes[14]) << 8 | (hashBytes[15]);
            return fromBytes;
        }

        private static Int32 HexToIntOrZero(String val)
        {
            if (val == null)
                return 0;

            return HexToInt(val);
        }

        private static Int32 HexToInt(String val)
        {
            Int32 result;
            if (Int32.TryParse(val, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result))
                return result;

            return 0;
        }
        private static String IntToHex(Int32 val)
        {
            var hex = val.ToString("x8");
            if (hex == null)
                throw new NullReferenceException("hex");

            return hex;
        }
    }
}
