// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0



using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers
{
    /// <summary>
    /// Helpers that deal with reflections.  Useful to ientifying which assemblies 
    /// have been loaded as part of the agent.
    /// </summary>
    public static class ReflectionUtil
    {

        public static List<Tuple<string, string>> ScanAssembliesAndTypes()
        {
            var result = new List<Tuple<string, string>>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => !x.IsDynamic)
                .ToList();

            foreach (var assembly in assemblies)
            {
                Console.WriteLine($"Assembly: {assembly.FullName}; location={assembly.Location}");

                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    result.Add(new Tuple<string, string>(assembly.FullName, type.FullName));
                    Console.WriteLine($"\tType: {type.FullName}");
                }
            }

            return result;
        }

        public static List<Type> GetClassesByType<T>()
        {
            var result = new List<Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                result.AddRange(GetClassesByType<T>(assembly));
            }

            return result;
        }

        public static List<Type> GetClassesByType<T>(Assembly assembly)
        {
            var result = new List<Type>();
            var type = typeof(T);

            foreach (var objType in assembly.GetTypes())
            {
                if (objType.IsInstanceOfType(type))
                {
                    result.Add(objType);
                }
            }

            return result;
        }

        public static IEnumerable<Type> FindTypesWithAttribute<TAttrib>() where TAttrib : Attribute
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => !x.IsDynamic)
                .ToList();

            var attribType = typeof(TAttrib);

            foreach (var assembly in assemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (Exception)
                {
                    Console.WriteLine($"Exception calling .GetTypes on assembly {assembly.FullName}");
                    throw;
                }

                foreach (var type in types)
                {
                    if (type.IsDefined(attribType, true))
                    {
                        yield return type;
                    }
                }
            }
        }

        public static IEnumerable<MethodInfo> FindMethodsWithAttribute<TAttrib>(Type type) where TAttrib : Attribute
        {
            var attribType = typeof(TAttrib);

            foreach (var method in type.GetMethods())
            {
                if (method.IsDefined(attribType, true))
                {
                    yield return method;
                }

            }
        }

        public static List<MethodInfo> FindMethodUsingAttributes<TAttribClass, TAttribMethod>(string libraryName, string methodName)
            where TAttribClass : Attribute
            where TAttribMethod : Attribute
        {

            var types = ReflectionUtil.FindTypesWithAttribute<TAttribClass>()
                .Where(t => t.Name.Equals(libraryName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var methods = types.SelectMany(t => ReflectionUtil.FindMethodsWithAttribute<TAttribMethod>(t)
                                                    .Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase)))
                                                    .ToList();

            return methods;
        }

    }
}
