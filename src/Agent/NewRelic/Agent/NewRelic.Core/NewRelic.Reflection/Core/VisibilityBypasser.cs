/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#if NETSTANDARD2_0
using System;
using System.Reflection;

namespace NewRelic.Reflection
{
    public class ReflectionImpl : IReflection
    {
        public ConstructorInfo GetConstructor(Type type, Type[] parameterTypes)
        {
            return type.GetConstructor(parameterTypes);
        }
    }

    public class VisibilityBypasser : VisibilityBypasserBase
    {
        public static readonly VisibilityBypasser Instance = new VisibilityBypasser();

        private VisibilityBypasser() : base(new ReflectionImpl())
        {
        }
    }
}
#endif
