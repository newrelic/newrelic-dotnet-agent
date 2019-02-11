using System;
using Xunit;

namespace custom_attributes
{
    public class CustomAttributeUnitTest
    {
        private TracerInvocation invoker = new TracerInvocation();

        public CustomAttributeUnitTest()
        {
            NewRelic.Agent.Core.AgentShim.GetTracerDelegate = invoker.GetTracer;
        }

        [Fact]
        public void TestFactoryName()
        {
            OtherTransaction();

            Assert.Equal("NewRelic.Agent.Core.Tracer.Factories.DefaultTracerFactory", invoker.TracerFactoryName);
        }

        [Fact]
        public void TestOtherTransactionTracerArgs()
        {
            OtherTransaction();

            Assert.Equal(1 << 20 | 1 << 22, invoker.TracerArguments);
        }

        [Fact]
        public void TestWebTransactionTracerArgs()
        {
            WebTransaction();

            Assert.Equal(1 << 20 | 1 << 21, invoker.TracerArguments);
        }

        [Fact]
        public void TestNormalTracerTracerArgs()
        {
            TracedMethod();

            Assert.Equal(1 << 20, invoker.TracerArguments);
        }

        [NewRelic.Api.Agent.Transaction]
        static void OtherTransaction()
        {
        }

        [NewRelic.Api.Agent.Transaction(Web=true)]
        static void WebTransaction()
        {
        }

        [NewRelic.Api.Agent.Trace]
        static void TracedMethod()
        {
        }
    }

    
    class TracerInvocation
    {
        public String TracerFactoryName;
        public long TracerArguments;

        public Object GetTracer(String tracerFactoryName, UInt32 tracerArguments, String metricName, String assemblyName, Type type, String typeName, String methodName, String argumentSignature, Object invocationTarget, Object[] args, UInt64 functionId)
        {
            TracerFactoryName = tracerFactoryName;
            TracerArguments = (long)tracerArguments;

            return "";
        }
    }
}
