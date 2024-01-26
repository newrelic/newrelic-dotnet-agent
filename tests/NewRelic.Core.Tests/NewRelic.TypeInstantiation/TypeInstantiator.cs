// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CSharp;
using NUnit.Framework;

public interface IInterface { }

public interface IInterface2 { }

public interface IInterface3 : IInterface { }

namespace NewRelic.TypeInstantiation.UnitTests
{
    [TestFixture]
    public class Class_TypeInstantiator
    {
        private static readonly string[] References = { "System.dll", new Uri(typeof(IInterface).Assembly.CodeBase).LocalPath };

        private DirectoryInfo _tempDir;

        private static Assembly GenerateAssembly(string source, string filePath = null, Assembly[] additionalAssemblyReferences = null)
        {
            using (var provider = new CSharpCodeProvider())
            {
                additionalAssemblyReferences = additionalAssemblyReferences ?? new Assembly[0];
                var additionalReferences = additionalAssemblyReferences.Select(assembly => new Uri(assembly.CodeBase).LocalPath);
                var references = References.Concat(additionalReferences).ToArray();

                var compilerParameters = new CompilerParameters(references) { GenerateInMemory = true };
                if (filePath != null)
                {
                    compilerParameters.GenerateInMemory = false;
                    compilerParameters.OutputAssembly = filePath;
                }
                var compilerResults = provider.CompileAssemblyFromSource(compilerParameters, source);
                if (compilerResults.Errors != null && compilerResults.Errors.Count > 0 && compilerResults.Errors[0] != null)
                    throw new Exception(compilerResults.Errors[0].ErrorText);
                return compilerResults.CompiledAssembly;
            }
        }

        [OneTimeSetUp]
        public void Init()
        {
            // create temp dir for test files
            _tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            // clean up after tests
            _tempDir.Delete(true); // true means recursive delete
        }
        private DirectoryInfo MakeTempDirForTest()
        {
            return Directory.CreateDirectory(Path.Combine(_tempDir.FullName, Guid.NewGuid().ToString()));
        }


