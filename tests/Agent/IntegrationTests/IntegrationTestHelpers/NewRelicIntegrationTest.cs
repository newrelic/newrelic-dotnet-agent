// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public abstract class NewRelicIntegrationTest<TFixture> : IClassFixture<TFixture>
            where TFixture : RemoteApplicationFixture
    {

        public NewRelicIntegrationTest(TFixture fixture)
        {
            fixture.SetTestClassType(this.GetType());
        }
    }
}
