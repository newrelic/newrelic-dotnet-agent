// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;


namespace NewRelic.Reflection.UnitTests
{
#pragma warning disable 414
    public class Base
    {
        public virtual string VirtualProperty { get { return "base property"; } }
    }
    public class Derived : Base
    {
        public override string VirtualProperty { get { return "derived property"; } }
    }

    public class PublicOuter
    {
        private class PrivateInner
        {
            public static PrivateInner Create() { return new PrivateInner(); }
            public string ConstructorCalled = string.Empty;
            private PrivateInner() { ConstructorCalled = "0 parameters"; }
            private PrivateInner(bool a) { ConstructorCalled = "1 parameters"; }
            private PrivateInner(bool a, string b) { ConstructorCalled = "2 parameters"; }
            private int Int32ReturningMethod() { return 5; }
            private PrivateInner PrivateInnerReturningMethod() { return new PrivateInner(); }
            private readonly int _int32Field = 3;
            private Dictionary<string, object> DictionaryProperty { get; set; } = new Dictionary<string, object>() { { "originalKey", "originalValue" } };
            private int Int32Property { get; set; } =  11;
            public override string ToString() { return ConstructorCalled; }
        }

        public static PublicOuter Create() { return new PublicOuter(); }
        public static object CreatePrivateInner() { return PrivateInner.Create(); }
        public string ConstructorCalled = string.Empty;
        private PublicOuter() { ConstructorCalled = "0 parameters"; }
        private PublicOuter(bool a) { ConstructorCalled = "1 parameters"; }
        private PublicOuter(bool a, string b) { ConstructorCalled = "2 parameters"; }
        private int Int32ReturningMethod() { return 9; }
        private PrivateInner PrivateInnerReturningMethod() { return PrivateInner.Create(); }
        private PublicOuter PublicOuterReturningMethod() { return new PublicOuter(); }
        public readonly Base DerivedField = new Derived();
        private readonly int _int32Field = 7;
        private Dictionary<string, object> DictionaryProperty { get; set; } = new Dictionary<string, object> { { "originalKey", "originalValue" } };
        private int Int32Property { get; set; } = 13;
        private string StringTakingAndReturningMethod(string @string) { return @string; }
        private string _writableStringField = "stringFieldValue";
        public string GetWritableStringField { get { return _writableStringField; } }
        private int _writeableIntField = 7;
        public int GetWriteableIntField { get { return _writeableIntField; } }
    }

    public static class PublicStatic
    {
        public static int GetANumber() => 3;
    }

#pragma warning restore 414

    public class FieldAccessTests
    {
        [Test]
        public void private_int32_on_public_object()
        {
            var fieldName = "_int32Field";
            var publicOuter = PublicOuter.Create();
            var expectedValue = 7;

            var fieldAccessor = VisibilityBypasser.Instance.GenerateFieldReadAccessor<PublicOuter, int>(fieldName);
            var actualValue = fieldAccessor(publicOuter);

            Assert.AreEqual(expectedValue, actualValue);
        }

        [Test]
        public void private_int32_on_private_object()
        {
            var assemblyName = Assembly.GetExecutingAssembly().FullName;
            var typeName = "NewRelic.Reflection.UnitTests.PublicOuter+PrivateInner";
            var fieldName = "_int32Field";
            var privateInner = PublicOuter.CreatePrivateInner();
            var expectedValue = 3;

            var fieldAccessor = VisibilityBypasser.Instance.GenerateFieldReadAccessor<int>(assemblyName, typeName, fieldName);
            var actualValue = fieldAccessor(privateInner);

            Assert.AreEqual(expectedValue, actualValue);
        }

        [Test]
        public void base_class_return_type()
        {
            var fieldName = "DerivedField";
            var publicOuter = PublicOuter.Create();
            var expectedValue = publicOuter.DerivedField;

            var fieldAccessor = VisibilityBypasser.Instance.GenerateFieldReadAccessor<PublicOuter, object>(fieldName);
            var actualValue = fieldAccessor(publicOuter);

            Assert.AreEqual(expectedValue, actualValue);
        }

        [Test]
        public void derived_class_return_type()
        {
            var fieldName = "DerivedField";
            var publicOuter = PublicOuter.Create();

            Assert.Throws<Exception>(() => VisibilityBypasser.Instance.GenerateFieldReadAccessor<PublicOuter, Derived>(fieldName));
        }

        [Test]
        public void non_existant_field()
        {
            var fieldName = "does_not_exist";

            Assert.Throws<KeyNotFoundException>(() => VisibilityBypasser.Instance.GenerateFieldReadAccessor<PublicOuter, object>(fieldName));
        }

