// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Utilities
{
    /// <summary>
    /// This attribute can be generated using an MSBuild file.
    ///
    /// Example:
    ///
    /// <ItemGroup>
    ///   <AssemblyAttribute Include="NewRelic.Agent.Core.Utilities.BuildTimestamp">
    ///     <_Parameter1>$([System.DateTime]::UtcNow.Ticks)</_Parameter1>
    ///   </AssemblyAttribute>
    /// </ItemGroup>
    ///
    /// This AssemblyAttribute item generates an assembly attribute like so: [assembly: NewRelic.Agent.Core.Utilities.BuildTimestamp("637213171838522390")]
    /// Parameters for the attribute are always strings. There is an open issue requesting support for non-string parameters.
    /// https://github.com/microsoft/msbuild/issues/2281
    ///
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public class BuildTimestampAttribute : Attribute
    {
        public long? BuildTimestamp { get; }

        public BuildTimestampAttribute(string utcTicksAsString)
        {
            // Ideally this constructor would accept a long instead of a string.
            // We are limited to a string by the MSBuild AssemblyAttribute item.
            try
            {
                var ticks = long.Parse(utcTicksAsString);
                BuildTimestamp = new DateTime(ticks, DateTimeKind.Utc).ToUnixTimeMilliseconds();
            }
            catch
            {
                BuildTimestamp = null;
            }
        }
    }
}
