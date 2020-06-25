/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace NewRelic.Agent.Tests.ProfiledMethods
{
    public class GenericsTests : ProfilerTestsBase
    {
        #region Method Types
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private void GenericParameter(GenericClass<EmptyClass> foo)
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private void GenericMethod<T>()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private void GenericMethodWithParameter<T>(T foo)
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private T GenericMethodWithParameterAndReturn<T>(T foo)
        {
            return foo;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        private GenericClass<GenericClass2<A>> GenericMethodAndNestedGenericParameterAndReturn<A>(GenericClass<GenericClass2<A>> foo)
        {
            return foo;
        }
        #endregion

        [Test]
        public void call_generic_parameter()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            //Act
            var parameter = new GenericClass<EmptyClass>();
            Assert.DoesNotThrow(() => { GenericParameter(parameter); }, "Exception should not have been thrown.");

            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericParameter", parameter.GetType().ToString(), this, new object[] { parameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericsTests");
        }

        [Test]
        public void call_generic_method()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            // Act
            Assert.DoesNotThrow(() => { GenericMethod<String>(); }, "Exception should not have been thrown.");

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethod", "", this, new object[] { }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericsTests");
        }

        [Test]
        public void call_generic_method_with_parameter()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            //Act
            var parameter = "My String.";
            Assert.DoesNotThrow(() => { GenericMethodWithParameter(parameter); }, "Exception should not have been thrown.");

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodWithParameter", "!!0", this, new object[] { parameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericsTests");
        }

        [Test]
        public void call_generic_method_with_parameter_and_return()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            //Act
            var parameter = "My String.";
            var result = null as String;
            Assert.DoesNotThrow(() => { result = GenericMethodWithParameterAndReturn(parameter); }, "Exception should not have been thrown.");

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodWithParameterAndReturn", "!!0", this, new object[] { parameter }, result, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericsTests");

            // validate that the input was returned to the result (implementation detail of method being called)
            Assert.AreEqual(parameter, result, "Method did not execute properly.");
        }

        [Test]
        public void call_generic_method_and_nested_generic_parameter_and_return()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            //Act
            var parameter = new GenericClass<GenericClass2<String>>(new GenericClass2<String>("MyString"));
            var result = new GenericClass<GenericClass2<String>>(new GenericClass2<String>(String.Empty));
            Assert.DoesNotThrow(() => { result = GenericMethodAndNestedGenericParameterAndReturn(parameter); }, "Exception should not have been thrown.");

            //Assert
            const String expectedSignature = "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1[NewRelic.Agent.Tests.ProfiledMethods.GenericClass2`1[!!0]]";
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodAndNestedGenericParameterAndReturn", expectedSignature, this, new object[] { parameter }, result, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericsTests");

            // validate that the input was returned to the result (implementation detail of method being called)
            Assert.AreEqual(parameter, result, "Method did not execute properly.");
        }

        [Test]
        public void call_generic_method_with_different_types_on_different_calls()
        {
            // run a full test on the generic method with a string type parameter/return

            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            //Act
            var stringParameter = "My String.";
            var stringResult = null as String;
            Assert.DoesNotThrow(() => { stringResult = GenericMethodWithParameterAndReturn(stringParameter); }, "Exception should not have been thrown.");

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodWithParameterAndReturn", "!!0", this, new object[] { stringParameter }, stringResult, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericsTests");

            // validate that the input was returned to the result (implementation detail of method being called)
            Assert.AreEqual(stringParameter, stringResult, "Method did not execute properly.");

            // repeat with a bool type parameter/return

            //Arrange
            getTracerParameters = DefaultGetTracerImplementation();
            finishTracerParameters = DefaultFinishTracerImplementation();

            //Act
            var boolParameter = true;
            var boolResult = false;
            Assert.DoesNotThrow(() => { boolResult = GenericMethodWithParameterAndReturn(boolParameter); }, "Exception should not have been thrown.");

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodWithParameterAndReturn", "!!0", this, new object[] { boolParameter }, boolResult, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericsTests");

            // validate that the input was returned to the result (implementation detail of method being called)
            Assert.AreEqual(boolParameter, boolResult, "Method did not execute properly.");
        }

        #region Generic Class Tests

        [Test]
        public void call_method_on_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var genClass = new GenericClass<String>("yo");

            //Act
            Assert.DoesNotThrow(() => { genClass.DefaultMethod(); }, "Exception should not have been thrown.");

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "DefaultMethod", "", genClass, new object[] { }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        [Test]
        public void call_method_passing_string_argument_on_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var stringParameter = "mydata";
            var genClass = new GenericClass<String>("yo");

            //Act
            genClass.DefaultMethod(stringParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "DefaultMethod", "!0", genClass, new object[] { stringParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        [Test]
        public void call_method_passing_reftype_on_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var referenceTypeParameter = new DefaultClass();
            var genClass = new GenericClass<DefaultClass>(new DefaultClass());

            //Act
            genClass.DefaultMethod(referenceTypeParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "DefaultMethod", "!0", genClass, new object[] { referenceTypeParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        [Test]
        public void call_method_passing_valuetype_on_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var intParameter = 33;
            var genClass = new GenericClass<int>(6);

            //Act
            genClass.DefaultMethod(intParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "DefaultMethod", "!0", genClass, new object[] { intParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        [Test]
        public void call_generic_method_passing_valuetype_on_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var intParameter = 10;
            var genClass = new GenericClass<String>("yo");

            //Act
            genClass.GenericMethod<int>(intParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethod", "!!0", genClass, new object[] { intParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        [Test]
        public void call_generic_method_passing_string_on_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var stringParameter = "I'll meet you at the corner.";
            var genClass = new GenericClass<String>("What did one wall say to the other wall?");

            //Act
            genClass.GenericMethod<String>(stringParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethod", "!!0", genClass, new object[] { stringParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        [Test]
        public void call_generic_method_passing_reftype_on_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var referenceTypeParameter = new DefaultClass();
            var genClass = new GenericClass<DefaultClass>(new DefaultClass());

            //Act
            genClass.GenericMethod<DefaultClass>(referenceTypeParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethod", "!!0", genClass, new object[] { referenceTypeParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        [Test]
        public void call_generic_method_passing_reftype_on_generic_class_with_different_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var referenceTypeParameter = new DefaultClass();
            var genClass = new GenericClass<int>(50);

            //Act
            genClass.GenericMethod<DefaultClass>(referenceTypeParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethod", "!!0", genClass, new object[] { referenceTypeParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        [Test]
        public void call_generic_method_passing_both_class_and_method_generic_params_1()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var referenceTypeParameter = new DefaultClass();
            var intParameter = 300;
            var genClass = new GenericClass<int>(50);

            //Act
            genClass.GenericMethodTwo<DefaultClass>(referenceTypeParameter, intParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodTwo", "!!0,!0", genClass, new object[] { referenceTypeParameter, intParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        [Test]
        public void call_generic_method_passing_both_class_and_method_generic_params_2()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var referenceTypeParameter = new DefaultClass();
            GenericClass<DefaultClass> genClass = new GenericClass<DefaultClass>(new DefaultClass());

            //Act
            genClass.GenericMethodTwo<DefaultClass>(referenceTypeParameter, referenceTypeParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodTwo", "!!0,!0", genClass, new object[] { referenceTypeParameter, referenceTypeParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        [Test]
        public void call_generic_method_passing_both_class_and_method_generic_params_3()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var referenceTypeParameter = new DefaultClass();
            var genClass = new GenericClass<DefaultClass>(new DefaultClass());

            //Act
            genClass.GenericMethodTwo<DefaultClass>(null, referenceTypeParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodTwo", "!!0,!0", genClass, new object[] { null, referenceTypeParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        [Test]
        public void call_generic_method_passing_and_returning_reftype_on_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var referenceTypeParameter = new DefaultClass();
            var genClass = new GenericClass<int>(50);

            //Act
            genClass.GenericMethodThree<DefaultClass>(referenceTypeParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodThree", "!!0", genClass, new object[] { referenceTypeParameter }, referenceTypeParameter, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        [Test]
        public void call_generic_method_passing_and_returning_string_on_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var stringParameter = "Pats";
            var genClass = new GenericClass<int>(50);

            //Act
            genClass.GenericMethodThree<String>(stringParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodThree", "!!0", genClass, new object[] { stringParameter }, stringParameter, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        [Test]
        public void call_generic_method_passing_and_returning_valuetype_on_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var intParameter = 42;
            var genClass = new GenericClass<String>("yoAdrian!");

            //Act
            genClass.GenericMethodThree<int>(intParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodThree", "!!0", genClass, new object[] { intParameter }, intParameter, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        [Test]
        public void call_generic_method_on_inner_default_class_of_generic_class_string_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var stringParameter = "yoAdrian!";
            var innerClass = new OuterGenericClass<int>.InnerDefaultClass();

            //Act
            innerClass.InnerGenericMethod<String>(stringParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "InnerGenericMethod", "!!0", innerClass, new object[] { stringParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.OuterGenericClass`1+InnerDefaultClass");
        }

        [Test]
        public void call_generic_method_on_inner_default_class_of_generic_class_reference_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var referenceTypeParameter = new DefaultClass();
            var innerClass = new OuterGenericClass<int>.InnerDefaultClass();

            //Act
            innerClass.InnerGenericMethod<DefaultClass>(referenceTypeParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "InnerGenericMethod", "!!0", innerClass, new object[] { referenceTypeParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.OuterGenericClass`1+InnerDefaultClass");
        }

        [Test]
        public void call_generic_method_on_inner_default_class_of_generic_class_value_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var intParameter = 33;
            var innerClass = new OuterGenericClass<int>.InnerDefaultClass();

            //Act
            innerClass.InnerGenericMethod<int>(intParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "InnerGenericMethod", "!!0", innerClass, new object[] { intParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.OuterGenericClass`1+InnerDefaultClass");
        }

        [Test]
        public void call_generic_method_on_inner_default_class_of_generic_class_and_return_string_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var stringParameter = "yoAdrian!";
            var innerClass = new OuterGenericClass<int>.InnerDefaultClass();

            //Act
            var returnedValue = innerClass.InnerGenericMethodReturnsIt<String>(stringParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "InnerGenericMethodReturnsIt", "!!0", innerClass, new object[] { stringParameter }, stringParameter, null, "NewRelic.Agent.Tests.ProfiledMethods.OuterGenericClass`1+InnerDefaultClass");
        }

        [Test]
        public void call_generic_method_on_inner_default_class_of_generic_class_and_return_reference_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var referenceTypeParameter = new DefaultClass();
            var innerClass = new OuterGenericClass<int>.InnerDefaultClass();

            //Act
            var returnedValue = innerClass.InnerGenericMethodReturnsIt<DefaultClass>(referenceTypeParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "InnerGenericMethodReturnsIt", "!!0", innerClass, new object[] { referenceTypeParameter }, referenceTypeParameter, null, "NewRelic.Agent.Tests.ProfiledMethods.OuterGenericClass`1+InnerDefaultClass");
        }

        [Test]
        public void call_generic_method_on_inner_default_class_of_generic_class_and_return_value_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var intParameter = 4;
            var innerClass = new OuterGenericClass<int>.InnerDefaultClass();

            //Act
            var returnedValue = innerClass.InnerGenericMethodReturnsIt<int>(intParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "InnerGenericMethodReturnsIt", "!!0", innerClass, new object[] { intParameter }, intParameter, null, "NewRelic.Agent.Tests.ProfiledMethods.OuterGenericClass`1+InnerDefaultClass");
        }

        [Test]
        public void call_method_on_inner_default_class_passing_string_as_generic_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var stringParameter = "yoAdrian!";
            var innerClass = new OuterGenericClass<String>.InnerDefaultClass();

            //Act
            innerClass.InnerClassMethodWithClassGenericParam(stringParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "InnerClassMethodWithClassGenericParam", "!0", innerClass, new object[] { stringParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.OuterGenericClass`1+InnerDefaultClass");
        }

        [Test]
        public void call_method_on_inner_default_class_passing_referencetype_as_generic_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var referenceTypeParameter = new DefaultClass();
            var innerClass = new OuterGenericClass<DefaultClass>.InnerDefaultClass();

            //Act
            innerClass.InnerClassMethodWithClassGenericParam(referenceTypeParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "InnerClassMethodWithClassGenericParam", "!0", innerClass, new object[] { referenceTypeParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.OuterGenericClass`1+InnerDefaultClass");
        }

        [Test]
        public void call_method_on_inner_default_class_passing_valuetype_as_generic_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var intParameter = 33;
            var innerClass = new OuterGenericClass<int>.InnerDefaultClass();

            //Act
            innerClass.InnerClassMethodWithClassGenericParam(intParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "InnerClassMethodWithClassGenericParam", "!0", innerClass, new object[] { intParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.OuterGenericClass`1+InnerDefaultClass");
        }

        [Test]
        public void call_method_on_inner_default_class_pass_and_return_string_as_generic_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var stringParameter = "yoAdrian!";
            var innerClass = new OuterGenericClass<String>.InnerDefaultClass();

            //Act
            var returnedValue = innerClass.InnerClassMethodReturnsIt(stringParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "InnerClassMethodReturnsIt", "!0", innerClass, new object[] { stringParameter }, stringParameter, null, "NewRelic.Agent.Tests.ProfiledMethods.OuterGenericClass`1+InnerDefaultClass");
        }

        [Test]
        public void call_method_on_inner_default_class_pass_and_return_referencetype_as_generic_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var referenceTypeParameter = new DefaultClass();
            var innerClass = new OuterGenericClass<DefaultClass>.InnerDefaultClass();

            //Act
            var returnedValue = innerClass.InnerClassMethodReturnsIt(referenceTypeParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "InnerClassMethodReturnsIt", "!0", innerClass, new object[] { referenceTypeParameter }, referenceTypeParameter, null, "NewRelic.Agent.Tests.ProfiledMethods.OuterGenericClass`1+InnerDefaultClass");
        }

        [Test]
        public void call_method_on_inner_default_class_pass_and_return_valuetype_as_generic_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var intParameter = 33;
            var innerClass = new OuterGenericClass<int>.InnerDefaultClass();

            //Act
            var returnedValue = innerClass.InnerClassMethodReturnsIt(intParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "InnerClassMethodReturnsIt", "!0", innerClass, new object[] { intParameter }, intParameter, null, "NewRelic.Agent.Tests.ProfiledMethods.OuterGenericClass`1+InnerDefaultClass");
        }

        [Test]
        public void call_generic_method_with_var_args_on_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            var referenceTypeParameter = new DefaultClass();
            var refTypeParam2 = new DefaultClass();
            var refTypeParam3 = new DefaultClass();
            var genClass = new GenericClass<string>("MVPTom");

            //Act
            genClass.GenericMethodFour<DefaultClass>(referenceTypeParameter, refTypeParam2, refTypeParam3);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodFour", "!!0,!!0[]", genClass, new object[] { referenceTypeParameter, new object[] { refTypeParam2, refTypeParam3 } }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        [Test]
        public void call_generic_method_with_var_args_on_generic_class_2()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            var stringParam1 = "one";
            var stringParam2 = "two";
            var stringParam3 = "three";
            var genClass = new GenericClass<string>("MVPTom");

            //Act
            genClass.GenericMethodFour<String>(stringParam1, stringParam2, stringParam3);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodFour", "!!0,!!0[]", genClass, new object[] { stringParam1, new object[] { stringParam2, stringParam3 } }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }
        #endregion

        #region Static Generic Class Tests
        [Test]
        public void call_method_on_static_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            //Act
            StaticGenericClass<String>.DefaultMethod();

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "DefaultMethod", "", null, new object[] { }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.StaticGenericClass`1");
        }

        [Test]
        public void call_method_passing_reftype_on_static_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            //Act
            StaticGenericClass<DefaultClass>.DefaultMethod();

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "DefaultMethod", "", null, new object[] { }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.StaticGenericClass`1");
        }

        [Test]
        public void call_generic_method_passing_valuetype_on_static_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var intParameter = 10;

            //Act
            StaticGenericClass<String>.GenericMethod<int>(intParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethod", "!!0", null, new object[] { intParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.StaticGenericClass`1");
        }

        [Test]
        public void call_generic_method_passing_reftype_on_static_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var referenceTypeParameter = new DefaultClass();

            //Act
            StaticGenericClass<DefaultClass>.GenericMethod<DefaultClass>(referenceTypeParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethod", "!!0", null, new object[] { referenceTypeParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.StaticGenericClass`1");
        }

        [Test]
        public void call_generic_method_passing_reftype_on_static_generic_class_with_different_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var referenceTypeParameter = new DefaultClass();

            //Act
            StaticGenericClass<int>.GenericMethod<DefaultClass>(referenceTypeParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethod", "!!0", null, new object[] { referenceTypeParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.StaticGenericClass`1");
        }

        [Test]
        public void call_generic_method_pass_class_and_method_generic_params_on_static_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var referenceTypeParameter = new DefaultClass();
            var intParameter = 300;

            //Act
            StaticGenericClass<int>.GenericMethodTwo<DefaultClass>(referenceTypeParameter, intParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodTwo", "!!0,!0", null, new object[] { referenceTypeParameter, intParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.StaticGenericClass`1");
        }

        [Test]
        public void call_generic_method_passing_and_returning_reftype_on_static_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var referenceTypeParameter = new DefaultClass();

            //Act
            StaticGenericClass<int>.GenericMethodThree<DefaultClass>(referenceTypeParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodThree", "!!0", null, new object[] { referenceTypeParameter }, referenceTypeParameter, null, "NewRelic.Agent.Tests.ProfiledMethods.StaticGenericClass`1");
        }

        #endregion

        #region Nested Generics

        [Test]
        public void call_method_passing_generic_class_as_type_in_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            var genClass2 = new GenericClass2<string>("Rain");
            var genClass = new GenericClass<GenericClass2<string>>();

            //Act
            genClass.DefaultMethod(genClass2);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "DefaultMethod", "!0", genClass, new object[] { genClass2 }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        [Test]
        public void call_generic_method_in_generic_class_that_has_generic_gentype()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();

            const int intParameter = 33;
            var genClass = new GenericClass<GenericClass2<string>>();

            //Act
            genClass.GenericMethod<int>(intParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethod", "!!0", genClass, new object[] { intParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        [Test]
        public void call_generic_method_passing_gentype_in_generic_class()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var referenceTypeParameter = new DefaultClass();
            var genClass2 = new GenericClass2<string>("Rain");
            var genClass = new GenericClass<GenericClass2<string>>();

            //Act
            genClass.GenericMethodTwo<DefaultClass>(referenceTypeParameter, genClass2);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodTwo", "!!0,!0", genClass, new object[] { referenceTypeParameter, genClass2 }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass`1");
        }

        #endregion

        [Test]
        public void call_method_on_generic_class_that_implements_generic_interface()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var genClass = new GenericClass3<String>();

            //Act
            var count = genClass.GetCount();

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GetCount", "", genClass, new object[] { }, count, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClass3`1");
        }

        #region Constraints on Generic Type Parameters

        [Test]
        public void call_method_on_generic_class_with_value_type_constraint()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var intParameter = 37;
            var genClass = new GenericClassValueTypeConstraint<int>();

            //Act
            genClass.DefaultMethod(intParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "DefaultMethod", "!0", genClass, new object[] { intParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClassValueTypeConstraint`1");
        }

        [Test]
        public void call_method_on_generic_class_with_multiple_value_type_constraints()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var intParameter = 37;
            var dblParameter = 12.00;
            var genClass = new GenericClassMultipleValueTypeConstraints<int, double>();

            //Act
            genClass.DefaultMethod(intParameter, dblParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "DefaultMethod", "!0,!1", genClass, new object[] { intParameter, dblParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClassMultipleValueTypeConstraints`2");
        }

        [Test]
        public void call_method_on_generic_class_with_multiple_type_constraints()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var intParameter = 37;
            var defaultClass = new DefaultClass();
            var genClass = new GenericClassMultipleTypeConstraints<int, DefaultClass>();

            //Act
            genClass.DefaultMethod(intParameter, defaultClass);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "DefaultMethod", "!0,!1", genClass, new object[] { intParameter, defaultClass }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClassMultipleTypeConstraints`2");
        }

        [Test]
        public void call_generic_method_on_generic_class_with_value_type_constraint()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var dblParameter = 4.00;
            var genClass = new GenericClassValueTypeConstraint<int>();

            //Act
            genClass.GenericMethodOne(dblParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodOne", "!!0", genClass, new object[] { dblParameter }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClassValueTypeConstraint`1");
        }

        [Test]
        public void call_generic_method_with_no_constraint_on_generic_class_with_value_type_constraint()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var dblParameter = 4.00;
            var intParameter = 33;
            var genClass = new GenericClassValueTypeConstraint<int>();

            //Act
            var result = genClass.GenericMethodTwo<double>(dblParameter, intParameter);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodTwo", "!!0,!0", genClass, new object[] { dblParameter, intParameter }, result, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClassValueTypeConstraint`1");
        }

        [Test]
        public void call_generic_method_with_inheritance_constraint_on_generic_class_with_constraint()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var derivedClass = new DerivedClass();
            var genClass = new GenericClassClassConstraint<BaseClass>();

            //Act
            genClass.GenericMethod<DerivedClass>(derivedClass);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethod", "!!0", genClass, new object[] { derivedClass }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClassClassConstraint`1");
        }

        [Test]
        public void call_generic_method_with_interface_inheritance_constraint_on_generic_class_with_constraint()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var impl = new Implementation();
            var genClass = new GenericClassClassConstraint<IInterface>();

            //Act
            genClass.GenericMethod<Implementation>(impl);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethod", "!!0", genClass, new object[] { impl }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClassClassConstraint`1");
        }

        [Test]
        public void call_method_passing_on_generic_class_with_type_constraint_relationship_between_gentypes()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var derivedClass = new DerivedClass();
            var defaultClass = new DefaultClass();
            var genClass = new GenericClassComplexConstraints<DerivedClass, DefaultClass, BaseClass>();

            //Act
            genClass.DefaultMethod(derivedClass, defaultClass);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "DefaultMethod", "!0,!1", genClass, new object[] { derivedClass, defaultClass }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClassComplexConstraints`3");
        }

        [Test]
        public void call_generic_method_with_type_constraint_on_class_generic_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var derivedClass = new DerivedClass();
            var defaultClass = new DefaultClass();
            var genClass = new GenericClassComplexConstraints<DerivedClass, DefaultClass, BaseClass>();

            //Act
            genClass.GenericMethodOne<DefaultClass>(defaultClass, derivedClass);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodOne", "!!0,NewRelic.Agent.Tests.ProfiledMethods.BaseClass", genClass, new object[] { defaultClass, derivedClass }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClassComplexConstraints`3");
        }

        [Test]
        public void call_generic_method_with_multiple_type_constraints_on_class_generic_types()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var baseClass = new BaseClass();
            var derivedClass = new DerivedClass();
            var defaultClass = new DefaultClass();
            var genClass = new GenericClassComplexConstraints<DerivedClass, DefaultClass, BaseClass>();

            //Act
            var returned = genClass.GenericMethodTwo<BaseClass, DefaultClass>(derivedClass, defaultClass, baseClass);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethodTwo", "!!0,!!1,!2", genClass, new object[] { derivedClass, defaultClass, baseClass }, returned, null, "NewRelic.Agent.Tests.ProfiledMethods.GenericClassComplexConstraints`3");
        }
        #endregion

        #region Covariance and Contravariance

        [Test]
        public void call_method_on_class_with_covariant_generic_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var whaleFactory = new CovariantFactory<Whale>();
            var factories = new List<ICovariantFactory<IAnimal>> { whaleFactory };
            IAnimal animal = null;
            ICovariantFactory<IAnimal> animalFactory = null;

            //Act
            foreach (var factory in factories)
            {
                animalFactory = factory;
                animal = animalFactory.CreateInstance();
                animal.Speak();
            }

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "CreateInstance", "", animalFactory, new object[] { }, animal, null, "NewRelic.Agent.Tests.ProfiledMethods.CovariantFactory`1");
        }

        [Test]
        public void call_generic_method_on_class_with_covariant_generic_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var whaleFactory = new CovariantFactory<Whale>();
            var factories = new List<ICovariantFactory<IAnimal>> { whaleFactory };
            IAnimal animal = null;
            ICovariantFactory<IAnimal> animalFactory = null;
            var giraffe = new Giraffe();

            //Act
            foreach (var factory in factories)
            {
                animalFactory = factory;
                animal = animalFactory.GenericMethod<Giraffe>(giraffe);
            }

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethod", "!!0", animalFactory, new object[] { giraffe }, animal, null, "NewRelic.Agent.Tests.ProfiledMethods.CovariantFactory`1");
        }

        [Test]
        public void call_method_on_class_with_contravariant_generic_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var giraffe = new Giraffe();
            var giraffes = new List<Giraffe> { giraffe };
            var animalProc = new ContravariantProcessor<IAnimal>();
            var giraffeProcessor = animalProc;

            //Act
            giraffeProcessor.Process(giraffes);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "Process", "System.Collections.Generic.IEnumerable`1[!0]", giraffeProcessor, new object[] { giraffes }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.ContravariantProcessor`1");
        }

        [Test]
        public void call_generic_method_on_class_with_contravariant_generic_type()
        {
            //Arrange
            var getTracerParameters = DefaultGetTracerImplementation();
            var finishTracerParameters = DefaultFinishTracerImplementation();
            var giraffe = new Giraffe();
            var giraffes = new List<Giraffe> { giraffe };
            var animalProc = new ContravariantProcessor<IAnimal>();
            var giraffeProcessor = animalProc;
            var whale = new Whale();

            //Act
            giraffeProcessor.GenericMethod<Whale>(whale);

            //Assert
            ValidateTracers(getTracerParameters, finishTracerParameters, "GenericMethod", "!!0", giraffeProcessor, new object[] { whale }, null, null, "NewRelic.Agent.Tests.ProfiledMethods.ContravariantProcessor`1");
        }

        #endregion
    }
}
