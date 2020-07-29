/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core
{
    [TestFixture]
    public class AgentStateTest
    {

        [Test]
        static public void TestValidTransition()
        {
            AgentState state = AgentState.Uninitialized;
            state = AgentStateHelper.Transition(state, AgentState.Starting);
            state = AgentStateHelper.Transition(state, AgentState.Started);
            state = AgentStateHelper.Transition(state, AgentState.Stopping);
            state = AgentStateHelper.Transition(state, AgentState.Stopped);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes"), Test]
        static public void TestInvalidTransition1()
        {
            AgentState state = AgentState.Uninitialized;
            try
            {
                state = AgentStateHelper.Transition(state, AgentState.Started);
                Assert.Fail();
            }
            catch (Exception ex)
            {
                Assert.AreEqual("Invalid agent state transition from Uninitialized to  Started", ex.Message);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes"), Test]
        static public void TestInvalidTransition2()
        {
            AgentState state = AgentState.Uninitialized;
            state = AgentStateHelper.Transition(state, AgentState.Starting);
            try
            {
                state = AgentStateHelper.Transition(state, AgentState.Uninitialized);
                Assert.Fail();
            }
            catch (Exception ex)
            {
                Assert.AreEqual("Invalid agent state transition from Starting to  Uninitialized", ex.Message);
            }
        }

    }
}
