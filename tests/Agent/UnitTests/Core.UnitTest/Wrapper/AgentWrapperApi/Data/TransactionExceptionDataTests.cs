// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Errors;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data
{
    [TestFixture]
    public class TransactionExceptionDataTests
    {
        [Test]
        public void FromException_GeneratesCorrectTransactionExceptionData()
        {
            IErrorService errorService = new ErrorService(Mock.Create<IConfigurationService>());
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

            var errorData = errorService.FromException(ex);

            NrAssert.Multiple(
                () => ClassicAssert.AreEqual("Oh no!", errorData.ErrorMessage),
                () => ClassicAssert.AreEqual("System.Exception", errorData.ErrorTypeName),
                () => ClassicAssert.IsFalse(string.IsNullOrEmpty(errorData.StackTrace)),
                () => ClassicAssert.IsTrue(errorData.NoticedAt > now.AddMinutes(-1) && errorData.NoticedAt < now.AddMinutes(1))
            );
        }
    }
}
