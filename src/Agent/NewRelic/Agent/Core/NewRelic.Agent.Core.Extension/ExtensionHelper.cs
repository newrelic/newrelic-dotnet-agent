/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace NewRelic.Agent.Core.Extension
{
    public class ExtensionHelper
    {
    }

    public partial class extensionTracerFactoryMatchExactMethodMatcher
    {
        public override string ToString()
        {
            return this.methodName;
        }
    }
}
