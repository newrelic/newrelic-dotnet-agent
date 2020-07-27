using System;

namespace NewRelic.Agent.Core
{
    public enum AgentState : byte
    {
        Uninitialized = 0,
        Starting = 1,
        Started = 2,
        Stopping = 3,
        Stopped = 4
    }

    public static class AgentStateHelper
    {
        public static void CheckTransition(AgentState from, AgentState to)
        {
            if (from + 1 != to)
                throw new AgentStateException(String.Format("Invalid agent state transition from {0} to  {1}", from, to));
        }

        public static AgentState Transition(AgentState from, AgentState to)
        {
            CheckTransition(from, to);
            return to;
        }
    }

    /// <summary>
    /// An exception to be thrown when there's some fault in the agent state machine transition.
    /// </summary>
    public class AgentStateException : Exception
    {
        public AgentStateException(String message)
            : base(message)
        {
        }
    }

}
