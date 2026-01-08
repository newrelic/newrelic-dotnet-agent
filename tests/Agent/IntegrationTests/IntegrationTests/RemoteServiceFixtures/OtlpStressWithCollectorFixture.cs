// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.IntegrationTests.RemoteServiceFixtures
{
    public abstract class OtlpStressWithCollectorFixtureBase : OtlpMetricsWithCollectorFixtureBase
    {
        public int MeasurementsPerThread { get; set; } = 1000;
        public int ThreadCount { get; set; } = 10;

        protected OtlpStressWithCollectorFixtureBase(string targetFramework, bool isCoreApp) 
            : base(targetFramework, isCoreApp, "OTelMetricsApplication", "OTelMetricsApplication.exe")
        {
        }

        public override void Initialize()
        {
            base.Initialize();
        }
    }

    public class OtlpStressWithCollectorFixtureCoreLatest : OtlpStressWithCollectorFixtureBase
    {
        public OtlpStressWithCollectorFixtureCoreLatest() : base("net10.0", true) { }
    }

    public class OtlpStressWithCollectorFixtureCoreNet8 : OtlpStressWithCollectorFixtureBase
    {
        public OtlpStressWithCollectorFixtureCoreNet8() : base("net8.0", true) { }
    }

    public class OtlpStressWithCollectorFixtureFW472 : OtlpStressWithCollectorFixtureBase
    {
        public OtlpStressWithCollectorFixtureFW472() : base("net472", false) { }
    }

    public class OtlpStressWithCollectorFixtureFW481 : OtlpStressWithCollectorFixtureBase
    {
        public OtlpStressWithCollectorFixtureFW481() : base("net481", false) { }
    }
}