        [Test]
        public void incorrect_result_type()
        {
            var fieldName = "_int32Field";
            var publicOuter = PublicOuter.Create();

            Assert.Throws<Exception>(() => VisibilityBypasser.Instance.GenerateFieldReadAccessor<PublicOuter, string>(fieldName));
        }

        [Test]
        public void correctly_write_private_object_reference()
        {
            var fieldName = "_writableStringField";
            var publicOuter = PublicOuter.Create();
            var newStringValue = "newStringValue";
            var action = VisibilityBypasser.Instance.GenerateFieldWriteAccessor<string>(typeof(PublicOuter), fieldName);
            Assert.DoesNotThrow(() => action(publicOuter, newStringValue));
            Assert.That(publicOuter.GetWritableStringField == newStringValue);
        }

        [Test]
        public void incorrect_object_reference_does_not_throw()
        {
            var fieldName = "_writableStringField";
            var publicOuter = PublicOuter.Create();
            var newStringValue = "newStringValue";
            var action = VisibilityBypasser.Instance.GenerateFieldWriteAccessor<string>(typeof(PublicOuter), fieldName);
            Assert.DoesNotThrow(() => action(new List<string>(), newStringValue));
            Assert.That(publicOuter.GetWritableStringField != newStringValue);
        }

        [Test]
        public void correctly_write_private_value_type()
        {
            var fieldName = "_writeableIntField";
            var publicOuter = PublicOuter.Create();
            var newIntValue = 42;
            var action = VisibilityBypasser.Instance.GenerateFieldWriteAccessor<int>(typeof(PublicOuter), fieldName);
            Assert.DoesNotThrow(() => action(publicOuter, newIntValue));
            Assert.That(publicOuter.GetWriteableIntField == newIntValue);
        }
    }

    public class MethodAccessTests
    {
        [Test]
        public void private_reference_returning_method_on_public_object()
        {
            var methodName = "PublicOuterReturningMethod";
            var publicOuter = PublicOuter.Create();

            var methodCaller = VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<PublicOuter, PublicOuter>(methodName);
            var actualValue = methodCaller(publicOuter);

            Assert.NotNull(actualValue);
        }

        [Test]
        public void private_value_returning_method_on_public_object()
        {
            var methodName = "Int32ReturningMethod";
            var expectedValue = 9;
            var publicOuter = PublicOuter.Create();

            var methodCaller = VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<PublicOuter, int>(methodName);
            var actualValue = methodCaller(publicOuter);

            Assert.AreEqual(expectedValue, actualValue);
        }

        [Test]
        public void private_reference_returnting_method_on_private_object()
        {
            var assemblyName = Assembly.GetExecutingAssembly().FullName;
            var typeName = "NewRelic.Reflection.UnitTests.PublicOuter+PrivateInner";
            var methodName = "PrivateInnerReturningMethod";
            var privateInner = PublicOuter.CreatePrivateInner();

            var methodCaller = VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<object>(assemblyName, typeName, methodName);
            var actualValue = methodCaller(privateInner);

            Assert.NotNull(actualValue);
        }

        [Test]
        public void private_value_returning_method_on_private_object()
        {
            var assemblyName = Assembly.GetExecutingAssembly().FullName;
            var typeName = "NewRelic.Reflection.UnitTests.PublicOuter+PrivateInner";
            var methodName = "Int32ReturningMethod";
            var expectedValue = 5;
            var privateInner = PublicOuter.CreatePrivateInner();

            var methodCaller = VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<int>(assemblyName, typeName, methodName);
            var actualValue = methodCaller(privateInner);

            Assert.AreEqual(expectedValue, actualValue);
        }

        [Test]
        public void incorrect_result_type()
        {
            var methodName = "PublicOuterReturningMethod";

            Assert.Throws<Exception>(() => VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<PublicOuter, int>(methodName));
        }

        [Test]
        public void base_result_type()
        {
            var methodName = "Int32ReturningMethod";
            var expectedValue = 9;
            var publicOuter = PublicOuter.Create();

            var methodCaller = VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<PublicOuter, object>(methodName);
            var actualValue = methodCaller(publicOuter);

            Assert.AreEqual(expectedValue, actualValue);
        }

        [Test]
        public void no_method_by_that_name()
        {
            Assert.Throws<KeyNotFoundException>(() => VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<PublicOuter, object>("does_not_exist"));
        }

