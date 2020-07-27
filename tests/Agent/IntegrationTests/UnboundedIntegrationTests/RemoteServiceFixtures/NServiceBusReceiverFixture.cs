using System;
using DotNet_Msmq_Shared;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class NServiceBusReceiverFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"NServiceBusReceiverHost";
        private const string ExecutableName = @"NServiceBus.Host.exe";
        private const string TargetFramework = "net452";

        public readonly NServiceBusBasicMvcApplicationFixture SendFixture;

        public NServiceBusReceiverFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Unbounded, false))
        {
            SendFixture = new NServiceBusBasicMvcApplicationFixture();
            SendFixture.DelayKill = true;
            SendFixture.Initialize();

            MessageQueueUtil.CreateEmptyQueue("nservicebusreceiverhost", true);
            MessageQueueUtil.CreateEmptyQueue("nservicebusreceiverhost.error", true);
            MessageQueueUtil.CreateEmptyQueue("nservicebusreceiverhost.retries", true);
            MessageQueueUtil.CreateEmptyQueue("nservicebusreceiverhost.timeouts", true);
            MessageQueueUtil.CreateEmptyQueue("nservicebusreceiverhost.timeoutsdispatcher", true);
        }

        public override void Dispose()
        {
            SendFixture.Dispose();

            base.Dispose();
        }
    }
}
