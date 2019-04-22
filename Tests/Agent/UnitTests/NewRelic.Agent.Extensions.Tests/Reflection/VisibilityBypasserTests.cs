using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;


namespace NewRelic.Reflection.UnitTests
{
#pragma warning disable 414
	public class Base
	{
		public virtual String VirtualProperty { get { return "base property"; } }
	}
	public class Derived : Base
	{
		public override String VirtualProperty { get { return "derived property"; } }
	}

	public class PublicOuter
	{
		private class PrivateInner
		{
			public static PrivateInner Create() { return new PrivateInner(); }
			public String ConstructorCalled = String.Empty;
			private PrivateInner() { ConstructorCalled = "0 parameters"; }
			private PrivateInner(Boolean a) { ConstructorCalled = "1 parameters"; }
			private PrivateInner(Boolean a, String b) { ConstructorCalled = "2 parameters"; }
			private Int32 Int32ReturningMethod() { return 5; }
			private PrivateInner PrivateInnerReturningMethod() { return new PrivateInner(); }
			private readonly Int32 _int32Field = 3;
			private PrivateInner PrivateInnerProperty { get { return new PrivateInner(); } }
			private Int32 Int32Property { get { return 11; } }
			public override string ToString() { return ConstructorCalled; }
		}

		public static PublicOuter Create() { return new PublicOuter(); }
		public static Object CreatePrivateInner() { return PrivateInner.Create(); }
		public String ConstructorCalled = String.Empty;
		private PublicOuter() { ConstructorCalled = "0 parameters"; }
		private PublicOuter(Boolean a) { ConstructorCalled = "1 parameters"; }
		private PublicOuter(Boolean a, String b) { ConstructorCalled = "2 parameters"; }
		private Int32 Int32ReturningMethod() { return 9; }
		private PrivateInner PrivateInnerReturningMethod() { return PrivateInner.Create(); }
		private PublicOuter PublicOuterReturningMethod() { return new PublicOuter(); }
		public readonly Base DerivedField = new Derived();
		private readonly Int32 _int32Field = 7;
		private PublicOuter PublicOuterProperty { get { return new PublicOuter(); } }
		private Int32 Int32Property { get { return 13; } }
		private String StringTakingAndReturningMethod(String @string) { return @string; }
		private string _writableStringField = "stringFieldValue";
		public string GetWritableStringField { get { return _writableStringField; } }
		private int _writeableIntField = 7;
		public int GetWriteableIntField { get { return _writeableIntField; } }
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

			var fieldAccessor = VisibilityBypasser.Instance.GenerateFieldReadAccessor<PublicOuter, Int32>(fieldName);
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

			var fieldAccessor = VisibilityBypasser.Instance.GenerateFieldReadAccessor<Int32>(assemblyName, typeName, fieldName);
			var actualValue = fieldAccessor(privateInner);

			Assert.AreEqual(expectedValue, actualValue);
		}

		[Test]
		public void base_class_return_type()
		{
			var fieldName = "DerivedField";
			var publicOuter = PublicOuter.Create();
			var expectedValue = publicOuter.DerivedField;

			var fieldAccessor = VisibilityBypasser.Instance.GenerateFieldReadAccessor<PublicOuter, Object>(fieldName);
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

			Assert.Throws<KeyNotFoundException>(() => VisibilityBypasser.Instance.GenerateFieldReadAccessor<PublicOuter, Object>(fieldName));
		}

		[Test]
		public void incorrect_result_type()
		{
			var fieldName = "_int32Field";
			var publicOuter = PublicOuter.Create();

			Assert.Throws<Exception>(() => VisibilityBypasser.Instance.GenerateFieldReadAccessor<PublicOuter, String>(fieldName));
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

			var methodCaller = VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<PublicOuter, Int32>(methodName);
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

			var methodCaller = VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<Object>(assemblyName, typeName, methodName);
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

			var methodCaller = VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<Int32>(assemblyName, typeName, methodName);
			var actualValue = methodCaller(privateInner);

			Assert.AreEqual(expectedValue, actualValue);
		}

		[Test]
		public void incorrect_result_type()
		{
			var methodName = "PublicOuterReturningMethod";

			Assert.Throws<Exception>(() => VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<PublicOuter, Int32>(methodName));
		}

		[Test]
		public void base_result_type()
		{
			var methodName = "Int32ReturningMethod";
			var expectedValue = 9;
			var publicOuter = PublicOuter.Create();

			var methodCaller = VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<PublicOuter, Object>(methodName);
			var actualValue = methodCaller(publicOuter);

			Assert.AreEqual(expectedValue, actualValue);
		}

		[Test]
		public void no_method_by_that_name()
		{
			Assert.Throws<KeyNotFoundException>(() => VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<PublicOuter, Object>("does_not_exist"));
		}

		[Test]
		public void method_with_parameter()
		{
			var methodName = "StringTakingAndReturningMethod";
			var publicOuter = PublicOuter.Create();
			var expectedValue = "foo";

			var methodCaller = VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<PublicOuter, String, String>(methodName);
			var actualValue = methodCaller(publicOuter, expectedValue);

			Assert.AreEqual(expectedValue, actualValue);
		}

