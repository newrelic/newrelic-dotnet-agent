/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Microsoft.CSharp;
using mscoree;
using NUnit.Framework;


namespace NewRelic.SystemExtensions.UnitTests
{
    public class AppDomainExtensionsTests
    {
        [Test]
        public void simple_case()
        {
            // act
            var complexName = AppDomain.CurrentDomain.GetLoadedAssemblyFullNamesBySimpleName("NewRelic.Core.Tests").FirstOrDefault();

            // assert
            Assert.AreEqual("NewRelic.Core.Tests, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", complexName);
        }

        [Test]
        public void when_multiple_assemblies_with_the_same_simple_name_then_returns_all()
        {
            // arrange
            var simpleAssemblyName = Guid.NewGuid().ToString("N");
            var fullAssemblyPath = Path.Combine(Path.GetTempPath(), simpleAssemblyName);
            var assembly1 = GenerateAssembly(string.Format(@"[assembly: System.Reflection.AssemblyVersion(""1.2.3.4"")]"), fullAssemblyPath);
            var assembly2 = GenerateAssembly(string.Format(@"[assembly: System.Reflection.AssemblyVersion(""2.0.0.0"")]"), fullAssemblyPath);

            // act
            var complexName = AppDomain.CurrentDomain.GetLoadedAssemblyFullNamesBySimpleName(simpleAssemblyName);

            // assert
            Assert.AreEqual(2, complexName.Count());
        }

        [Test]
        public void when_assembly_is_not_found_then_returns_empty_collection()
        {
            // act
            var complexName = AppDomain.CurrentDomain.GetLoadedAssemblyFullNamesBySimpleName("NonExistantAssembly");

            // assert
            Assert.NotNull(complexName);
            Assert.IsEmpty(complexName);
        }

        private static readonly string[] References = { "System.dll" };

        private static Assembly GenerateAssembly(string source, string assemblyName)
        {
            using (var provider = new CSharpCodeProvider())
            {
                var compilerParameters = new CompilerParameters(References.ToArray()) { GenerateInMemory = true };
                compilerParameters.OutputAssembly = assemblyName;
                compilerParameters.TempFiles = new TempFileCollection();
                var compilerResults = provider.CompileAssemblyFromSource(compilerParameters, source);
                if (compilerResults.Errors != null && compilerResults.Errors.Count > 0 && compilerResults.Errors[0] != null)
                {
                    throw new Exception(compilerResults.Errors[0].ErrorText);
                }
                return compilerResults.CompiledAssembly;
            }
        }

        [Test]
        public void when_CreateInstanceAndUnwrap_is_called_then_instance_of_type_T_is_returned()
        {
            var instance = AppDomain.CurrentDomain.CreateInstanceAndUnwrap<BasicClass>();
            Assert.IsInstanceOf<BasicClass>(instance);

        }

        [Test]
        public void when_CreateInstanceAndUnwrap_is_called_with_constructorless_class_then_()
        {
            Assert.Throws<MissingMethodException>(() => AppDomain.CurrentDomain.CreateInstanceAndUnwrap<NoDefaultConstructorClass>());
        }

        [Test]
        public void when_empty_action_is_called_then_no_result_is_returned()
        {
            Action action = () => { };
            var result = AppDomainExtensions.IsolateMethodInAppDomain(action, AssemblyResolver);
            Assert.Null(result);
        }

        [Test]
        public void when_func_is_passed_then_output_is_returned()
        {
            Func<object> func = () => "Foo";
            var result = AppDomainExtensions.IsolateMethodInAppDomain(func, AssemblyResolver);
            Assert.AreEqual("Foo", result);
        }

        [Test]
        public void when_exception_is_thrown_in_action_then_exception_bubbles_out()
        {
            Action action = () => { throw new ArgumentOutOfRangeException(); };
            Assert.Throws<ArgumentOutOfRangeException>(() => AppDomainExtensions.IsolateMethodInAppDomain(action, AssemblyResolver));
        }

        [Test]
        public void when_func_throws_exception_then_exception_bubbles_out()
        {
            Func<object> func = () => { throw new ArgumentOutOfRangeException(); };
            Assert.Throws<ArgumentOutOfRangeException>(() => AppDomainExtensions.IsolateMethodInAppDomain(func, AssemblyResolver));
        }

