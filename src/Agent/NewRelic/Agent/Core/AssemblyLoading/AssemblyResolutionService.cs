// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace NewRelic.Agent.Core.AssemblyLoading;

/// <summary>
/// This service registers an AssemblyResolve handler that will be fired whenever assembly resolution fails. The handler essentially acts as an assembly version redirect for any assembly that loaded by NewRelic code.
/// </summary>
public class AssemblyResolutionService : IDisposable
{
    public AssemblyResolutionService()
    {
        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolutionFailure;
    }

    public void Dispose()
    {
        AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolutionFailure;
    }

    private static Assembly OnAssemblyResolutionFailure(object sender, ResolveEventArgs args)
    {
        if (args == null)
            return null;
        if (!IsWrapperServiceOrTracerInStack())
            return null;

        return TryGetAlreadyLoadedAssemblyFromFullName(args.Name);
    }

    private static Assembly TryGetAlreadyLoadedAssemblyFromFullName(string fullAssemblyName)
    {
        if (fullAssemblyName == null)
            return null;

        var simpleAssemblyName = new AssemblyName(fullAssemblyName).Name;
        if (simpleAssemblyName == null)
            return null;

        return TryGetAlreadyLoadedAssemblyBySimpleName(simpleAssemblyName);
    }

    private static Assembly TryGetAlreadyLoadedAssemblyBySimpleName(string simpleAssemblyName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly != null)
            .Where(assembly => assembly.GetName().Name == simpleAssemblyName)
            .FirstOrDefault();
    }

    private static bool IsWrapperServiceOrTracerInStack()
    {
        var stackFrames = new StackTrace().GetFrames();
        if (stackFrames == null)
            return false;

        return stackFrames
            .Select(GetFrameType)
            .Where(type => type != null)
            .Where(type => type != typeof(AssemblyResolutionService))
            .Where(IsNewRelicType)
            .Any();
    }

    private static Type GetFrameType(StackFrame stackFrame)
    {
        if (stackFrame == null)
            return null;

        var method = stackFrame.GetMethod();
        if (method == null)
            return null;

        return method.DeclaringType;
    }

    private static bool IsNewRelicType(Type type)
    {
        var typeName = type.FullName;
        if (typeName == null)
            return false;

        return typeName.StartsWith("NewRelic");
    }
}
