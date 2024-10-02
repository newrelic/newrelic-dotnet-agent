// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using DiffEngine;
using PublicApiGenerator;

namespace PublicApiChangeTests;

[TestFixture]
public class PublicApiTest
{
    [Test]
    public Task PublicApiHasNotChanged()
    {
        if (BuildServerDetector.Detected) // don't launch the diff engine if the test fails on a build server
            DiffRunner.Disabled = true; 

        // Get the assembly for the library we want to document
        Assembly assembly = typeof(NewRelic.Api.Agent.IAgent).Assembly;

        // Retrieve the public API for all types in the assembly
        string publicApi = assembly.GeneratePublicApi();

        // Run a snapshot test on the returned string
        return Verify(publicApi);
    }
}
