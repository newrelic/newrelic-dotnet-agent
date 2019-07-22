using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NewRelic.Core.Logging;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.Commands
{
	public class ThreadProfilerCommandArgs
	{
		public const Single MinimumSamplingFrequencySeconds = 0.1F;   // 1/10 of a second
		public const Single MinimumSamplingDurationSeconds = 120;       // 2 minutes

		public const Single MaximumSamplingFrequencySeconds = 60;       // 1 minute
		public const Single MaximumSamplingDurationSeconds = 86400;     // 24 hours

		public const Single DefaultSamplingFrequencySeconds = MinimumSamplingDurationSeconds;
		public const Single DefaultSamplingDurationSeconds = MinimumSamplingDurationSeconds;     // 2 minutes

		public readonly Int32 ProfileId;
		public readonly UInt32 Frequency;
		public readonly UInt32 Duration;
		public readonly Boolean ReportData;

		public ThreadProfilerCommandArgs([NotNull] IDictionary<String, Object> arguments)
		{
			var profileId = arguments.GetValueOrDefault("profile_id");
			if (profileId != null)
				Int32.TryParse(profileId.ToString(), out ProfileId);

			Frequency = ParseFloatArgument(arguments, "sample_period", DefaultSamplingFrequencySeconds, MinimumSamplingFrequencySeconds, MaximumSamplingFrequencySeconds);

			Duration = ParseFloatArgument(arguments, "duration", DefaultSamplingDurationSeconds, MinimumSamplingDurationSeconds, MaximumSamplingDurationSeconds);
			
			ReportData = ParseBooleanArgument(arguments, "report_data", true);
		}

		private UInt32 ParseFloatArgument([NotNull] IDictionary<String, Object> arguments, [NotNull] String argumentName, Single defaultValue, Single minValue, Single maxValue)
		{
			Object value;
			if (!arguments.TryGetValue(argumentName, out value) || value == null)
				return (UInt32)defaultValue*1000;

			Single parsedValue;
			if (!Single.TryParse(value.ToString(), out parsedValue))
				return (UInt32)defaultValue*1000;

			try
			{
				if (parsedValue == 0)
					parsedValue = defaultValue;
				else if (parsedValue < minValue)
					parsedValue = minValue;
				else if (parsedValue > maxValue)
					parsedValue = maxValue;

				return (UInt32)(parsedValue*1000);
			}
			catch (OverflowException)
			{
				Log.DebugFormat("Received a sample_period value with start_profiler command that caused an overflow converting to milliseconds. value = {0}", parsedValue);
				return (UInt32)maxValue*1000;
			}
		}

		private Boolean ParseBooleanArgument([NotNull] IDictionary<String, Object> arguments, [NotNull] String argumentName, Boolean defaultValue)
		{
			var value = arguments.GetValueOrDefault(argumentName);
			if (value == null)
				return defaultValue;

			Boolean result;
			if (!Boolean.TryParse(value.ToString(), out result))
				return defaultValue;

			return result;
		}
	}
}
