// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.ContainerIntegrationTests.ContainerFixtures;

public abstract class ContainerFixture : RemoteApplicationFixture
{
    protected ContainerFixture(ContainerApplication remoteApplication) : base(remoteApplication)
    {
    }

    protected override int MaxTries => 1;
}