        [Test]
        public void method_with_parameter()
        {
            var methodName = "StringTakingAndReturningMethod";
            var publicOuter = PublicOuter.Create();
            var expectedValue = "foo";

            var methodCaller = VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<PublicOuter, string, string>(methodName);
            var actualValue = methodCaller(publicOuter, expectedValue);

            Assert.AreEqual(expectedValue, actualValue);
        }

        [Test]
        public void method_with_parameter_unknown_type()
        {
            var methodName = "StringTakingAndReturningMethod";
            var publicOuter = PublicOuter.Create();
            var expectedValue = "foo";

            var methodCaller = VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<PublicOuter, object, string>(methodName);
            var actualValue = methodCaller(publicOuter, expectedValue);

            Assert.AreEqual(expectedValue, actualValue);
        }

        [Test]
        public void method_with_parameter_unknown_instance_types()
        {
            var assemblyName = Assembly.GetExecutingAssembly().FullName;
            var typeName = "NewRelic.Reflection.UnitTests.PublicOuter";
            var methodName = "StringTakingAndReturningMethod";
            var publicOuter = PublicOuter.Create();
            var expectedValue = "foo";

            var methodCaller = VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<string, string>(assemblyName, typeName, methodName);
            var actualValue = methodCaller(publicOuter, expectedValue);

            Assert.AreEqual(expectedValue, actualValue);
        }
    }

    public class PropertyAccessorTests
    {
        [Test]
        public void private_value_property_on_public_object()
        {
            var propertyName = "Int32Property";
            var expectedValue = 13;
            var publicOuter = PublicOuter.Create();

            var methodCaller = VisibilityBypasser.Instance.GeneratePropertyAccessor<PublicOuter, int>(propertyName);
            var actualValue = methodCaller(publicOuter);

            Assert.AreEqual(expectedValue, actualValue);
        }

        [Test]
        public void private_reference_property_on_public_object()
        {
            var propertyName = "DictionaryProperty";
            var publicOuter = PublicOuter.Create();

            var methodCaller = VisibilityBypasser.Instance.GeneratePropertyAccessor<PublicOuter, Dictionary<string, object>>(propertyName);
            var actualValue = methodCaller(publicOuter);

            Assert.NotNull(actualValue);
            Assert.That((string)actualValue["originalKey"] == "originalValue");
        }

        [Test]
        public void private_reference_property_on_private_object()
        {
            var assemblyName = Assembly.GetExecutingAssembly().FullName;
            var typeName = "NewRelic.Reflection.UnitTests.PublicOuter+PrivateInner";
            var propertyName = "DictionaryProperty";
            var privateInner = PublicOuter.CreatePrivateInner();

            var methodCaller = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(assemblyName, typeName, propertyName);
            var actualValue = methodCaller(privateInner);

            Assert.NotNull(actualValue);
        }

        [Test]
        public void private_value_property_on_private_object()
        {
            var assemblyName = Assembly.GetExecutingAssembly().FullName;
            var typeName = "NewRelic.Reflection.UnitTests.PublicOuter+PrivateInner";
            var propertyName = "Int32Property";
            var expectedValue = 11;
            var privateInner = PublicOuter.CreatePrivateInner();

            var methodCaller = VisibilityBypasser.Instance.GeneratePropertyAccessor<int>(assemblyName, typeName, propertyName);
            var actualValue = methodCaller(privateInner);

            Assert.AreEqual(expectedValue, actualValue);
        }

        [Test]
        public void incorrect_result_type()
        {
            var propertyName = "DictionaryProperty";

            Assert.Throws<Exception>(() => VisibilityBypasser.Instance.GeneratePropertyAccessor<PublicOuter, int>(propertyName));
        }

        [Test]
        public void base_result_type()
        {
            var propertyName = "Int32Property";
            var expectedValue = 13;
            var publicOuter = PublicOuter.Create();

            var methodCaller = VisibilityBypasser.Instance.GeneratePropertyAccessor<PublicOuter, object>(propertyName);
            var actualValue = methodCaller(publicOuter);

            Assert.AreEqual(expectedValue, actualValue);
        }

        [Test]
        public void no_method_by_that_name()
        {
            Assert.Throws<KeyNotFoundException>(() => VisibilityBypasser.Instance.GeneratePropertyAccessor<PublicOuter, object>("does_not_exist"));
        }

        [Test]
        public void virtual_property()
        {
            var propertyName = "VirtualProperty";
            var expectedValue = "derived property";
            var derived = new Derived() as Base;

            var methodCaller = VisibilityBypasser.Instance.GeneratePropertyAccessor<Base, string>(propertyName);
            var actualValue = methodCaller(derived);

            Assert.AreEqual(expectedValue, actualValue);
        }
    }

