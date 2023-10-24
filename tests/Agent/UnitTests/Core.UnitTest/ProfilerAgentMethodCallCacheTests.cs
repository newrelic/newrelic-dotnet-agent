// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System;
using NUnit.Framework;
using NewRelic.Testing.Assertions;

namespace NewRelic.Agent.Core
{
    [TestFixture]
    public class ProfilerAgentMethodCallCacheTests
    {
        [Test]
        public void GetMethodCacheFuncShouldReturnAFuncAsAnObject()
        {
            var methodReference = ProfilerAgentMethodCallCache.GetMethodCacheFunc();
            Assert.IsAssignableFrom(typeof(Func<string, string, string, Type[], MethodInfo>), methodReference);
        }

        [Test]
        public void ShouldGetMethodWithNoParameters()
        {
            var cacheGetter = (Func<string, string, string, Type[], MethodInfo>)ProfilerAgentMethodCallCache.GetMethodCacheFunc();

            var typeName = typeof(TestingClass).AssemblyQualifiedName;
            var methodName = nameof(TestingClass.MethodWithNoParams);
            var cacheKey = string.Concat(typeName, "|", methodName);

            var methodInfoFromCache = cacheGetter.Invoke(cacheKey, typeName, methodName, null);

            NrAssert.Multiple(
                () => Assert.IsNotNull(methodInfoFromCache),
                () => Assert.AreEqual(methodName, methodInfoFromCache.Invoke(null, null))
            );
        }

        [Test]
        public void ShouldGetMethodWithParameters()
        {
            var cacheGetter = (Func<string, string, string, Type[], MethodInfo>)ProfilerAgentMethodCallCache.GetMethodCacheFunc();

            var typeName = typeof(TestingClass).AssemblyQualifiedName;
            var methodName = nameof(TestingClass.MethodWithParams);
            var cacheKey = string.Concat(typeName, "|", methodName);

            var methodInfoFromCache = cacheGetter.Invoke(cacheKey, typeName, methodName, new Type[] { typeof(string) });

            var expectedMethodResult = $"{methodName} + param_value";

            NrAssert.Multiple(
                () => Assert.IsNotNull(methodInfoFromCache),
                () => Assert.AreEqual(expectedMethodResult, methodInfoFromCache.Invoke(null, new object[] { "param_value" }))
            );
        }

        [Test]
        public void ShouldGetWrongMethodIfCacheKeyNotUniqueEnough()
        {
            var cacheGetter = (Func<string, string, string, Type[], MethodInfo>)ProfilerAgentMethodCallCache.GetMethodCacheFunc();

            var typeName = typeof(TestingClass).AssemblyQualifiedName;
            var methodName = nameof(TestingClass.MethodWithOverload);
            var cacheKey = string.Concat(typeName, "|", methodName);

            var overloadWith1Param = cacheGetter.Invoke(cacheKey, typeName, methodName, new Type[] { typeof(string) });
            var overloadWith2Params = cacheGetter.Invoke(cacheKey, typeName, methodName, new Type[] { typeof(string), typeof(string) });

            NrAssert.Multiple(
                () => Assert.IsNotNull(overloadWith1Param),
                () => Assert.IsNotNull(overloadWith2Params),
                () => Assert.AreEqual(overloadWith2Params, overloadWith1Param),
                // The call to the 2 parameter overload should fail because the 1 parameter overload was returned from the cache
                () => Assert.Throws(typeof(TargetParameterCountException), () => overloadWith2Params.Invoke(null, new object[] { "param_1", "param_2" }))
            );
        }

        public class TestingClass
        {
            public static string MethodWithNoParams()
            {
                return nameof(MethodWithNoParams);
            }

            public static string MethodWithParams(string param)
            {
                return $"{nameof(MethodWithParams)} + {param}";
            }

            public static string MethodWithOverload(string param)
            {
                return $"{nameof(MethodWithOverload)} + {param}";
            }

            public static string MethodWithOverload(string param1, string param2)
            {
                return $"{nameof(MethodWithOverload)} + {param1} + {param2}";
            }
        }
    }
}