        [Test]
        public void when_no_assemblies()
        {
            var assemblies = new Assembly[] { };
            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(assemblies);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(0));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_null_assembly()
        {
            var assemblies = new Assembly[] { null };
            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(assemblies);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(0));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_null_assemblies()
        {
            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(null);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(0));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_empty_assembly()
        {
            var assembly = GenerateAssembly(@"");
            var assemblies = new[] { assembly };
            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(assemblies);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(0));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_is_abstract()
        {
            var assembly = GenerateAssembly(@"public abstract class Foo : IInterface {}");
            var assemblies = new[] { assembly };
            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(assemblies);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(0));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_is_interface()
        {
            var assembly = GenerateAssembly(@"public interface Foo : IInterface {}");
            var assemblies = new[] { assembly };
            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(assemblies);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(0));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_is_derived()
        {
            var assembly = GenerateAssembly(
@"public class Foo : IInterface {}
public class Bar : Foo {}");
            var assemblies = new[] { assembly };
            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(assemblies);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(2));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_null_type()
        {
            var types = new Type[] { null };
            var result = TypeInstantiator.InstancesFromTypes<IInterface>(types);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(0));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_null_types()
        {
            var result = TypeInstantiator.InstancesFromTypes<IInterface>(null);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(0));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_distantly_extending_interface()
        {
            var assembly = GenerateAssembly(@"public class Foo : IInterface3 {}");
            var assemblies = new[] { assembly };
            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(assemblies);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(1));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_multiple_classes_in_assembly()
        {
            var assembly = GenerateAssembly(
@"public class Foo {}
public class Bar : IInterface {}");
            var assemblies = new[] { assembly };
            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(assemblies);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(1));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_multiple_classes_implementing_interface_in_assembly()
        {
            var assembly = GenerateAssembly(
@"public class Foo : IInterface {}
public class Bar : IInterface {}");
            var assemblies = new[] { assembly };
            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(assemblies);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(2));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_mixed_assembly()
        {
            var assembly = GenerateAssembly(
@"public class Foo : IInterface {}
public class Faz : IInterface2 {}
public class Bar : IInterface3 {}
public class Baz {}");
            var assemblies = new[] { assembly };
            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(assemblies);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(2));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_multiple_interfaces()
        {
            var assembly = GenerateAssembly(@"public class Foo : IInterface, IInterface2 {}");
            var assemblies = new[] { assembly };
            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(assemblies);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(1));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_multiple_assemblies()
        {
            var assembly1 = GenerateAssembly(@"public class Foo : IInterface {}");
            var assembly2 = GenerateAssembly(@"public class Foo : IInterface {}");
            var assemblies = new[] { assembly1, assembly2 };
            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(assemblies);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(2));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }


        [Test]
        public void when_type_load_exception()
        {
            var directory = MakeTempDirForTest();

            Action<string> testMethod1 = directoryPath =>
            {
                var interfaceAssemblyPath = Path.Combine(directoryPath, "IDoSomething.dll");
                var interfaceAssembly = GenerateAssembly(@"public interface IDoSomething : IInterface { }", interfaceAssemblyPath);

                var concreteAssemblyPath = Path.Combine(directoryPath, "DoNothing.dll");
                GenerateAssembly(@"public class DoNothing : IDoSomething { }", concreteAssemblyPath, additionalAssemblyReferences: new[] { interfaceAssembly });
            };
            AppDomainExtensions.IsolateMethodInAppDomain(testMethod1, directory.FullName);

            Action<string> testMethod2 = directoryPath =>
            {
                // Add a method to IDoSomething so that our DoNothing class is no longer a valid implementation and will throw a TypeLoadException when loaded
                var interfaceAssemblyPath = Path.Combine(directoryPath, "IDoSomething.dll");
                GenerateAssembly(@"public interface IDoSomething : IInterface { void DoSomething(); }", interfaceAssemblyPath);

                var files = Directory.GetFiles(directoryPath);

                var result = TypeInstantiator.ExportedInstancesFromAssemblyPaths<IInterface>(files);
                Assert.That(result.Instances.Count(), Is.EqualTo(0));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(1));
            };

            AppDomainExtensions.IsolateMethodInAppDomain(testMethod2, directory.FullName);
            directory.Delete(true);
        }

        [Test]
        public void when_complicated()
        {
            var assemblies = new[]
            {
                GenerateAssembly(@"public class Foo : IInterface {}"),
                GenerateAssembly(@"public class Foo : IInterface2 {}"),
                GenerateAssembly(@"public class Foo {}"),
                GenerateAssembly(@"public class Foo : IInterface, IInterface2 {}"),
                GenerateAssembly(
@"public class Foo {}
public class Bar : IInterface {}"),
                GenerateAssembly(@"public class Foo : IInterface3 {}"),
            };
            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(assemblies);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(4));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_finds_files_on_disk()
        {
            var directory = MakeTempDirForTest();
            Action<string> testMethod = directoryPath =>
            {
                var filePath1 = Path.Combine(directoryPath, "foo.dll");
                var filePath2 = Path.Combine(directoryPath, "bar.dll");
                GenerateAssembly(@"public class Foo : IInterface {}", filePath1);
                GenerateAssembly(@"public class Bar : IInterface {}", filePath2);
                var result = TypeInstantiator.ExportedInstancesFromAssemblyPaths<IInterface>(filePath1, filePath2);
                Assert.That(result.Instances.Count(), Is.EqualTo(2));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            };
            AppDomainExtensions.IsolateMethodInAppDomain(testMethod, directory.FullName);
            directory.Delete(true);
        }

        [Test]
        public void when_non_existant_directory_on_disk()
        {
            var result = TypeInstantiator.ExportedInstancesFromAssemblyPaths<IInterface>("C:\\Foo\\Bar.dll");
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(0));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_invalid_path()
        {
            var result = TypeInstantiator.ExportedInstancesFromAssemblyPaths<IInterface>("!@#$%");
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(0));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_null_directory()
        {
            var result = TypeInstantiator.ExportedInstancesFromAssemblyPaths<IInterface>((string)null);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(0));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void when_invalid_file_on_disk()
        {
            var directory = MakeTempDirForTest();
            Action<string> testMethod = directoryPath =>
            {
                var filePath1 = Path.Combine(directoryPath, "foo.dll");
                File.CreateText(filePath1);
                var result = TypeInstantiator.ExportedInstancesFromAssemblyPaths<IInterface>(filePath1);
                Assert.That(result.Instances.Count(), Is.EqualTo(0));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            };
            AppDomainExtensions.IsolateMethodInAppDomain(testMethod, directory.FullName);
            directory.Delete(true);
        }

        [Test]
        public void when_constructor_throws_an_exception()
        {
            var assembly = GenerateAssembly(@"public class Foo : IInterface { public Foo() { throw new System.Exception(); } }");
            var assemblies = new[] { assembly };

            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(assemblies);
            Assert.That(result.Instances.Count(), Is.EqualTo(0));
            Assert.That(result.Exceptions.Count(), Is.EqualTo(1));
        }

        [Test]
        public void when_constructor_throws_an_exception_then_other_instances_are_still_created()
        {
            var assemblies = new[]
            {
                GenerateAssembly(@"public class Foo : IInterface {}"),
                GenerateAssembly(@"public class Foo : IInterface { public Foo() { throw new System.Exception(); } }"),
                GenerateAssembly(@"public class Foo : IInterface {}"),
            };
            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(assemblies);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(2));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(1));
            });
        }

        [Test]
        public void when_nested_class()
        {
            var assemblies = new[]
            {
                GenerateAssembly(@"public class Foo : IInterface {}"),
                GenerateAssembly(@"public class Foo { public class Bar : IInterface {} }"),
            };

            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(assemblies);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(2));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void ShouldNotLoadPrivateNestedClass()
        {
            var assemblies = new[]
            {
                GenerateAssembly(@"public class Foo : IInterface {}"),
                GenerateAssembly(@"public class Foo { private class Bar : IInterface {} }"),
            };
            var result = TypeInstantiator.ExportedInstancesFromAssemblies<IInterface>(assemblies);
            Assert.Multiple(() =>
            {
                Assert.That(result.Instances.Count(), Is.EqualTo(1));
                Assert.That(result.Exceptions.Count(), Is.EqualTo(0));
            });
        }
    }
}
#endif
