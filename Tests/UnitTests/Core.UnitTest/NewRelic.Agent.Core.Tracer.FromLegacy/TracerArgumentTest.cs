using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Tracer
{

	[TestFixture]
	public class TracerArgumentTest
	{
		[Test]
		public static void TestTransactionNamingPriority()
		{
			Assert.AreEqual(TransactionNamePriority.Handler, TracerArgument.GetTransactionNamingPriority(0x000012F | (3 << 24)));
			Assert.AreEqual((TransactionNamePriority)7, TracerArgument.GetTransactionNamingPriority(0x0000076 | (7 << 24)));
		}
	}
}
