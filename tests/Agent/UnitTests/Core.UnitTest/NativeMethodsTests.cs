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

    // Every continuous-profiler entry point returns int (0 == success, non-zero == HRESULT-style
    // failure) so a non-throwing native failure can be signaled to managed code -- see Cluster B and the
    // status-channel comment on INativeMethods. ReadThreadSamples's int is its byte count (unchanged).
    [TestCase("ContinuousProfilerStart", new[] { typeof(int) }, typeof(int))]
    [TestCase("ContinuousProfilerStop", new Type[0], typeof(int))]
    [TestCase("ContinuousProfilerReadThreadSamples", new[] { typeof(int), typeof(byte[]) }, typeof(int))]
    [TestCase("ContinuousProfilerSetTraceContext", new[] { typeof(long), typeof(long), typeof(long) }, typeof(int))]
    [TestCase("ContinuousProfilerResetTraceContext", new Type[0], typeof(int))]
    [TestCase("ContinuousProfilerSetAgentWork", new Type[0], typeof(int))]
    [TestCase("ContinuousProfilerResetAgentWork", new Type[0], typeof(int))]
    [TestCase("ContinuousProfilerShutdown", new Type[0], typeof(int))]
    [TestCase("ContinuousProfilerRetireSpans", new[] { typeof(long[]), typeof(int) }, typeof(int))]
    public void INativeMethods_DeclaresExpectedContinuousProfilerMember(string methodName, Type[] parameterTypes, Type returnType)
    {
        var method = typeof(INativeMethods).GetMethod(methodName, parameterTypes);

        Assert.That(method, Is.Not.Null, $"INativeMethods is missing {methodName}({string.Join(", ", parameterTypes.Select(t => t.Name))})");
        Assert.That(method.ReturnType, Is.EqualTo(returnType));
    }

    // ContinuousProfilerGetPendingPushCell cannot be listed in the [TestCase] table above: its parameter is
    // by-ref (out IntPtr) and typeof(IntPtr).MakeByRefType() is not a constant expression, so it gets its
    // own test. The remaining reflection-driven tests below cover it automatically.
    [Test]
    public void INativeMethods_DeclaresContinuousProfilerGetPendingPushCell()
    {
        var method = typeof(INativeMethods).GetMethod("ContinuousProfilerGetPendingPushCell", new[] { typeof(IntPtr).MakeByRefType() });

        Assert.That(method, Is.Not.Null, "INativeMethods is missing ContinuousProfilerGetPendingPushCell(out IntPtr)");
        Assert.Multiple(() =>
        {
            Assert.That(method.ReturnType, Is.EqualTo(typeof(int)));
            Assert.That(method.GetParameters()[0].IsOut, Is.True);
        });
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
