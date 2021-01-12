// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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