        [Test]
        public void when_static_is_set_outside_isolation_then_default_value_is_read_in_isolation()
        {
            Foo.StaticString = "Bar";
            Func<object> func = () => Foo.StaticString;
            var result = AppDomainExtensions.IsolateMethodInAppDomain(func, AssemblyResolver);
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void when_static_is_set_inside_isolation_then_the_change_is_not_seen_outside_of_isolation()
        {
            Foo.StaticString = "Bar";
            Func<object> func = () => Foo.StaticString = "Zab";
            AppDomainExtensions.IsolateMethodInAppDomain(func, AssemblyResolver);
            Assert.AreEqual("Bar", Foo.StaticString);
        }

        [Test]
        public void when_static_is_set_and_read_in_isolation_then_correct_value_is_read()
        {
            Func<object> func = () =>
            {
                Foo.StaticString = "Zab";
                return Foo.StaticString;
            };
            var result = AppDomainExtensions.IsolateMethodInAppDomain(func, AssemblyResolver);
            Assert.AreEqual("Zab", result);
        }

        [Test]
        public void when_two_functions_are_executed_in_separate_isolated_AppDomains_then_changes_to_statics_by_one_will_not_effect_the_other()
        {
            Action action = () => Foo.StaticString = "Zab";
            AppDomainExtensions.IsolateMethodInAppDomain(action, AssemblyResolver);
            Func<object> func = () => Foo.StaticString;
            var result = AppDomainExtensions.IsolateMethodInAppDomain(func, AssemblyResolver);
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void when_input_data_is_passed_in_then_it_is_accessible_from_inside_isolation()
        {
            Func<object, object> func = inputData => inputData + ":Bar";
            var result = AppDomainExtensions.IsolateMethodInAppDomain(func, AssemblyResolver, "Foo");
            Assert.AreEqual("Foo:Bar", result);
        }

        [Test]
        public void when_input_data_is_passed_in_with_action_then_it_is_accessible_from_inside_isolation()
        {
            Action<object> action = inputData => { throw new Exception(inputData + ":Foo"); };
            var exception = Assert.Throws<Exception>(() => AppDomainExtensions.IsolateMethodInAppDomain(action, AssemblyResolver, "Bar"));
            Assert.AreEqual("Bar:Foo", exception.Message);
        }

        [Test]
        public void when_input_data_is_not_serializable_then_serialization_exception_is_thrown()
        {
            Func<object, object> func = inputData => (inputData as Foo).LocalString + ":Bar";
            Assert.Throws<SerializationException>(() => AppDomainExtensions.IsolateMethodInAppDomain(func, AssemblyResolver, new Foo()));
        }

        [Test]
        public void when_returns_then_AppDomain_is_unloaded()
        {
            // ARRANGE
            var appDomainsAtStart = GetAppDomains();
            Action action = () => { };

            // ACT
            AppDomainExtensions.IsolateMethodInAppDomain(action, AssemblyResolver);

            // ASSERT
            var appDomainsAtEnd = GetAppDomains();
            Assert.AreEqual(appDomainsAtStart.Count(), appDomainsAtEnd.Count());
        }

        private static IEnumerable<AppDomain> GetAppDomains()
        {
            var appDomains = new List<AppDomain>();
            var enumHandle = IntPtr.Zero;
            var host = new CorRuntimeHost();
            try
            {
                host.EnumDomains(out enumHandle);
                while (true)
                {
                    object domain;
                    host.NextDomain(enumHandle, out domain);
                    if (domain == null)
                        break;
                    var appDomain = domain as AppDomain;
                    appDomains.Add(appDomain);
                }
                return appDomains;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                return Enumerable.Empty<AppDomain>();
            }
            finally
            {
                host.CloseEnum(enumHandle);
                Marshal.ReleaseComObject(host);
            }
        }

        private static Assembly AssemblyResolver(object sender, ResolveEventArgs args)
        {
            var shortAssemblyName = GetShortAssemblyName(args.Name);

            return GetAllAssemblyLocations()
                .Where(location => string.Equals(Path.GetFileNameWithoutExtension(location), shortAssemblyName, StringComparison.InvariantCultureIgnoreCase))
                .Select(location => Assembly.LoadFrom(location))
                .FirstOrDefault();
        }

        public static string[] GetAllAssemblyLocations()
        {
            var dependencies = Environment.GetEnvironmentVariable("NCrunch.AllAssemblyLocations");
            if (dependencies == null)
                return null;

            return dependencies.Split(';');
        }

        private static string GetShortAssemblyName(string assemblyName)
        {
#if NCRUNCH
            if (assemblyName.Contains(","))
                return assemblyName.Substring(0, assemblyName.IndexOf(','));

            return assemblyName;
#else
            return null;
#endif
        }

        private class BasicClass { }

        private class NoDefaultConstructorClass
        {
            private NoDefaultConstructorClass() { }
        }

        private class Foo
        {
            public static string StaticString = string.Empty;
            public readonly string LocalString = "Zab";
        }
    }
}
