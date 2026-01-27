// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NewRelic.Agent.Core.Utilities;

public class TypeInstantiator
{
    static TypeInstantiator()
    {
        // Libraries are loaded by reading the bytes off disk and then loading the bytes as an assembly.  Because of this, their path isn't in the assembly resolution search path.  This leads to dependencies failing to resolve because they can't be found.  Also, assemblies loaded with Assembly.Load will not resolve assemblies by looking at the currently loaded set of assemblies.  This adds an assembly resolver that will resolve with already loaded assemblies.  In the case of extensions, because we loop over all of the extensions in a folder, they will all be loaded into memory.  Once someone goes and tries to instantiate any of the types, this assembly resolver will successfully resolve the dependencies.
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            if (args == null)
                return null;

            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly != null)
                .FirstOrDefault(assembly => assembly.FullName == args.Name);
        };
    }

    private static Assembly AssemblyFromPath(string path)
    {
        if (path == null)
            return null;

        try
        {
            return Assembly.LoadFrom(path);
        }
        catch
        {
            return null;
        }
    }


    private static IEnumerable<Type> ExportedTypesFromAssembly(Assembly assembly)
    {
        if (assembly == null)
        {
            return Enumerable.Empty<Type>();
        }

        try
        {
            // Look for only publicly exposed types
            return assembly.GetExportedTypes();
        }
        catch (Exception exception)
        {
            // GetExportedTypes() will throw a FileNotFoundException if can't load dependent types or 
            // NotSupportedException for dynamic assemblies. If we wanted to gracefully log and continue, 
            // could switch back to GetTypes() and catch the ReflectionTypeLoadException again but actually return 
            // the sucessful types. I still think we should filter out / not log non-public types in that case.

            throw new Exception($"Error occurred while loading types from {assembly.FullName}", exception);
        }
    }

    private static bool TypeIsInstantiatable(Type type)
    {
        if (type == null)
            return false;
        if (type.IsAbstract)
            return false;
        if (type.IsGenericTypeDefinition)
            return false;
        if (type.IsInterface)
            return false;
        return true;
    }

    private static bool TypeImplements<T>(Type type)
    {
        return type != null
               && typeof(T).IsAssignableFrom(type);
    }

    private static T InstanceFromType<T>(Type type)
    {
        try
        {
            return (T)Activator.CreateInstance(type);
        }
        catch (Exception ex)
        {
            var message = string.Format("An exception was thrown while constructing an instance of type {0}", type.FullName);
            throw new Exception(message, ex);
        }
    }


    private static GetTypesResult GetExportedTypes(IEnumerable<Assembly> assemblies)
    {
        var types = new List<Type>();
        var exceptions = new List<Exception>();

        foreach (var assembly in assemblies)
        {
            try
            {
                var typesFromAssembly = ExportedTypesFromAssembly(assembly);
                types.AddRange(typesFromAssembly);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        return new GetTypesResult(types, exceptions);
    }

    public static TypeInstantiatorResult<T> ExportedInstancesFromAssemblyPaths<T>(params string[] assemblyPaths)
    {
        var assemblies = assemblyPaths != null
            ? assemblyPaths.Select(AssemblyFromPath)
            : Enumerable.Empty<Assembly>();

        return ExportedInstancesFromAssemblies<T>(assemblies.ToArray());
    }

    public static TypeInstantiatorResult<T> ExportedInstancesFromAssemblies<T>(params Assembly[] assemblies)
    {
        if (assemblies == null)
            return new TypeInstantiatorResult<T>();

        var getTypesResult = GetExportedTypes(assemblies);
        var getInstancesResult = InstancesFromTypes<T>(getTypesResult.Types);

        var instances = getInstancesResult.Instances;
        var exceptions = getInstancesResult.Exceptions.Concat(getTypesResult.Exceptions);

        return new TypeInstantiatorResult<T>(instances, exceptions);
    }

    public static TypeInstantiatorResult<T> InstancesFromTypes<T>(IEnumerable<Type> types)
    {
        if (types == null)
            return new TypeInstantiatorResult<T>();

        var applicableTypes = types
            .Where(TypeIsInstantiatable)
            .Where(TypeImplements<T>);

        var instances = new List<T>();
        var exceptions = new List<Exception>();
        foreach (var type in applicableTypes)
        {
            if (type == null)
                continue;

            try
            {
                var instance = InstanceFromType<T>(type);
                instances.Add(instance);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        return new TypeInstantiatorResult<T>(instances, exceptions);
    }

    private class GetTypesResult
    {
        public readonly IEnumerable<Type> Types;

        public readonly IEnumerable<Exception> Exceptions;

        public GetTypesResult(IEnumerable<Type> instances, IEnumerable<Exception> exceptions)
        {
            Types = instances;
            Exceptions = exceptions;
        }
    }
}