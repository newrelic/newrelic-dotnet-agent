// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;

namespace NewRelic.Agent.Core;

public static class AgentInitializer
{
    public static event EventHandler OnExit = (sender, args) => { };

    static AgentInitializer()
    {
        InitializeAgent = () => CallOnce.TouchMe();
    }

    /// <summary>
    /// THIS FIELD SHOULD ONLY BE CHANGED BY UNIT TESTS.
    /// 
    /// This is the one place in our agent where we are capitulating the needs of unit tests by providing functionality that only tests should use.
    /// </summary>
    public static Action InitializeAgent { get; private set; }

    private static class CallOnce
    {
        static CallOnce()
        {
            // we must ensure that we hook up to ProcessExit and DomainUnload *before* log initialization.  Otherwise we can't log anything during OnExit.
            AppDomain.CurrentDomain.ProcessExit += (sender, args) => OnExit(sender, args);
            AppDomain.CurrentDomain.DomainUnload += (sender, args) => OnExit(sender, args);
            LoggerBootstrapper.Initialize();

            // Force agent to be initialized
            var agent = AgentManager.Instance;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public static void TouchMe()
        {
        }
    }
}
