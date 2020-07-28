using NUnit.Framework;

namespace NewRelic.Parsing
{

    [TestFixture]
    public class StringsHelperTest
    {
        [Test]
        public void DoubleBracket()
        {
            Assert.AreEqual("dude", StringsHelper.RemoveBracketsQuotesParenthesis("[[dude]]"));
        }
    }
}
