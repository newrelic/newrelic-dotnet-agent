
namespace NewRelic.Agent.UnboundedIntegrationTests.Msmq
{
    internal static class MsmqHelper
    {
        private static int _queueNum = 0;
        private static object _lock = new object();

        // Because the MSMQ tests modify the queue, we can run into concurrency issues if more than one test
        // is running at the same time. To prevent this, give each test a unique queue name based on a number
        internal static int GetNextQueueNum()
        {
            lock (_lock)
            {
                _queueNum++;
                return _queueNum;
            }
        }
    }
}
