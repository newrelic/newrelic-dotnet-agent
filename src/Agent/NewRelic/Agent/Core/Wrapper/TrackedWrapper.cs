using System;
using System.Threading;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper
{
    public class TrackedWrapper
    {
        [NotNull]
        public readonly IWrapper Wrapper;

        private Int32 _numberOfConsecutiveFailures;
        public Int32 NumberOfConsecutiveFailures => _numberOfConsecutiveFailures;

        public TrackedWrapper([NotNull] IWrapper wrapper)
        {
            Wrapper = wrapper;
        }

        public void NoticeSuccess()
        {
            _numberOfConsecutiveFailures = 0;
        }

        public void NoticeFailure()
        {
            Interlocked.Increment(ref _numberOfConsecutiveFailures);
        }
    }
}
