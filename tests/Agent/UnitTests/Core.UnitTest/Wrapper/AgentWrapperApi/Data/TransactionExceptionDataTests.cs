/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Errors;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
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
                () => Assert.AreEqual("Oh no!", errorData.ErrorMessage),
                () => Assert.AreEqual("System.Exception", errorData.ErrorTypeName),
                () => Assert.IsFalse(string.IsNullOrEmpty(errorData.StackTrace)),
                () => Assert.IsTrue(errorData.NoticedAt > now.AddMinutes(-1) && errorData.NoticedAt < now.AddMinutes(1))
            );
        }
    }
}
