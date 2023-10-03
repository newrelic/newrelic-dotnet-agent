// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Core.Logging;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.Commands
{
    public class ThreadProfilerCommandArgs
    {
        private bool _ignoreMinMinimumSamplingDuration;

        public const float MinimumSamplingFrequencySeconds = 0.1F;   // 1/10 of a second
        public const float MinimumSamplingDurationSeconds = 120;       // 2 minutes

        public const float MaximumSamplingFrequencySeconds = 60;       // 1 minute
        public const float MaximumSamplingDurationSeconds = 86400;     // 24 hours

        public const float DefaultSamplingFrequencySeconds = MinimumSamplingDurationSeconds;
        public const float DefaultSamplingDurationSeconds = MinimumSamplingDurationSeconds;     // 2 minutes

        public readonly int ProfileId;
        public readonly uint Frequency;
        public readonly uint Duration;
        public readonly bool ReportData;

        public ThreadProfilerCommandArgs(IDictionary<string, object> arguments, bool ignoreMinMinimumSamplingDuration)
        {
            _ignoreMinMinimumSamplingDuration = ignoreMinMinimumSamplingDuration;

            var profileId = arguments.GetValueOrDefault("profile_id");
            if (profileId != null)
                int.TryParse(profileId.ToString(), out ProfileId);

            Frequency = ParseFloatArgument(arguments, "sample_period", DefaultSamplingFrequencySeconds, MinimumSamplingFrequencySeconds, MaximumSamplingFrequencySeconds);

            Duration = ParseFloatArgument(arguments, "duration", DefaultSamplingDurationSeconds, ignoreMinMinimumSamplingDuration ? 0 : MinimumSamplingDurationSeconds, MaximumSamplingDurationSeconds);

            ReportData = ParseBooleanArgument(arguments, "report_data", true);
        }

        private uint ParseFloatArgument(IDictionary<string, object> arguments, string argumentName, float defaultValue, float minValue, float maxValue)
        {
            object value;
            if (!arguments.TryGetValue(argumentName, out value) || value == null)
                return (uint)defaultValue * 1000;

            float parsedValue;
            if (!float.TryParse(value.ToString(), out parsedValue))
                return (uint)defaultValue * 1000;

            try
            {
                if (parsedValue == 0)
                    parsedValue = defaultValue;
                else if (parsedValue < minValue)
                    parsedValue = minValue;
                else if (parsedValue > maxValue)
                    parsedValue = maxValue;

                return (uint)(parsedValue * 1000);
            }
            catch (OverflowException)
            {
                Log.Debug("Received a sample_period value with start_profiler command that caused an overflow converting to milliseconds. value = {0}", parsedValue);
                return (uint)maxValue * 1000;
            }
        }

        private bool ParseBooleanArgument(IDictionary<string, object> arguments, string argumentName, bool defaultValue)
        {
            var value = arguments.GetValueOrDefault(argumentName);
            if (value == null)
                return defaultValue;

            bool result;
            if (!bool.TryParse(value.ToString(), out result))
                return defaultValue;

            return result;
        }
    }
}
