using System;
using NewRelic.Agent.Core.Errors;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data
{
    [TestFixture]
    public class TransactionExceptionDataTests
    {
        [Test]
        public void FromException_GeneratesCorrectTransactionExceptionData()
        {
            var now = DateTime.UtcNow;

            Exception ex;
            try
            {
                throw new Exception("Oh no!");
            }
            catch (Exception e)
            {
                ex = e;
            }

            var errorData = ErrorData.FromException(ex, false);

            NrAssert.Multiple(
                () => Assert.AreEqual("Oh no!", errorData.ErrorMessage),
                () => Assert.AreEqual("System.Exception", errorData.ErrorTypeName),
                () => Assert.IsFalse(string.IsNullOrEmpty(errorData.StackTrace)),
                () => Assert.IsTrue(errorData.NoticedAt > now.AddMinutes(-1) && errorData.NoticedAt < now.AddMinutes(1))
            );
        }
    }
}
