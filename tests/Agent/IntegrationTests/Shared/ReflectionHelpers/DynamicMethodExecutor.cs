// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers
{
    /// <summary>
    /// Provides utilities that allow execution of a method using reflection
    /// </summary>
    public class DynamicMethodExecutor
    {
        private readonly Dictionary<Type, object> _instanceDic = new Dictionary<Type, object>();

        public void ExecuteDynamicMethod(MethodInfo method, string[] args)
        {
            var paramValues = MapParameterValues(method, args);

            object objInst = null;
            if (!method.IsStatic)
            {
                if (!_instanceDic.TryGetValue(method.DeclaringType, out objInst))
                {
                    objInst = Activator.CreateInstance(method.DeclaringType);
                    _instanceDic[method.DeclaringType] = objInst;
                }
            }

            var returnValue = method.Invoke(objInst, paramValues.ToArray());

            if (returnValue is Task task)
            {
                task.Wait();
            }

        }

        public static IEnumerable<object> MapParameterValues(MethodInfo method, string[] args)
        {
            var methodParams = method.GetParameters();
            var result = new object[methodParams.Length];

            for (var i = 0; i < methodParams.Length; i++)
            {
                var paramType = methodParams[i].ParameterType;

                var converter = TypeDescriptor.GetConverter(paramType);
                var val = converter.ConvertFromInvariantString(args[i]);
                result[i] = val;
            }

            return result;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class LibraryAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class LibraryMethodAttribute : Attribute
    {
    }
}