    public class PropertySetterTests
    {
        [Test]
        public void set_private_value_property_on_public_object()
        {
            var propertyName = "Int32Property";
            var publicOuter = PublicOuter.Create();

            var getter = VisibilityBypasser.Instance.GeneratePropertyAccessor<PublicOuter, int>(propertyName);
            var originalValue = getter(publicOuter);

            var setter = VisibilityBypasser.Instance.GeneratePropertySetter<int>(publicOuter, propertyName);

            setter(originalValue + 1);

            var newValue = getter(publicOuter);

            Assert.AreEqual(newValue, originalValue + 1);
        }

        [Test]
        public void set_private_reference_property_on_public_object()
        {
            var propertyName = "DictionaryProperty";
            var publicOuter = PublicOuter.Create();

            var getter = VisibilityBypasser.Instance.GeneratePropertyAccessor<PublicOuter, Dictionary<string, object>>(propertyName);
            var originalValue = getter(publicOuter);

            var setter = VisibilityBypasser.Instance.GeneratePropertySetter<Dictionary<string,object>>(publicOuter, propertyName);

            setter(new Dictionary<string, object>() { { "newKey", "newValue" } });

            var newValue = getter(publicOuter);

            Assert.AreNotSame(originalValue, newValue);
            Assert.That((string)newValue["newKey"] == "newValue");
        }

        [Test]
        public void set_private_reference_property_on_private_object()
        {
            var assemblyName = Assembly.GetExecutingAssembly().FullName;
            var typeName = "NewRelic.Reflection.UnitTests.PublicOuter+PrivateInner";
            var propertyName = "DictionaryProperty";
            var privateInner = PublicOuter.CreatePrivateInner();

            var getter = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(assemblyName, typeName, propertyName);
            var originalValue = getter(privateInner);

            var setter = VisibilityBypasser.Instance.GeneratePropertySetter<Dictionary<string, object>>(privateInner, propertyName);

            setter(new Dictionary<string, object>() { { "newKey", "newValue" } });

            var newValue = (Dictionary<string,object>)getter(privateInner);

            Assert.AreNotSame(originalValue, newValue);
            Assert.That((string)newValue["newKey"] == "newValue");
        }

        [Test]
        public void set_private_value_property_on_private_object()
        {
            var assemblyName = Assembly.GetExecutingAssembly().FullName;
            var typeName = "NewRelic.Reflection.UnitTests.PublicOuter+PrivateInner";
            var propertyName = "Int32Property";
            var privateInner = PublicOuter.CreatePrivateInner();

            var getter = VisibilityBypasser.Instance.GeneratePropertyAccessor<int>(assemblyName, typeName, propertyName);
            var originalValue = getter(privateInner);

            var setter = VisibilityBypasser.Instance.GeneratePropertySetter<int>(privateInner, propertyName);
            setter(originalValue + 1);

            var newValue = getter(privateInner);

            Assert.AreEqual(newValue, originalValue + 1);
        }

        [Test]
        public void setter_with_incorrect_target_type()
        {
            var propertyName = "DictionaryProperty";
            var publicOuter = PublicOuter.Create();

            Assert.Throws<ArgumentException>(() => VisibilityBypasser.Instance.GeneratePropertySetter<int>(publicOuter, propertyName));
        }

        [Test]
        public void setter_with_incorrect_property_name()
        {
            var publicOuter = PublicOuter.Create();

            Assert.Throws<KeyNotFoundException>(() => VisibilityBypasser.Instance.GeneratePropertySetter<int>(publicOuter, "does_not_exist"));
        }

    }

    public class ConstructorAccessTests
    {
        [Test]
        public void private_parameterless_constructor_on_public_object()
        {
            var publicOuterFactory = VisibilityBypasser.Instance.GenerateTypeFactory<PublicOuter>();
            var publicOuter = publicOuterFactory();

            Assert.AreEqual("0 parameters", publicOuter.ConstructorCalled);
        }

        [Test]
        public void private_one_parameter_constructor_on_public_object()
        {
            var publicOuterFactory = VisibilityBypasser.Instance.GenerateTypeFactory<bool, PublicOuter>();
            var publicOuter = publicOuterFactory(true);

            Assert.AreEqual("1 parameters", publicOuter.ConstructorCalled);
        }

        [Test]
        public void private_two_parameter_constructor_on_public_object()
        {
            var publicOuterFactory = VisibilityBypasser.Instance.GenerateTypeFactory<bool, string, PublicOuter>();
            var publicOuter = publicOuterFactory(true, "rawr");

            Assert.AreEqual("2 parameters", publicOuter.ConstructorCalled);
        }

