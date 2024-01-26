// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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
                () => Assert.That(errorData.ErrorMessage, Is.EqualTo("Oh no!")),
                () => Assert.That(errorData.ErrorTypeName, Is.EqualTo("System.Exception")),
                () => Assert.That(string.IsNullOrEmpty(errorData.StackTrace), Is.False),
                () => Assert.That(errorData.NoticedAt > now.AddMinutes(-1) && errorData.NoticedAt < now.AddMinutes(1), Is.True)
            );
        }
    }
}
