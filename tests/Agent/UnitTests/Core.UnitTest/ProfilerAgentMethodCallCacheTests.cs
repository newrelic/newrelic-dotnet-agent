// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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
            var methodReference = ProfilerAgentMethodCallCache.GetInvokerFromCache();
            Assert.IsAssignableFrom(typeof(Func<string, string, string, Type[], Type, object[], object>), methodReference);
        }

        [Test]
        public void ShouldInvokeMethodWithNoParameters()
        {
            var invoker = (Func<string, string, string, Type[], Type, object[], object>)ProfilerAgentMethodCallCache.GetInvokerFromCache();

            var typeName = typeof(TestingClass).AssemblyQualifiedName;
            var methodName = nameof(TestingClass.MethodWithNoParams);
            var cacheKey = string.Concat(typeName, "|", methodName);

            var result1 = invoker.Invoke(cacheKey, typeName, methodName, null, typeof(string), null);
            var result2 = invoker.Invoke(cacheKey, typeName, methodName, Array.Empty<Type>(), typeof(string), null);

            NrAssert.Multiple(
                () => Assert.IsNotNull(result1),
                () => Assert.AreEqual(methodName, result1),
                () => Assert.AreEqual(result1, result2)
            );
        }

        [Test]
        public void ShouldInvokeMethodWithParameters()
        {
            var invoker = (Func<string, string, string, Type[], Type, object[], object>)ProfilerAgentMethodCallCache.GetInvokerFromCache();

            var typeName = typeof(TestingClass).AssemblyQualifiedName;
            var methodName = nameof(TestingClass.MethodWithParams);
            var cacheKey = string.Concat(typeName, "|", methodName);
            var parameterValue = "param_value";

            var result = invoker.Invoke(cacheKey, typeName, methodName, new Type[] { typeof(string) }, typeof(string), new object[] { parameterValue });

            var expectedMethodResult = $"{methodName} + {parameterValue}";

            NrAssert.Multiple(
                () => Assert.IsNotNull(result),
                () => Assert.AreEqual(expectedMethodResult, result)
            );
        }

        [Test]
        public void ShouldInvokeWrongMethodIfCacheKeyNotUniqueEnough()
        {
            var invoker = (Func<string, string, string, Type[], Type, object[], object>)ProfilerAgentMethodCallCache.GetInvokerFromCache();

            var typeName = typeof(TestingClass).AssemblyQualifiedName;
            var methodName = nameof(TestingClass.MethodWithOverload);
            var cacheKey = string.Concat(typeName, "|", methodName);

            var resultWithStringParam = invoker.Invoke(cacheKey, typeName, methodName, new Type[] { typeof(string) }, typeof(string), new object[] { "param" });

            NrAssert.Multiple(
                () => Assert.IsNotNull(resultWithStringParam),
                // The call to the bool parameter overload should fail because the string parameter overload was returned from the cache
                () => Assert.Throws(typeof(InvalidCastException), () => invoker.Invoke(cacheKey, typeName, methodName, new Type[] { typeof(bool) }, typeof(string), new object[] { true }))
            );
        }

        [Test]
        public void ShouldHandleMethodsWithVoidReturnType()
        {
            var invoker = (Func<string, string, string, Type[], Type, object[], object>)ProfilerAgentMethodCallCache.GetInvokerFromCache();

            var typeName = typeof(TestingClass).AssemblyQualifiedName;

            var resultNoParam = invoker.Invoke(string.Concat(typeName, "|", nameof(TestingClass.ActionWithNoParams)), typeName, nameof(TestingClass.ActionWithNoParams), null, null, null);
            var resultWithParam = invoker.Invoke(string.Concat(typeName, "|", nameof(TestingClass.ActionWithParam)), typeName, nameof(TestingClass.ActionWithParam), new Type[] { typeof(int) }, null, new object[] { 5 });

            NrAssert.Multiple(
                () => Assert.IsNull(resultNoParam),
                () => Assert.IsNull(resultWithParam),
                () => Assert.AreEqual(1, TestingClass.ActionWithNoParamsCallCount),
                () => Assert.AreEqual(5, TestingClass.ActionWithParamLatestValue)
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

            public static string MethodWithOverload(bool param1)
            {
                return $"{nameof(MethodWithOverload)} + {param1}";
            }

            public static int ActionWithNoParamsCallCount = 0;
            public static void ActionWithNoParams()
            {
                ActionWithNoParamsCallCount++;
            }

            public static int ActionWithParamLatestValue = 0;
            public static void ActionWithParam(int value)
            {
                ActionWithParamLatestValue = value;
            }
        }
    }
}
