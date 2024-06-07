// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core
{
    [TestFixture]
    public class ProfilerAgentMethodInvokerTests
    {
        [Test]
        public void GetMethodCacheFuncShouldReturnAFuncAsAnObject()
        {
            var methodReference = ProfilerAgentMethodInvoker.GetInvoker();
            Assert.That(methodReference, Is.AssignableFrom(typeof(Func<string, string, string, Type[], Type, object[], object>)));
        }

        [Test]
        public void ShouldInvokeMethodWithNoParameters()
        {
            var invoker = (Func<string, string, string, Type[], Type, object[], object>)ProfilerAgentMethodInvoker.GetInvoker();

            var typeName = typeof(TestingClass).AssemblyQualifiedName;
            var methodName = nameof(TestingClass.MethodWithNoParams);
            var cacheKey = string.Concat(typeName, "|", methodName);

            var result1 = invoker.Invoke(cacheKey, typeName, methodName, null, typeof(string), null);
            var result2 = invoker.Invoke(cacheKey, typeName, methodName, Array.Empty<Type>(), typeof(string), null);

            Assert.Multiple(() =>
            {
                Assert.That(result1, Is.Not.Null);
                Assert.That(result2, Is.EqualTo(methodName));
                Assert.That(result1, Is.EqualTo(result2));
            });
        }

        [Test]
        public void ShouldInvokeMethodWithParameters()
        {
            var invoker = (Func<string, string, string, Type[], Type, object[], object>)ProfilerAgentMethodInvoker.GetInvoker();

            var typeName = typeof(TestingClass).AssemblyQualifiedName;
            var methodName = nameof(TestingClass.MethodWithParams);
            var cacheKey = string.Concat(typeName, "|", methodName);
            var parameterValue = "param_value";

            var result = invoker.Invoke(cacheKey, typeName, methodName, new Type[] { typeof(string) }, typeof(string), new object[] { parameterValue });

            var expectedMethodResult = $"{methodName} + {parameterValue}";

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.EqualTo(expectedMethodResult));
            });
        }

        [Test]
        public void ShouldInvokeWrongMethodIfCacheKeyNotUniqueEnough()
        {
            var invoker = (Func<string, string, string, Type[], Type, object[], object>)ProfilerAgentMethodInvoker.GetInvoker();

            var typeName = typeof(TestingClass).AssemblyQualifiedName;
            var methodName = nameof(TestingClass.MethodWithOverload);
            var cacheKey = string.Concat(typeName, "|", methodName);

            var resultWithStringParam = invoker.Invoke(cacheKey, typeName, methodName, new Type[] { typeof(string) }, typeof(string), new object[] { "param" });

            Assert.Multiple(() => {
                Assert.That(resultWithStringParam, Is.Not.Null);
                // The call to the bool parameter overload should fail because the string parameter overload was returned from the cache
                Assert.Throws(typeof(InvalidCastException), () => invoker.Invoke(cacheKey, typeName, methodName, [typeof(bool)], typeof(string), [true]));
            });
        }

        [Test]
        public void ShouldHandleMethodsWithVoidReturnType()
        {
            var invoker = (Func<string, string, string, Type[], Type, object[], object>)ProfilerAgentMethodInvoker.GetInvoker();

            var typeName = typeof(TestingClass).AssemblyQualifiedName;

            var resultNoParam = invoker.Invoke(string.Concat(typeName, "|", nameof(TestingClass.ActionWithNoParams)), typeName, nameof(TestingClass.ActionWithNoParams), null, null, null);
            var resultWithParam = invoker.Invoke(string.Concat(typeName, "|", nameof(TestingClass.ActionWithParam)), typeName, nameof(TestingClass.ActionWithParam), new Type[] { typeof(int) }, null, new object[] { 5 });

            Assert.Multiple(() => {
                Assert.That(resultNoParam, Is.Null);
                Assert.That(resultWithParam, Is.Null);
                Assert.That(TestingClass.ActionWithNoParamsCallCount, Is.EqualTo(1));
                Assert.That(TestingClass.ActionWithParamLatestValue, Is.EqualTo(5));
            });
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