        [Test]
        public void private_parameterless_constructor_on_private_object()
        {
            var assemblyName = Assembly.GetExecutingAssembly().FullName;
            var typeName = "NewRelic.Reflection.UnitTests.PublicOuter+PrivateInner";

            var publicOuterFactory = VisibilityBypasser.Instance.GenerateTypeFactory(assemblyName, typeName);
            var publicOuter = publicOuterFactory();

            Assert.AreEqual("0 parameters", publicOuter.ToString());
        }

        [Test]
        public void private_one_parameter_constructor_on_private_object()
        {
            var assemblyName = Assembly.GetExecutingAssembly().FullName;
            var typeName = "NewRelic.Reflection.UnitTests.PublicOuter+PrivateInner";

            var publicOuterFactory = VisibilityBypasser.Instance.GenerateTypeFactory<bool>(assemblyName, typeName);
            var publicOuter = publicOuterFactory(true);

            Assert.AreEqual("1 parameters", publicOuter.ToString());
        }

        [Test]
        public void private_two_parameter_constructor_on_private_object()
        {
            var assemblyName = Assembly.GetExecutingAssembly().FullName;
            var typeName = "NewRelic.Reflection.UnitTests.PublicOuter+PrivateInner";

            var publicOuterFactory = VisibilityBypasser.Instance.GenerateTypeFactory<bool, string>(assemblyName, typeName);
            var publicOuter = publicOuterFactory(true, "rawr");

            Assert.AreEqual("2 parameters", publicOuter.ToString());
        }

        [Test]
        public void no_constructor_found()
        {
            Assert.Throws<Exception>(() => VisibilityBypasser.Instance.GenerateTypeFactory<bool, int, PublicOuter>());
        }

        [Test]
        public void test_input_validation()
        {
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateFieldWriteAccessor<string>(null, "foo", "bar"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateFieldWriteAccessor<string>("foo", null, "bar"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateFieldWriteAccessor<string>("foo", "bar", null));

            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateFieldWriteAccessor<string>(null, "foo"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateFieldWriteAccessor<string>(typeof(string), null));

            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateFieldWriteAccessor<string>(null, "foo"));

            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateFieldReadAccessor<string>(null, "foo", "bar"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateFieldReadAccessor<string>("foo", null, "bar"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateFieldReadAccessor<string>("foo", "bar", null));

            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateFieldReadAccessor<string>(null, "foo"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateFieldReadAccessor<string>(typeof(string), null));

            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateFieldReadAccessor<string>(null, "foo"));

            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<PublicOuter, PublicOuter>(null));

            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<string>(null, "foo", "bar"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<string>("foo", null, "foo"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<string>("foo", "bar", null));

            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<PublicOuter, string>(null, "foo", "bar"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<PublicOuter, string>("foo", null, "bar"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<PublicOuter, string>("foo", "bar", null));

            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<PublicOuter>(null, "foo", "bar", "baz"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<PublicOuter>("foo", null, "bar", "baz"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<PublicOuter>("foo", "bar", null, "baz"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<PublicOuter>("foo", "bar", null, "baz"));

            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(null, "foo"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(typeof(string), null));

            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GeneratePropertyAccessor<PublicOuter, string>(null));

            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(null, "foo", "bar"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GeneratePropertyAccessor<string>("foo", null, "foo"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GeneratePropertyAccessor<string>("foo", "bar", null));

            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GeneratePropertySetter<string>(null, "foo"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GeneratePropertySetter<string>("foo", null));

            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateTypeFactory(null, "foo"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateTypeFactory("foo", null));
                                                                                   
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateTypeFactory<string>(null, "foo"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateTypeFactory<string>("foo", null));

            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateTypeFactory<string, string>(null, "foo"));
            Assert.Throws<ArgumentNullException>(() => VisibilityBypasser.Instance.GenerateTypeFactory<string, string>("foo", null));
        }
    }
    public class StaticMethodTests
    {
        [Test]
        public void test_static_generator()
        {
            var assemblyName = Assembly.GetExecutingAssembly().FullName;
            var typeName = "NewRelic.Reflection.UnitTests.PublicStatic";
            var methodName = "GetANumber";

            var method = VisibilityBypasser.Instance.GenerateParameterlessStaticMethodCaller<int>(assemblyName, typeName, methodName);
            var result = method();
            Assert.AreEqual(3, result);

            Assert.Throws<KeyNotFoundException>(() => VisibilityBypasser.Instance.GenerateParameterlessStaticMethodCaller<int>(assemblyName, typeName, "NoSuchMethod"));
        }
    }
}
