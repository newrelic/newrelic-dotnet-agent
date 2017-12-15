using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Time
{
	[TestFixture]
	public class TimeSpanMathTests
	{
		[Test]
		[TestCase(1, 1, 1)]
		[TestCase(2, 3, 2)]
		[TestCase(3, 2, 2)]
		public void Min_ReturnsMinTime(Int32 seconds1, Int32 seconds2, Int32 expectedSeconds)
		{
			var time1 = TimeSpan.FromSeconds(seconds1);
			var time2 = TimeSpan.FromSeconds(seconds2);
			var expectedTime = TimeSpan.FromSeconds(expectedSeconds);

			var actualTime = TimeSpanMath.Min(time1, time2);

			Assert.AreEqual(expectedTime, actualTime);
		}

		[Test]
		[TestCase(1, 1, 1)]
		[TestCase(2, 3, 3)]
		[TestCase(3, 2, 3)]
		public void Max_ReturnsMaxTime(Int32 seconds1, Int32 seconds2, Int32 expectedSeconds)
		{
			var time1 = TimeSpan.FromSeconds(seconds1);
			var time2 = TimeSpan.FromSeconds(seconds2);
			var expectedTime = TimeSpan.FromSeconds(expectedSeconds);

			var actualTime = TimeSpanMath.Max(time1, time2);

			Assert.AreEqual(expectedTime, actualTime);
		}
	}
}
