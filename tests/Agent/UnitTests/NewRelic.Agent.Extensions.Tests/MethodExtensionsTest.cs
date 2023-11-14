// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace Agent.Extensions.Tests
{
    public class MethodExtensionsTest
    {
        [Test]
        public void MatchesAny_MethodName_Success()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "SimpleMethod";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", string.Empty);

            // ACT & ASSERT
            Assert.True(method.MatchesAny(expectedAssemblyName, expectedTypeName, expectedMethodName));
        }

        [Test]
        public void MatchesAny_MethodName_Failure()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "NotSoSimpleMethod";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", string.Empty);

            // ACT & ASSERT
            Assert.False(method.MatchesAny(expectedAssemblyName, expectedTypeName, expectedMethodName));
        }

        [Test]
        public void MatchesAny_MethodName_Failure_Different_Case()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "simpleMethod";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", string.Empty);

            // ACT & ASSERT
            Assert.False(method.MatchesAny(expectedAssemblyName, expectedTypeName, expectedMethodName));
        }

        [Test]
        public void MatchesAny_MethodName_SingleParameter_SingleParameterSignature_Success()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "SimpleMethod";
            const string expectedParamSignature = "System.String";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String");

            // ACT & ASSERT
            Assert.True(method.MatchesAny(expectedAssemblyName, expectedTypeName, expectedMethodName, expectedParamSignature));
        }

        [Test]
        public void MatchesAny_MethodName_SingleParameter_SingleParameterSignature_AcceptAnyParamSignature_Success()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "SimpleMethod";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String");

            // ACT & ASSERT
            Assert.True(method.MatchesAny(expectedAssemblyName, expectedTypeName, expectedMethodName));
        }

        [Test]
        public void MatchesAny_MethodName_SingleParameter_SingleParameterSignature_Failure()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "SimpleMethod";
            const string expectedParamSignature = "System.Int32";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String");

            // ACT & ASSERT
            Assert.False(method.MatchesAny(expectedAssemblyName, expectedTypeName, expectedMethodName, expectedParamSignature));
        }

        [Test]
        public void MatchesAny_MethodName_SingleParameter_SingleParameterSignature_Failure_Different_Case()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "SimpleMethod";
            const string expectedParamSignature = "system.string";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String");

            // ACT & ASSERT
            Assert.False(method.MatchesAny(expectedAssemblyName, expectedTypeName, expectedMethodName, expectedParamSignature));
        }

        [Test]
        public void MatchesAny_MethodName_TwoParameters_SingleParameterSignature_Success()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "SimpleMethod";
            const string expectedParamSignature = "System.String,System.Int32";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String,System.Int32");

            // ACT & ASSERT
            Assert.True(method.MatchesAny(expectedAssemblyName, expectedTypeName, expectedMethodName, expectedParamSignature));
        }

        [Test]
        public void MatchesAny_MethodName_TwoParameters_SingleParameterSignature_AcceptAnyParamSignature_Success()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "SimpleMethod";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String,System.Int32");

            // ACT & ASSERT
            Assert.True(method.MatchesAny(expectedAssemblyName, expectedTypeName, expectedMethodName));
        }

        [Test]
        public void MatchesAny_MethodName_TwoParameters_SingleParameterSignature_Failure_Different_Param_Type()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "SimpleMethod";
            const string expectedParamSignature = "System.String,System.Double";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String,System.Int32");

            // ACT & ASSERT
            Assert.False(method.MatchesAny(expectedAssemblyName, expectedTypeName, expectedMethodName, expectedParamSignature));
        }

        [Test]
        public void MatchesAny_MethodName_TwoParameters_SingleParameterSignature_Failure_Different_Number_Params()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "SimpleMethod";
            const string expectedParamSignature = "System.String,System.Int32,System.Threading.Timer";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String,System.Int32");

            // ACT & ASSERT
            Assert.False(method.MatchesAny(expectedAssemblyName, expectedTypeName, expectedMethodName, expectedParamSignature));
        }

        [Test]
        public void MatchesAny_MethodName__TwoParameterSignatures_Success()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "SimpleMethod";
            const string expectedParamSignature1 = "System.Object";
            const string expectedParamSignature2 = "System.String,System.Int32";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.Object");

            // ACT & ASSERT
            Assert.True(method.MatchesAny(expectedAssemblyName, expectedTypeName, expectedMethodName, new[] { expectedParamSignature1, expectedParamSignature2 }));
        }

        [Test]
        public void MatchesAny_MethodName__TwoParameterSignatures_Success_On_No_Parameters()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "SimpleMethod";
            const string expectedParamSignature1 = "System.Object";
            var expectedParamSignature2 = string.Empty;
            var method = new Method(typeof(SimpleClass), "SimpleMethod", string.Empty);

            // ACT & ASSERT
            Assert.True(method.MatchesAny(expectedAssemblyName, expectedTypeName, expectedMethodName, new[] { expectedParamSignature1, expectedParamSignature2 }));
        }

        [Test]
        public void MatchesAny_MethodName__TwoParameterSignatures_Failure_Different_MethodName()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "ComplexMethod";
            const string expectedParamSignature1 = "System.Object";
            const string expectedParamSignature2 = "System.String,System.Int32";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.Object");

            // ACT & ASSERT
            Assert.False(method.MatchesAny(expectedAssemblyName, expectedTypeName, expectedMethodName, new[] { expectedParamSignature1, expectedParamSignature2 }));
        }

        [Test]
        public void MatchesAny_MethodName__TwoParameterSignatures_Failure_No_Match_Actual_Has_Zero_Parameters()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "ComplexMethod";
            const string expectedParamSignature1 = "System.Object";
            const string expectedParamSignature2 = "System.String,System.Int32";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", string.Empty);

            // ACT & ASSERT
            Assert.False(method.MatchesAny(expectedAssemblyName, expectedTypeName, expectedMethodName, new[] { expectedParamSignature1, expectedParamSignature2 }));
        }

        [Test]
        public void MatchesAny_MethodName__TwoParameterSignatures_Failure_No_Match_Actual_Has_Parameters()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "ComplexMethod";
            const string expectedParamSignature1 = "System.Object";
            const string expectedParamSignature2 = "System.String,System.Int32";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String");

            // ACT & ASSERT
            Assert.False(method.MatchesAny(expectedAssemblyName, expectedTypeName, expectedMethodName, new[] { expectedParamSignature1, expectedParamSignature2 }));
        }

        [Test]
        public void MatchesAny_MultipleTypes_Success()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName1 = "Circus.Bozo.Clowning";
            const string expectedTypeName2 = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "SimpleMethod";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String");

            // ACT & ASSERT
            Assert.True(method.MatchesAny(expectedAssemblyName, new[] { expectedTypeName1, expectedTypeName2 }, expectedMethodName));
        }

        [Test]
        public void MatchesAny_MultipleTypes_Failure_Different_Class()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName1 = "Circus.Bozo.Clowning";
            const string expectedTypeName2 = "Agent.Extensions.Testing.SimpleClass";
            const string expectedMethodName = "SimpleMethod";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String");

            // ACT & ASSERT
            Assert.False(method.MatchesAny(expectedAssemblyName, new[] { expectedTypeName1, expectedTypeName2 }, expectedMethodName));
        }

        [Test]
        public void MatchesAny_MultipleTypes_Failure_Different_Method()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName1 = "Circus.Bozo.Clowning";
            const string expectedTypeName2 = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "ComplexMethod";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String");

            // ACT & ASSERT
            Assert.False(method.MatchesAny(expectedAssemblyName, new[] { expectedTypeName1, expectedTypeName2 }, expectedMethodName));
        }

        [Test]
        public void MatchesAny_MultipleTypes_Failure_Different_Case()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName1 = "Circus.Bozo.Clowning";
            const string expectedTypeName2 = "Agent.extensions.tests.SimpleClass";
            const string expectedMethodName = "SimpleMethod";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String");

            // ACT & ASSERT
            Assert.False(method.MatchesAny(expectedAssemblyName, new[] { expectedTypeName1, expectedTypeName2 }, expectedMethodName));
        }

        [Test]
        public void MatchesAny_MultipleAssemblies_MultipleTypes_MultipleMethods_SingleParameterSignature_Success()
        {
            // ARRANGE
            const string expectedAssemblyName1 = "NewRelic.Agent.Extensions.Tests";
            const string expectedAssemblyName2 = "Universe.Galaxy.SolarSystem";
            const string expectedTypeName1 = "Constants";
            const string expectedTypeName2 = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName1 = "SimpleMethod";
            const string expectedMethodName2 = "ComplexMethod";
            const string expectedParamSignature = "System.String";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String");

            // ACT & ASSERT
            Assert.True(method.MatchesAny(new[] { expectedAssemblyName1, expectedAssemblyName2 }, new[] { expectedTypeName1, expectedTypeName2 }, new[] { expectedMethodName1, expectedMethodName2 }, expectedParamSignature));
        }

        [Test]
        public void MatchesAny_MultipleAssemblies_MultipleTypes_MultipleMethods_SingleParameterSignature_Failure_Different_Assembly()
        {
            // ARRANGE
            const string expectedAssemblyName1 = "Science.Physics.Electromagnetism";
            const string expectedAssemblyName2 = "Universe.Galaxy.SolarSystem";
            const string expectedTypeName1 = "Constants";
            const string expectedTypeName2 = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName1 = "SimpleMethod";
            const string expectedMethodName2 = "ComplexMethod";
            const string expectedParamSignature = "System.String";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String");

            // ACT & ASSERT
            Assert.False(method.MatchesAny(new[] { expectedAssemblyName1, expectedAssemblyName2 }, new[] { expectedTypeName1, expectedTypeName2 }, new[] { expectedMethodName1, expectedMethodName2 }, expectedParamSignature));
        }

        [Test]
        public void MatchesAny_MultipleAssemblies_MultipleTypes_MultipleMethods_SingleParameterSignature_Failure_Different_Type()
        {
            // ARRANGE
            const string expectedAssemblyName1 = "NewRelic.Agent.Extensions.Tests";
            const string expectedAssemblyName2 = "Universe.Galaxy.SolarSystem";
            const string expectedTypeName1 = "Constants";
            const string expectedTypeName2 = "Agent.Extensions.Tests.WonkyClass";
            const string expectedMethodName1 = "SimpleMethod";
            const string expectedMethodName2 = "ComplexMethod";
            const string expectedParamSignature = "System.String";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String");

            // ACT & ASSERT
            Assert.False(method.MatchesAny(new[] { expectedAssemblyName1, expectedAssemblyName2 }, new[] { expectedTypeName1, expectedTypeName2 }, new[] { expectedMethodName1, expectedMethodName2 }, expectedParamSignature));
        }

        [Test]
        public void MatchesAny_MultipleAssemblies_MultipleTypes_MultipleMethods_SingleParameterSignature_Failure_Different_NumberParameters()
        {
            // ARRANGE
            const string expectedAssemblyName1 = "NewRelic.Agent.Extensions.Tests";
            const string expectedAssemblyName2 = "Universe.Galaxy.SolarSystem";
            const string expectedTypeName1 = "Constants";
            const string expectedTypeName2 = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName1 = "SimpleMethod";
            const string expectedMethodName2 = "ComplexMethod";
            const string expectedParamSignature = "System.String";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String, System.Double");

            // ACT & ASSERT
            Assert.False(method.MatchesAny(new[] { expectedAssemblyName1, expectedAssemblyName2 }, new[] { expectedTypeName1, expectedTypeName2 }, new[] { expectedMethodName1, expectedMethodName2 }, expectedParamSignature));
        }

        [Test]
        public void MatchesAny_MultipleAssemblies_MultipleTypes_MultipleMethods_SingleParameterSignature_Failure_Different_ParameterType()
        {
            // ARRANGE
            const string expectedAssemblyName1 = "NewRelic.Agent.Extensions.Tests";
            const string expectedAssemblyName2 = "Universe.Galaxy.SolarSystem";
            const string expectedTypeName1 = "Constants";
            const string expectedTypeName2 = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName1 = "SimpleMethod";
            const string expectedMethodName2 = "ComplexMethod";
            const string expectedParamSignature = "System.String";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.Double");

            // ACT & ASSERT
            Assert.False(method.MatchesAny(new[] { expectedAssemblyName1, expectedAssemblyName2 }, new[] { expectedTypeName1, expectedTypeName2 }, new[] { expectedMethodName1, expectedMethodName2 }, expectedParamSignature));
        }

        [Test]
        public void MatchesAny_MultipleAssemblies_MultipleTypes_MultipleMethods_SingleParameterSignature_Failure_Different_Case()
        {
            // ARRANGE
            const string expectedAssemblyName1 = "NewRelic.Agent.Extensions.test";
            const string expectedAssemblyName2 = "Universe.Galaxy.SolarSystem";
            const string expectedTypeName1 = "Constants";
            const string expectedTypeName2 = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName1 = "SimpleMethod";
            const string expectedMethodName2 = "ComplexMethod";
            const string expectedParamSignature = "System.String";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String");

            // ACT & ASSERT
            Assert.False(method.MatchesAny(new[] { expectedAssemblyName1, expectedAssemblyName2 }, new[] { expectedTypeName1, expectedTypeName2 }, new[] { expectedMethodName1, expectedMethodName2 }, expectedParamSignature));
        }

        [Test]
        public void MatchesAny_MultipleAssemblies_MultipleTypes_SingleMethod_SingleParameterSignature_Success()
        {
            // ARRANGE
            const string expectedAssemblyName1 = "NewRelic.Agent.Extensions.Tests";
            const string expectedAssemblyName2 = "Universe.Galaxy.SolarSystem";
            const string expectedTypeName1 = "Constants";
            const string expectedTypeName2 = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "SimpleMethod";
            const string expectedParamSignature = "System.String";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String");

            // ACT & ASSERT
            Assert.True(method.MatchesAny(new[] { expectedAssemblyName1, expectedAssemblyName2 }, new[] { expectedTypeName1, expectedTypeName2 }, expectedMethodName, expectedParamSignature));
        }

        [Test]
        public void MatchesAny_MultipleAssemblies_MultipleTypes_SingleMethod_SingleParameterSignature_Failure_MethodName()
        {
            // ARRANGE
            const string expectedAssemblyName1 = "NewRelic.Agent.Extensions.Tests";
            const string expectedAssemblyName2 = "Universe.Galaxy.SolarSystem";
            const string expectedTypeName1 = "Constants";
            const string expectedTypeName2 = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "ComplexMethod";
            const string expectedParamSignature = "System.String";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String");

            // ACT & ASSERT
            Assert.False(method.MatchesAny(new[] { expectedAssemblyName1, expectedAssemblyName2 }, new[] { expectedTypeName1, expectedTypeName2 }, expectedMethodName, expectedParamSignature));
        }

        [Test]
        public void MatchesAny_MultipleAssemblies_SingeType_SingleMethod_SingleParameterSignature_Success()
        {
            // ARRANGE
            const string expectedAssemblyName1 = "NewRelic.Agent.Extensions.Tests";
            const string expectedAssemblyName2 = "Universe.Galaxy.SolarSystem";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            const string expectedMethodName = "SimpleMethod";
            const string expectedParamSignature = "System.String";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String");

            // ACT & ASSERT
            Assert.True(method.MatchesAny(new[] { expectedAssemblyName1, expectedAssemblyName2 }, expectedTypeName, expectedMethodName, expectedParamSignature));
        }

        [Test]
        public void MatchesAny_MultipleAssemblies_SingeType_SingleMethod_SingleParameterSignature_Failure_TypeName()
        {
            // ARRANGE
            const string expectedAssemblyName1 = "NewRelic.Agent.Extensions.Tests";
            const string expectedAssemblyName2 = "Universe.Galaxy.SolarSystem";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClassYo";
            const string expectedMethodName = "SimpleMethod";
            const string expectedParamSignature = "System.String";
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String");

            // ACT & ASSERT
            Assert.False(method.MatchesAny(new[] { expectedAssemblyName1, expectedAssemblyName2 }, expectedTypeName, expectedMethodName, expectedParamSignature));
        }

        [Test]
        public void MatchesAny_SingleMethodSignature_NoParams_Success()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            var methodSignature = new MethodSignature("SimpleMethod", string.Empty);
            var method = new Method(typeof(SimpleClass), "SimpleMethod", string.Empty);

            // ACT & ASSERT
            Assert.True(method.MatchesAny(expectedAssemblyName, expectedTypeName, new[] { methodSignature }));
        }

        [Test]
        public void MatchesAny_SingleMethodSignature_NoParams_Failure_Different_MethodName()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            var methodSignature = new MethodSignature("NotSoSimpleMethod", string.Empty);
            var method = new Method(typeof(SimpleClass), "SimpleMethod", string.Empty);

            // ACT & ASSERT
            Assert.False(method.MatchesAny(expectedAssemblyName, expectedTypeName, new[] { methodSignature }));
        }

        [Test]
        public void MatchesAny_SingleMethodSignature_NoParams_Failure_ExpectedHasParams()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            var methodSignature = new MethodSignature("NotSoSimpleMethod", "System.String");
            var method = new Method(typeof(SimpleClass), "SimpleMethod", string.Empty);

            // ACT & ASSERT
            Assert.False(method.MatchesAny(expectedAssemblyName, expectedTypeName, new[] { methodSignature }));
        }

        [Test]
        public void MatchesAny_SingleMethodSignature_WithParams_Success()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            var methodSignature = new MethodSignature("SimpleMethod", "System.String,System.Thread.Timer,System.Double,System.Boolean");
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String,System.Thread.Timer,System.Double,System.Boolean");

            // ACT & ASSERT
            Assert.True(method.MatchesAny(expectedAssemblyName, expectedTypeName, new[] { methodSignature }));
        }

        [Test]
        public void MatchesAny_SingleMethodSignature_WithParams_AcceptAnyParamSignature_Success()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            var methodSignature = new MethodSignature("SimpleMethod");
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String,System.Thread.Timer,System.Double,System.Boolean");

            // ACT & ASSERT
            Assert.True(method.MatchesAny(expectedAssemblyName, expectedTypeName, new[] { methodSignature }));
        }

        [Test]
        public void MatchesAny_MultipleMethodSignatures_NoParams_Success()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";

            // Note: 
            var methodSignature1 = new MethodSignature("SimpleMethod", string.Empty);
            var methodSignature2 = new MethodSignature("ComplexMethod", string.Empty);
            var method = new Method(typeof(SimpleClass), "SimpleMethod", string.Empty);

            // ACT & ASSERT
            Assert.True(method.MatchesAny(expectedAssemblyName, expectedTypeName, new[] { methodSignature1, methodSignature2 }));
        }

        [Test]
        public void MatchesAny_MultipleMethodSignatures_WithParams_AcceptAnyParamSignature_Success()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";

            // Note: 
            var methodSignature1 = new MethodSignature("SimpleMethod");
            var methodSignature2 = new MethodSignature("ComplexMethod", string.Empty);
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String,System.Double");

            // ACT & ASSERT
            Assert.True(method.MatchesAny(expectedAssemblyName, expectedTypeName, new[] { methodSignature1, methodSignature2 }));
        }

        [Test]
        public void MatchesAny_MultipleMethodSignatures_NoParams_Failure_ExpectedHasParams()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";

            // Note: 
            var methodSignature1 = new MethodSignature("SimpleMethod", "System.Int32");
            var methodSignature2 = new MethodSignature("ComplexMethod", string.Empty);
            var method = new Method(typeof(SimpleClass), "SimpleMethod", string.Empty);

            // ACT & ASSERT
            Assert.False(method.MatchesAny(expectedAssemblyName, expectedTypeName, new[] { methodSignature1, methodSignature2 }));
        }

        [Test]
        public void MatchesAny_MultipleMethodSignature_WithParams_Success()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            var methodSignature1 = new MethodSignature("SimpleMethod", "System.String,System.Thread.Timer,System.Double");
            var methodSignature2 = new MethodSignature("SimpleMethod", "System.String,System.Thread.Timer,System.Double,System.Boolean");
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.String,System.Thread.Timer,System.Double,System.Boolean");

            // ACT & ASSERT
            Assert.True(method.MatchesAny(expectedAssemblyName, expectedTypeName, new[] { methodSignature1, methodSignature2 }));
        }

        [Test]
        public void MatchesAny_MultipleMethodSignature_SomeWith_SomeWithout_Params_MatchOn_With_Success()
        {
            // ARRANGE
            const string expectedAssemblyName = "NewRelic.Agent.Extensions.Tests";
            const string expectedTypeName = "Agent.Extensions.Tests.SimpleClass";
            var methodSignature1 = new MethodSignature("SimpleMethod", "System.String,System.Thread.Timer,System.Double");
            var methodSignature2 = new MethodSignature("SimpleMethod", "System.String,System.Thread.Timer,System.Double,System.Boolean");
            var methodSignature3 = new MethodSignature("NotSoSimpleMethod", "System.Object,System.String");
            var methodSignature4 = new MethodSignature("SimpleMethod", "");
            var methodSignature5 = new MethodSignature("SimpleMethod", "System.Object,System.String");
            var method = new Method(typeof(SimpleClass), "SimpleMethod", "System.Object,System.String");

            // ACT & ASSERT
            Assert.True(method.MatchesAny(expectedAssemblyName, expectedTypeName, new[] { methodSignature1, methodSignature2, methodSignature3, methodSignature4, methodSignature5 }));
        }
    }
}
