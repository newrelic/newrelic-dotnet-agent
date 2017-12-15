using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using JetBrains.Annotations;

namespace NewRelic.Reflection
{
	public interface IReflection
	{
		ConstructorInfo GetConstructor(Type type, Type[] parameterTypes);
	}

	public abstract class VisibilityBypasserBase
	{
		private readonly IReflection reflection;

		public VisibilityBypasserBase(IReflection reflection)
		{
			this.reflection = reflection;
		}

#region Field Access

		[NotNull]
		public Func<Object, TResult> GenerateFieldAccessor<TResult>([NotNull] String assemblyName, [NotNull] String typeName, [NotNull] String fieldName)
		{
			if (assemblyName == null)
				throw new ArgumentNullException("assemblyName");
			if (typeName == null)
				throw new ArgumentNullException("typeName");
			if (fieldName == null)
				throw new ArgumentNullException("fieldName");

			var ownerType = GetType(assemblyName, typeName);
			return GenerateFieldAccessor<TResult>(ownerType, fieldName);
		}

		[NotNull]
		public Func<Object, TResult> GenerateFieldAccessor<TResult>([NotNull] Type ownerType, [NotNull] String fieldName)
		{
			if (ownerType == null)
				throw new ArgumentNullException("ownerType");
			if (fieldName == null)
				throw new ArgumentNullException("fieldName");

			var dynamicMethod = GenerateFieldAccessorInternal<TResult>(ownerType, fieldName);
			return (Func<Object, TResult>)dynamicMethod.CreateDelegate(typeof(Func<Object, TResult>));
		}

		[NotNull]
		public Func<TOwner, TResult> GenerateFieldAccessor<TOwner, TResult>([NotNull] String fieldName)
		{
			if (fieldName == null)
				throw new ArgumentNullException("fieldName");

			var dynamicMethod = GenerateFieldAccessorInternal<TResult>(typeof(TOwner), fieldName);
			return (Func<TOwner, TResult>)dynamicMethod.CreateDelegate(typeof(Func<TOwner, TResult>));
		}

		[NotNull]
		private static DynamicMethod GenerateFieldAccessorInternal<TResult>([NotNull]Type ownerType, [NotNull]String fieldName)
		{
			var fieldInfo = GetFieldInfo(ownerType, fieldName);
			return GenerateFieldAccessorInternal<TResult>(fieldInfo);
		}

		[NotNull]
		private static DynamicMethod GenerateFieldAccessorInternal<TResult>([NotNull]FieldInfo fieldInfo)
		{
			var resultType = typeof (TResult);
			if (!resultType.IsAssignableFrom(fieldInfo.FieldType))
				throw new Exception(String.Format("The return type for field {0} does not inherit or implement {1}", fieldInfo.Name, resultType.AssemblyQualifiedName));

			var ownerType = fieldInfo.DeclaringType;
			if (ownerType == null)
				throw new NullReferenceException("ownerType");

			var dynamicMethod = CreateDynamicMethod(ownerType, resultType, new[]{typeof(Object)});

			var ilGenerator = dynamicMethod.GetILGenerator();
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Castclass, ownerType);
			ilGenerator.Emit(OpCodes.Ldfld, fieldInfo);
			ilGenerator.Emit(OpCodes.Ret);

			return dynamicMethod;
		}

#endregion

#region Method Access

		[NotNull]
		public Func<TOwner, TResult> GenerateParameterlessMethodCaller<TOwner, TResult>([NotNull] String methodName)
		{
			if (methodName == null)
				throw new ArgumentNullException("methodName");

			var ownerType = typeof(TOwner);
			var resultType = typeof(TResult);

			var methodCaller = GenerateMethodCallerInternal(ownerType, resultType, methodName);
			return owner => (TResult)methodCaller(owner);
		}

		[NotNull]
		public Func<Object, TResult> GenerateParameterlessMethodCaller<TResult>([NotNull] String assemblyName, [NotNull] String typeName, [NotNull] String methodName)
		{
			if (assemblyName == null)
				throw new ArgumentNullException("assemblyName");
			if (typeName == null)
				throw new ArgumentNullException("typeName");
			if (methodName == null)
				throw new ArgumentNullException("methodName");

			var ownerType = GetType(assemblyName, typeName);
			var resultType = typeof(TResult);

			var methodCaller = GenerateMethodCallerInternal(ownerType, resultType, methodName);
			return owner => (TResult)methodCaller(owner);
		}

		[NotNull]
		public Func<TOwner, TParameter, TResult> GenerateOneParameterMethodCaller<TOwner, TParameter, TResult>([NotNull] String methodName)
		{
			if (methodName == null)
				throw new ArgumentNullException("methodName");

			var ownerType = typeof(TOwner);
			var resultType = typeof(TResult);
			var parameterType = typeof(TParameter);

			var methodCaller = GenerateMethodCallerInternal(ownerType, resultType, parameterType, methodName);
			return (owner, parameter) => (TResult)methodCaller(owner, parameter);
		}

