// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Dongle.Cryptography;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;

public interface IPathHashMaker
{
    /// <summary>
    /// Calculates and returns a path hash based on the given parameters.
    /// </summary>
    /// <returns>Returns a path hash based on the given parameters.</returns>
    string CalculatePathHash(string transactionName, string referringPathHash);
}

public class PathHashMaker : IPathHashMaker
{
    private const int TotalBits = 32;
    private const string PathHashSeparator = ";";
    public const int AlternatePathHashMaxSize = 10;
    private readonly IConfigurationService _configurationService;

    public PathHashMaker(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public string CalculatePathHash(string transactionName, string referringPathHash)
    {
        // The logic of this method is specced here: https://source.datanerd.us/agents/agent-specs/blob/master/Cross-Application-Tracing-PORTED.md#pathhash

        var appName = _configurationService.Configuration.ApplicationNames.First();
        if (appName == null)
            throw new NullReferenceException(nameof(appName));

        var referringPathHashInt = HexToIntOrZero(referringPathHash);
        var leftShift = (uint)referringPathHashInt << 1;
        var rightShift = (uint)referringPathHashInt >> (TotalBits - 1);
        var hash = GetHash(appName, transactionName);

        var rotatedReferringPathHash = (leftShift) | (rightShift);
        var pathHashInt = (int)(rotatedReferringPathHash ^ hash);
        return IntToHex(pathHashInt);
    }

    // Though currently unused, this function is left in place as a reference in case it is needed in the future.
    private static string ReversePathHash(string transactionName, string appName, string pathHash)
    {
        var pathHashInt = HexToInt(pathHash);
        var rotatedReferringPathHash = pathHashInt ^ GetHash(appName, transactionName);
        var rightShift = (uint)rotatedReferringPathHash >> 1;
        var leftShift = (uint)rotatedReferringPathHash << (TotalBits - 1);

        var reversedPathHashInt = (int)((rightShift) | (leftShift));
        return IntToHex(reversedPathHashInt);
    }

    private static int GetHash(string appName, string txName)
    {
        var md5Hash = new Md5();
        var formattedInput = appName + PathHashSeparator + txName;
        md5Hash.ValueAsByte = Encoding.UTF8.GetBytes(formattedInput);
        var hashBytes = md5Hash.HashAsByteArray;
        var fromBytes = (hashBytes[12]) << 24 | (hashBytes[13]) << 16 | (hashBytes[14]) << 8 | (hashBytes[15]);
        return fromBytes;
    }

    private static int HexToIntOrZero(string val)
    {
        if (val == null)
            return 0;

        return HexToInt(val);
    }

    private static int HexToInt(string val)
    {
        int result;
        if (int.TryParse(val, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result))
            return result;

        return 0;
    }

    private static string IntToHex(int val)
    {
        var hex = val.ToString("x8");
        if (hex == null)
            throw new NullReferenceException("hex");

        return hex;
    }
}
