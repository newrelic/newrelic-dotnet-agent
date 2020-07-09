﻿using System;

namespace NewRelic.SystemExtensions
{
	public static class TimeSpanExtensions
	{
		/// <summary>
		/// Adds <paramref name="timeSpan"/> to itself <paramref name="multiplier"/> times and returns the result as a TimeSpan.
		/// </summary>
		/// <param name="timeSpan">The timespan to add repeatedly.</param>
		/// <param name="multiplier">The number of times to add <paramref name="timeSpan"/> to itself.</param>
		/// <returns>The sum of <paramref name="timeSpan"/> added <paramref name="multiplier"/> times.</returns>
		public static TimeSpan Multiply(this TimeSpan timeSpan, Int64 multiplier)
		{
			return TimeSpan.FromTicks(timeSpan.Ticks * multiplier);
		}

		/// <summary>
		/// Adds <paramref name="timeSpan"/> to itself <paramref name="multiplier"/> times and returns the result as a TimeSpan.  If multiplier is a rational number, then it will be added a number of times equal to Math.Floor(<paramref name="multiplier"/>) and then the quotient of <paramref name="timeSpan"/> divided by the remainder will be added.
		/// </summary>
		/// <param name="timeSpan">The timespan to add repeatedly.</param>
		/// <param name="multiplier">The number of times to add <paramref name="timeSpan"/> to itself.  Can be rational!</param>
		/// <returns>The sum of <paramref name="timeSpan"/> added <paramref name="multiplier"/> times.</returns>
		public static TimeSpan Multiply(this TimeSpan timeSpan, Double multiplier)
		{
			return TimeSpan.FromTicks((Int64)(timeSpan.Ticks * multiplier));
		}

		/// <summary>
		/// Equivelant to TimeSpan.FromSeconds(Double value) except works with nullable doubles and time spans.
		/// </summary>
		/// <param name="value">A span of time in seconds stored in a floating point value, or null.</param>
		/// <returns>A span of time stored in a TimeSpan, or null if <paramref name="value"/> is null.</returns>
		public static TimeSpan? FromSeconds(Double? value)
		{
			if (value.HasValue)
			{
				return TimeSpan.FromSeconds((double)value);
			}

			return null;
		}
	}
}
