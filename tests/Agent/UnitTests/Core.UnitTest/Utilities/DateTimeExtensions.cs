using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilities
{
    [TestFixture]
    public class Class_DateTimeExtensions
    {
        [Test]
        public void DoubleToDateTime_ReturnsCorrectDateTime()
        {
            var dateTime = (-23890247268d).ToDateTime();

            Assert.AreEqual(new DateTime(1212, 12, 12, 12, 12, 12), dateTime);
        }

        [Test]
        public void DateTimeToUnixTime_ReturnsCorrectUnixTime()
        {
            var unixTime = new DateTime(1212, 12, 12, 12, 12, 12).ToUnixTimeSeconds();

            Assert.AreEqual(-23890247268d, unixTime);
        }
    }
}