		[Test]
		public void method_with_parameter_unknown_type()
		{
			var methodName = "StringTakingAndReturningMethod";
			var publicOuter = PublicOuter.Create();
			var expectedValue = "foo";

			var methodCaller = VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<PublicOuter, Object, String>(methodName);
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

			var methodCaller = VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<String, String>(assemblyName, typeName, methodName);
			var actualValue = methodCaller(publicOuter, expectedValue);

			Assert.AreEqual(expectedValue, actualValue);
		}
	}

	public class PropertyAccessTests
	{
		[Test]
		public void private_value_property_on_public_object()
		{
			var propertyName = "Int32Property";
			var expectedValue = 13;
			var publicOuter = PublicOuter.Create();

			var methodCaller = VisibilityBypasser.Instance.GeneratePropertyAccessor<PublicOuter, Int32>(propertyName);
			var actualValue = methodCaller(publicOuter);

			Assert.AreEqual(expectedValue, actualValue);
		}

		[Test]
		public void private_reference_property_on_public_object()
		{
			var propertyName = "PublicOuterProperty";
			var publicOuter = PublicOuter.Create();

			var methodCaller = VisibilityBypasser.Instance.GeneratePropertyAccessor<PublicOuter, PublicOuter>(propertyName);
			var actualValue = methodCaller(publicOuter);

			Assert.NotNull(actualValue);
		}

		[Test]
		public void private_reference_property_on_private_object()
		{
			var assemblyName = Assembly.GetExecutingAssembly().FullName;
			var typeName = "NewRelic.Reflection.UnitTests.PublicOuter+PrivateInner";
			var propertyName = "PrivateInnerProperty";
			var privateInner = PublicOuter.CreatePrivateInner();

			var methodCaller = VisibilityBypasser.Instance.GeneratePropertyAccessor<Object>(assemblyName, typeName, propertyName);
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

			var methodCaller = VisibilityBypasser.Instance.GeneratePropertyAccessor<Int32>(assemblyName, typeName, propertyName);
			var actualValue = methodCaller(privateInner);

			Assert.AreEqual(expectedValue, actualValue);
		}

		[Test]
		public void incorrect_result_type()
		{
			var propertyName = "PublicOuterProperty";

			Assert.Throws<Exception>(() => VisibilityBypasser.Instance.GeneratePropertyAccessor<PublicOuter, Int32>(propertyName));
		}

		[Test]
		public void base_result_type()
		{
			var propertyName = "Int32Property";
			var expectedValue = 13;
			var publicOuter = PublicOuter.Create();

			var methodCaller = VisibilityBypasser.Instance.GeneratePropertyAccessor<PublicOuter, Object>(propertyName);
			var actualValue = methodCaller(publicOuter);

			Assert.AreEqual(expectedValue, actualValue);
		}

		[Test]
		public void no_method_by_that_name()
		{
			Assert.Throws<KeyNotFoundException>(() => VisibilityBypasser.Instance.GeneratePropertyAccessor<PublicOuter, Object>("does_not_exist"));
		}

		[Test]
		public void virtual_property()
		{
			var propertyName = "VirtualProperty";
			var expectedValue = "derived property";
			var derived = new Derived() as Base;

			var methodCaller = VisibilityBypasser.Instance.GeneratePropertyAccessor<Base, String>(propertyName);
			var actualValue = methodCaller(derived);

			Assert.AreEqual(expectedValue, actualValue);
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
			var publicOuterFactory = VisibilityBypasser.Instance.GenerateTypeFactory<Boolean, PublicOuter>();
			var publicOuter = publicOuterFactory(true);

			Assert.AreEqual("1 parameters", publicOuter.ConstructorCalled);
		}

		[Test]
		public void private_two_parameter_constructor_on_public_object()
		{
			var publicOuterFactory = VisibilityBypasser.Instance.GenerateTypeFactory<Boolean, String, PublicOuter>();
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

			var publicOuterFactory = VisibilityBypasser.Instance.GenerateTypeFactory<Boolean>(assemblyName, typeName);
			var publicOuter = publicOuterFactory(true);

			Assert.AreEqual("1 parameters", publicOuter.ToString());
		}

		[Test]
		public void private_two_parameter_constructor_on_private_object()
		{
			var assemblyName = Assembly.GetExecutingAssembly().FullName;
			var typeName = "NewRelic.Reflection.UnitTests.PublicOuter+PrivateInner";

			var publicOuterFactory = VisibilityBypasser.Instance.GenerateTypeFactory<Boolean, String>(assemblyName, typeName);
			var publicOuter = publicOuterFactory(true, "rawr");

			Assert.AreEqual("2 parameters", publicOuter.ToString());
		}

		[Test]
		public void no_constructor_found()
		{
			Assert.Throws<Exception>(() => VisibilityBypasser.Instance.GenerateTypeFactory<Boolean, Int32, PublicOuter>());
		}
	}
}