		[NotNull]
		public Func<Object, TParameter, TResult> GenerateOneParameterMethodCaller<TParameter, TResult>([NotNull] String assemblyName, [NotNull] String typeName, [NotNull] String methodName)
		{
			if (assemblyName == null)
				throw new ArgumentNullException("assemblyName");
			if (typeName == null)
				throw new ArgumentNullException("typeName");
			if (methodName == null)
				throw new ArgumentNullException("methodName");

			var ownerType = GetType(assemblyName, typeName);
			var resultType = typeof(TResult);
			var parameterType = typeof(TParameter);

			var methodCaller = GenerateMethodCallerInternal(ownerType, resultType, parameterType, methodName);
			return (owner, parameter) => (TResult)methodCaller(owner, parameter);
		}

		[NotNull]
		public Func<object, object, TResult> GenerateOneParameterMethodCaller<TResult>(string assemblyName, string typeName, string methodName, string parameterTypeName)
		{
			//TODO: cleanup and add testing
			//TODO: more specific naming for typeName? for all of these types of calls?
			if (assemblyName == null) throw new ArgumentNullException("assemblyName");
			if (typeName == null) throw new ArgumentNullException("typeName");
			if (methodName == null) throw new ArgumentNullException("methodName");
			if (parameterTypeName == null) throw new ArgumentNullException("parameterTypeName");

			var ownerType = GetType(assemblyName, typeName);
			var resultType = typeof(TResult);
			var parameterType = GetType(assemblyName, parameterTypeName);

			var methodCaller = GenerateMethodCallerInternal(ownerType, resultType, parameterType, methodName);
			return (owner, parameter) => (TResult)methodCaller(owner, parameter);
		}

		[NotNull]
		private static Func<Object, Object> GenerateMethodCallerInternal([NotNull] Type ownerType, [NotNull] Type resultType, [NotNull] String methodName)
		{
			var methodInfo = GetMethodInfo(ownerType, methodName);
			return GenerateMethodCallerInternal(resultType, methodInfo);
		}

		[NotNull]
		private static Func<Object, Object, Object> GenerateMethodCallerInternal([NotNull] Type ownerType, [NotNull] Type resultType, [NotNull] Type parameterType, [NotNull] String methodName)
		{
			var methodInfo = GetMethodInfo(ownerType, methodName);
			return GenerateMethodCallerInternal(resultType, parameterType, methodInfo);
		}

		[NotNull]
		private static Func<Object, Object> GenerateMethodCallerInternal([NotNull] Type resultType, [NotNull] MethodInfo methodInfo)
		{
			if (!resultType.IsAssignableFrom(methodInfo.ReturnType))
				throw new Exception(String.Format("The return type {0} for method {1} does not inherit or implement {2}", methodInfo.ReturnType.AssemblyQualifiedName, methodInfo.Name, resultType.AssemblyQualifiedName));

			var dynamicMethod = GenerateMethodCallerInternal(methodInfo);
			return (Func<Object, Object>)dynamicMethod.CreateDelegate(typeof(Func<Object, Object>));
		}

		[NotNull]
		private static Func<Object, Object, Object> GenerateMethodCallerInternal([NotNull] Type resultType, [NotNull] Type parameterType, [NotNull] MethodInfo methodInfo)
		{
			if (!resultType.IsAssignableFrom(methodInfo.ReturnType))
				throw new Exception(String.Format("The return type {0} for method {1} does not inherit or implement {2}", methodInfo.ReturnType.AssemblyQualifiedName, methodInfo.Name, resultType.AssemblyQualifiedName));

			var parameters = methodInfo.GetParameters();
			if (parameters.Length != 1)
				throw new Exception(String.Format("The number of parameters expected by method {0} ({1}) does not match the number provided (1)", methodInfo.Name, parameters.Length));

			var actualParameterType = parameters[0].ParameterType;
			if (!parameterType.IsAssignableFrom(actualParameterType))
				throw new Exception(String.Format("The parameter type {0} for parameter 1 of method {1} does not inherit or implement {2}", parameterType.AssemblyQualifiedName, methodInfo.Name, actualParameterType.AssemblyQualifiedName));

			var dynamicMethod = GenerateMethodCallerInternal(methodInfo);
			return (Func<Object, Object, Object>)dynamicMethod.CreateDelegate(typeof(Func<Object, Object, Object>));
		}

