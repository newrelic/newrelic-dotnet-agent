// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
        var continuousProfilerMethods = typeof(INativeMethods).GetMethods()
            .Where(m => m.Name.StartsWith("ContinuousProfiler", StringComparison.Ordinal));

        Assert.That(continuousProfilerMethods, Is.Not.Empty);

        foreach (var interfaceMethod in continuousProfilerMethods)
        {
            var parameterTypes = interfaceMethod.GetParameters().Select(p => p.ParameterType).ToArray();
            var implementedMethod = implementationType.GetMethod(interfaceMethod.Name, parameterTypes);

            Assert.That(implementedMethod, Is.Not.Null,
                $"{implementationType.Name} is missing an implementation of {interfaceMethod.Name}");
        }
    }

    // Each public ContinuousProfiler* member delegates to a private static extern
    // "Extern<MemberName>" method carrying the actual [DllImport]. This verifies that P/Invoke's
    // EntryPoint, CallingConvention, and library name are correct -- a wrong value here compiles
    // and passes the two tests above (which only check managed method names/signatures) but fails
    // at runtime.
    [TestCase(typeof(LinuxNativeMethods), "NewRelicProfiler")]
    [TestCase(typeof(WindowsNativeMethods), "NewRelic.Profiler.dll")]
    public void NativeMethodsImplementation_ContinuousProfilerPInvokes_HaveExpectedDllImportMetadata(Type implementationType, string expectedLibrary)
    {
        var continuousProfilerMethodNames = typeof(INativeMethods).GetMethods()
            .Where(m => m.Name.StartsWith("ContinuousProfiler", StringComparison.Ordinal))
            .Select(m => m.Name);

        foreach (var methodName in continuousProfilerMethodNames)
        {
            var externMethod = implementationType.GetMethod($"Extern{methodName}", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(externMethod, Is.Not.Null, $"{implementationType.Name} is missing Extern{methodName}");

            var dllImport = externMethod.GetCustomAttribute<DllImportAttribute>();
            Assert.That(dllImport, Is.Not.Null, $"{implementationType.Name}.Extern{methodName} is missing [DllImport]");

            Assert.Multiple(() =>
            {
                Assert.That(dllImport.Value, Is.EqualTo(expectedLibrary), $"{implementationType.Name}.Extern{methodName} has wrong library name");
                Assert.That(dllImport.EntryPoint, Is.EqualTo(methodName), $"{implementationType.Name}.Extern{methodName} has wrong EntryPoint");
                Assert.That(dllImport.CallingConvention, Is.EqualTo(CallingConvention.Cdecl), $"{implementationType.Name}.Extern{methodName} has wrong CallingConvention");
            });
        }
    }
}
