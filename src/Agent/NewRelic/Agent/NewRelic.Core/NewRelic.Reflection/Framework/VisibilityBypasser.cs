/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#if NET35
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace NewRelic.Reflection
{

    public class ReflectionImpl : IReflection
    {
        public ConstructorInfo GetConstructor(Type type, Type[] parameterTypes)
        {
            return type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, parameterTypes, null);
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