		[NotNull]
		private static DynamicMethod GenerateMethodCallerInternal([NotNull] MethodInfo methodInfo)
		{
			var ownerType = methodInfo.DeclaringType;
			if (ownerType == null)
				throw new NullReferenceException("ownerType");
			var resultType = methodInfo.ReturnType;
			var returnType = typeof(Object);
			var parameters = methodInfo.GetParameters();
			var parameterTypes = Enumerable.Repeat(typeof(Object), parameters.Length + 1).ToArray();

			var dynamicMethod = CreateDynamicMethod(ownerType, returnType, parameterTypes);

			var ilGenerator = dynamicMethod.GetILGenerator();
			var failureLabel = ilGenerator.DefineLabel();

			// check to make sure the parameters are of the right type
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Isinst, ownerType);
			ilGenerator.Emit(OpCodes.Brfalse_S, failureLabel);
			for (var i = 0; i < parameters.Length; ++i)
			{
				ilGenerator.Emit(OpCodes.Ldarg, (UInt16)i + 1);
				ilGenerator.Emit(OpCodes.Isinst, parameters[i].ParameterType);
				ilGenerator.Emit(OpCodes.Brfalse_S, failureLabel);
			}

			// push 'this' and all of the parameters onto the stack
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Isinst, ownerType);
			for (var i = 0; i < parameters.Length; ++i)
			{
				ilGenerator.Emit(OpCodes.Ldarg, (UInt16)i + 1);
				ilGenerator.Emit(OpCodes.Isinst, parameters[i].ParameterType);
			}

			// call the method, box the results, return the results
			ilGenerator.Emit(OpCodes.Callvirt, methodInfo);
			ilGenerator.Emit(OpCodes.Box, resultType);
			ilGenerator.Emit(OpCodes.Ret);

			// failure case, return null
			ilGenerator.MarkLabel(failureLabel);
			ilGenerator.Emit(OpCodes.Ldnull);
			ilGenerator.Emit(OpCodes.Ret);

			return dynamicMethod;
		}

#endregion

#region Property Access

		[NotNull]
		public Func<TOwner, TResult> GeneratePropertyAccessor<TOwner, TResult>([NotNull] String propertyName)
		{
			if (propertyName == null)
				throw new ArgumentNullException("propertyName");

			var ownerType = typeof(TOwner);
			var resultType = typeof(TResult);

			var propertyGetter = GeneratePropertyAccessorInternal(ownerType, resultType, propertyName);
			return owner => (TResult)propertyGetter(owner);
		}

		[NotNull]
		public Func<Object, TResult> GeneratePropertyAccessor<TResult>([NotNull] String assemblyName, [NotNull] String typeName, [NotNull] String propertyName)
		{
			if (propertyName == null)
				throw new ArgumentNullException("propertyName");
			if (assemblyName == null)
				throw new ArgumentNullException("assemblyName");
			if (typeName == null)
				throw new ArgumentNullException("typeName");

			var ownerType = GetType(assemblyName, typeName);
			var resultType = typeof(TResult);

			var propertyGetter = GeneratePropertyAccessorInternal(ownerType, resultType, propertyName);
			return owner => (TResult)propertyGetter(owner);
		}

		[NotNull]
		private Func<Object, Object> GeneratePropertyAccessorInternal([NotNull] Type ownerType, [NotNull] Type resultType, [NotNull] String propertyName)
		{
			var propertyInfo = GetPropertyInfo(ownerType, propertyName);
			if (propertyInfo == null)
				throw new KeyNotFoundException(String.Format("Could not find property {0} on type {1}", propertyName, ownerType.AssemblyQualifiedName));

			var propertyGetter = GetPropertyGetter(ownerType, propertyName);

			return GenerateMethodCallerInternal(resultType, propertyGetter);
		}

#endregion

