using System;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Tracer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper
{
    /// <summary>
    /// A simple class which wraps an AfterWrappedMethodDelegate in an ITracer so that it can be easily passed around in a system already designed to handle ITracers
    /// </summary>
    public class WrapperTracer : ITracer
    {
        [NotNull]
        private readonly AfterWrappedMethodDelegate _afterWrappedMethodDelegate;

        public WrapperTracer([NotNull] AfterWrappedMethodDelegate afterWrappedMethodDelegate)
        {
            _afterWrappedMethodDelegate = afterWrappedMethodDelegate;
        }

        public void Finish(Object returnValue, Exception exception)
        {
            _afterWrappedMethodDelegate(returnValue, exception);
        }
    }
}
