// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class MSMQBasicMVCApplicationFixture : RemoteApplicationFixture
    {
        public const string ExpectedTransactionName = @"WebTransaction/MVC/DefaultController/Index";

        public MSMQBasicMVCApplicationFixture() : base(new RemoteWebApplication("MSMQBasicMvcApplication", ApplicationType.Unbounded))
        {
        }

        #region MSMQController Actions

        public void GetMessageQueue_Msmq_Send(bool ignoreThisTransaction)
        {
            var address = $"http://{DestinationServerName}:{Port}/MSMQ/Msmq_Send?ignoreThisTransaction={ignoreThisTransaction}";

            GetStringAndAssertIsNotNull(address);
        }

        public void GetMessageQueue_Msmq_Receive()
        {
            var address = $"http://{DestinationServerName}:{Port}/MSMQ/Msmq_Receive";

            GetStringAndAssertIsNotNull(address);
        }

        public void GetMessageQueue_Msmq_Peek()
        {
            var address = $"http://{DestinationServerName}:{Port}/MSMQ/Msmq_Peek";

            GetStringAndAssertIsNotNull(address);
        }

        public void GetMessageQueue_Msmq_Purge()
        {
            var address = $"http://{DestinationServerName}:{Port}/MSMQ/Msmq_Purge";

            GetStringAndAssertIsNotNull(address);
        }

        #endregion
    }
}