#region Constructor Access

		[NotNull]
		public Func<Object> GenerateTypeFactory([NotNull] String assemblyName, [NotNull] String typeName)
		{
			if (assemblyName == null)
				throw new ArgumentNullException("assemblyName");
			if (typeName == null)
				throw new ArgumentNullException("typeName");

			var type = GetType(assemblyName, typeName);
			return GenerateTypeFactory<Func<Object>>(type, new Type[] { });
		}

		[NotNull]
		public Func<TParam, Object> GenerateTypeFactory<TParam>([NotNull] String assemblyName, [NotNull] String typeName)
		{
			if (assemblyName == null)
				throw new ArgumentNullException("assemblyName");
			if (typeName == null)
				throw new ArgumentNullException("typeName");

			var type = GetType(assemblyName, typeName);
			return GenerateTypeFactory<Func<TParam, Object>>(type, new[] { typeof(TParam) });
		}

		[NotNull]
		public Func<TParam1, TParam2, Object> GenerateTypeFactory<TParam1, TParam2>([NotNull] String assemblyName, [NotNull] String typeName)
		{
			if (assemblyName == null)
				throw new ArgumentNullException("assemblyName");
			if (typeName == null)
				throw new ArgumentNullException("typeName");

			var type = GetType(assemblyName, typeName);
			return GenerateTypeFactory<Func<TParam1, TParam2, Object>>(type, new[] { typeof(TParam1), typeof(TParam2) });
		}

		[NotNull]
		public Func<TResult> GenerateTypeFactory<TResult>()
		{
			var type = typeof(TResult);
			var constructor = GenerateTypeFactory<Func<Object>>(type, new Type[] { });
			return () => (TResult)constructor();
		}

		[NotNull]
		public Func<TParam, TResult> GenerateTypeFactory<TParam, TResult>()
		{
			var type = typeof(TResult);
			var constructor = GenerateTypeFactory<Func<TParam, Object>>(type, new[] { typeof(TParam) });
			return param => (TResult)constructor(param);
		}

		[NotNull]
		public Func<TParam1, TParam2, TResult> GenerateTypeFactory<TParam1, TParam2, TResult>()
		{
			var type = typeof(TResult);
			var constructor = GenerateTypeFactory<Func<TParam1, TParam2, Object>>(type, new[] { typeof(TParam1), typeof(TParam2) });
			return (param1, param2) => (TResult)constructor(param1, param2);
		}

		[NotNull]
		private T GenerateTypeFactory<T>([NotNull]Type type, params Type[] parameterTypes) where T : class
		{
			var constructor = reflection.GetConstructor(type, parameterTypes);

			if (constructor == null)
			{
				var parameterNames = parameterTypes
						.Where(parameterType => parameterType != null)
						.Select(parameterType => parameterType.FullName)
						.ToArray();
				var parameters = String.Join(", ", parameterNames);
				var message = String.Format("Unable to find constructor taking parameter types {0} in type {1}", parameters, type.AssemblyQualifiedName);
				throw new Exception(message);
			}

			var dynamicMethod = new DynamicMethod(Guid.NewGuid().ToString(), typeof (Object), parameterTypes, type);
			var ilGenerator = dynamicMethod.GetILGenerator();
			for (var i = 0; i < parameterTypes.Length; ++i)
			{
				ilGenerator.Emit(OpCodes.Ldarg, i);
			}
			ilGenerator.Emit(OpCodes.Newobj, constructor);
			ilGenerator.Emit(OpCodes.Ret);

			var result = dynamicMethod.CreateDelegate(typeof(T)) as T;
			if (result == null)
				throw new Exception(String.Format("Failed to create a delegate for the constructor of type {0} that matches the desired delegate type {1}", type.AssemblyQualifiedName, typeof(T).AssemblyQualifiedName));

			return result;
		}

#endregion

#region Helpers

		[NotNull]
		private Type GetType([NotNull] String assemblyName, [NotNull] String typeName)
		{
			var assembly = Assembly.Load(assemblyName);
			if (assembly == null)
				throw new NullReferenceException("assembly");
			var type = assembly.GetType(typeName, true);
			if (type == null)
				throw new NullReferenceException("type");
			return type;
		}

		[NotNull]
		private static FieldInfo GetFieldInfo([NotNull] Type type, [NotNull] String fieldName)
		{
			var fieldInfo = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
			if (fieldInfo == null)
				throw new KeyNotFoundException(String.Format("Unable to find field {0} in type {1}", fieldName, type.AssemblyQualifiedName));

			return fieldInfo;
		}

		[NotNull]
		private static MethodInfo GetMethodInfo([NotNull] Type type, [NotNull] String methodName)
		{
			var methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			if (methodInfo == null)
				throw new KeyNotFoundException(String.Format("Unable to find method {0} in type {1}", methodName, type.AssemblyQualifiedName));

			return methodInfo;
		}

		[NotNull]
		private static PropertyInfo GetPropertyInfo([NotNull] Type type, [NotNull] String propertyName)
		{
			var propertyInfo = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
			if (propertyInfo == null)
				throw new KeyNotFoundException(String.Format("Unable to find property {0} in type {1}", propertyName, type.AssemblyQualifiedName));

			return propertyInfo;
		}

		[NotNull]
		private static MethodInfo GetPropertyGetter([NotNull] Type type, [NotNull] String propertyName)
		{
			var propertyInfo = GetPropertyInfo(type, propertyName);
			var methodInfo = propertyInfo.GetGetMethod(true);
			if (methodInfo == null)
				throw new Exception(String.Format("Property {0} on type {1} does not have a getter", propertyInfo.Name, type.AssemblyQualifiedName));

			return methodInfo;
		}

		[NotNull]
		private static DynamicMethod CreateDynamicMethod([NotNull] Type ownerType, [NotNull] Type resultType, params Type[] parameterTypes)
		{
			return new DynamicMethod(Guid.NewGuid().ToString(), resultType, parameterTypes, ownerType);
		}

#endregion


	}
}
