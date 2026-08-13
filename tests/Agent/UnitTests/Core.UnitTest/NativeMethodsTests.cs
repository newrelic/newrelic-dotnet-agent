// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NUnit.Framework;

namespace NewRelic.Agent.Core;

[TestFixture]
public class NativeMethodsTests
{
    // Real P/Invoke calls require the native profiler DLL to be loaded (only available in a
    // built agent home, exercised by integration tests). These tests instead assert the managed
    // contract shape: INativeMethods declares the expected continuous-profiler members with the
    // expected signatures, and both concrete implementations satisfy that contract.

    [TestCase(typeof(LinuxNativeMethods))]
    [TestCase(typeof(WindowsNativeMethods))]
    public void NativeMethodsImplementation_ImplementsINativeMethods(Type implementationType)
    {
        Assert.That(typeof(INativeMethods).IsAssignableFrom(implementationType), Is.True);
    }

    [TestCase("ContinuousProfilerStart", new[] { typeof(int) }, typeof(void))]
    [TestCase("ContinuousProfilerStop", new Type[0], typeof(void))]
    [TestCase("ContinuousProfilerReadThreadSamples", new[] { typeof(int), typeof(byte[]) }, typeof(int))]
    [TestCase("ContinuousProfilerSetTraceContext", new[] { typeof(long), typeof(long), typeof(long) }, typeof(void))]
    [TestCase("ContinuousProfilerResetTraceContext", new Type[0], typeof(void))]
    [TestCase("ContinuousProfilerSetAgentWork", new Type[0], typeof(void))]
    [TestCase("ContinuousProfilerResetAgentWork", new Type[0], typeof(void))]
    [TestCase("ContinuousProfilerShutdown", new Type[0], typeof(void))]
    [TestCase("ContinuousProfilerReadAllocationSamples", new[] { typeof(int), typeof(byte[]) }, typeof(int))]
    [TestCase("AllocationSamplerStart", new[] { typeof(int) }, typeof(void))]
    [TestCase("AllocationSamplerStop", new Type[0], typeof(void))]
    [TestCase("AllocationSamplerShutdown", new Type[0], typeof(void))]
    public void INativeMethods_DeclaresExpectedContinuousProfilerMember(string methodName, Type[] parameterTypes, Type returnType)
    {
        var method = typeof(INativeMethods).GetMethod(methodName, parameterTypes);

        Assert.That(method, Is.Not.Null, $"INativeMethods is missing {methodName}({string.Join(", ", parameterTypes.Select(t => t.Name))})");
        Assert.That(method.ReturnType, Is.EqualTo(returnType));
    }

    [TestCase(typeof(LinuxNativeMethods))]
    [TestCase(typeof(WindowsNativeMethods))]
    public void NativeMethodsImplementation_ImplementsAllContinuousProfilerMembers(Type implementationType)
    {
        var profilingMethods = typeof(INativeMethods).GetMethods()
            .Where(m => m.Name.StartsWith("ContinuousProfiler", StringComparison.Ordinal)
                || m.Name.StartsWith("AllocationSampler", StringComparison.Ordinal));

        Assert.That(profilingMethods, Is.Not.Empty);

        foreach (var interfaceMethod in profilingMethods)
        {
            var parameterTypes = interfaceMethod.GetParameters().Select(p => p.ParameterType).ToArray();
            var implementedMethod = implementationType.GetMethod(interfaceMethod.Name, parameterTypes);

            Assert.That(implementedMethod, Is.Not.Null,
                $"{implementationType.Name} is missing an implementation of {interfaceMethod.Name}");
        }
    }
}
