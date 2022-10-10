// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace ReportBuilder
{
    static public class DotNetMonikerNormalizer
    {
        private const string NetFramework = ".NET Framework 4.5.x to 4.8.x";
        private const string NetStandard = ".NET Standard 2.0, 2.1";
        private const string NetCore = ".NET Core 3.1 and .NET 5.0 to 7.0";

        private static List<string> _netFramework = new List<string>{ "net4" };
        private static List<string> _netStandard = new List<string> { "netstandard2" };
        private static List<string> _netCore = new List<string> { "netcoreapp3", "net5", "net6, net7" };

        public static string GetNormalizedMoniker(string targetFramework)
        {
            if (CheckForFramework(targetFramework))
            {
                return NetFramework;
            }

            if (CheckForStandard(targetFramework))
            {
                return NetStandard;
            }

            if (CheckForCore(targetFramework))
            {
                return NetCore;
            }

            throw new Exception(".NET version not found in list.  Please update the DotNetMonikerNormalizer class to include the missing version: " + targetFramework);
        }

        private static bool CheckForFramework(string targetFramework)
        {
            foreach (var net in _netFramework)
            {
                if (targetFramework.StartsWith(net))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CheckForStandard(string targetFramework)
        {
            foreach (var net in _netStandard)
            {
                if (targetFramework.StartsWith(net))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CheckForCore(string targetFramework)
        {
            foreach (var net in _netCore)
            {
                if (targetFramework.StartsWith(net))
                {
                    return true;
                }
            }

            return false;
        }


    }
}
