// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Wrapper
{
    [TestFixture]
    public class Class_WrapperTracer
    {
        [Test]
        public void when_finish_is_called_on_tracer_then_finish_is_called_on_wrapper()
        {
            var called = false;
            AfterWrappedMethodDelegate afterWrappedMethodDelegate = (_, __) => { called = true; };
            var wrapperTracer = new WrapperTracer(afterWrappedMethodDelegate);

            wrapperTracer.Finish(null, null);

            Assert.That(called, Is.True);
        }
    }
}
