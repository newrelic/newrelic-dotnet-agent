/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace FunctionalTests.Attributes
{
    [Serializable]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class DoesNotRunOn32BitOS : Attribute, ITestAction
    {
        public void BeforeTest(ITest details)
        {
            if (Common.TestServerContainer[details.Fixture.ToString()].ProcessorArchitecture != "x64")
            {
                Assert.Ignore("This test is not valid on 32 bit systems.");
            }
        }

        public void AfterTest(ITest details)
        {
            return;
        }

        public ActionTargets Targets
        {
            get { return ActionTargets.Test; }
        }
    }
}
