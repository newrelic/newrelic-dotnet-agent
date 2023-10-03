// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace NewRelic.Reflection
{
    public class VisibilityBypasser
    {
        public static readonly VisibilityBypasser Instance = new VisibilityBypasser();

        private VisibilityBypasser() { }

        #region Field Write Access

        public Action<object, TField> GenerateFieldWriteAccessor<TField>(string assemblyName, string typeName, string fieldName)
        {
            if (assemblyName == null)
            {
                throw new ArgumentNullException(nameof(assemblyName));
            }
            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }
            if (fieldName == null)
            {
                throw new ArgumentNullException(nameof(fieldName));
            }

            var ownerType = GetType(assemblyName, typeName);
            return GenerateFieldWriteAccessor<TField>(ownerType, fieldName);
        }

        public Action<object, TField> GenerateFieldWriteAccessor<TField>(Type ownerType, string fieldName)
        {
            if (ownerType == null)
            {
                throw new ArgumentNullException(nameof(ownerType));
            }

            if (fieldName == null)
            {
                throw new ArgumentNullException(nameof(fieldName));
            }

            var dynamicMethod = GenerateFieldWriteAccessorInternal<TField>(ownerType, fieldName);
            return (Action<object, TField>)dynamicMethod.CreateDelegate(typeof(Action<object, TField>));
        }

        private static DynamicMethod GenerateFieldWriteAccessorInternal<TField>(Type ownerType, string fieldName)
        {
            var fieldInfo = GetFieldInfo(ownerType, fieldName);
            return GenerateFieldWriteAccessorInternal<TField>(fieldInfo);
        }

        private static DynamicMethod GenerateFieldWriteAccessorInternal<TField>(FieldInfo fieldInfo)
        {
            var ownerType = fieldInfo.DeclaringType;
            if (ownerType == null)
            {
                throw new NullReferenceException(nameof(ownerType));
            }

            var dynamicMethod = CreateDynamicMethod(ownerType, null, new[] { typeof(object), typeof(TField) });

            var ilGenerator = dynamicMethod.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldarg_1);
            ilGenerator.Emit(OpCodes.Stfld, fieldInfo);
            ilGenerator.Emit(OpCodes.Ret);

            return dynamicMethod;
        }

        #endregion

        #region Field Read Access

        public Func<object, TResult> GenerateFieldReadAccessor<TResult>(string assemblyName, string typeName, string fieldName)
        {
            if (assemblyName == null)
            {
                throw new ArgumentNullException(nameof(assemblyName));
            }

            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (fieldName == null)
            {
                throw new ArgumentNullException(nameof(fieldName));
            }

            var ownerType = GetType(assemblyName, typeName);
            return GenerateFieldReadAccessor<TResult>(ownerType, fieldName);
        }

        public Func<object, TResult> GenerateFieldReadAccessor<TResult>(Type ownerType, string fieldName)
        {
            if (ownerType == null)
            {
                throw new ArgumentNullException(nameof(ownerType));
            }

            if (fieldName == null)
            {
                throw new ArgumentNullException(nameof(fieldName));
            }

            var dynamicMethod = GenerateFieldReadAccessorInternal<TResult>(ownerType, fieldName);
            return (Func<object, TResult>)dynamicMethod.CreateDelegate(typeof(Func<object, TResult>));
        }

        public Func<TOwner, TResult> GenerateFieldReadAccessor<TOwner, TResult>(string fieldName)
        {
            if (fieldName == null)
            {
                throw new ArgumentNullException(nameof(fieldName));
            }

            var dynamicMethod = GenerateFieldReadAccessorInternal<TResult>(typeof(TOwner), fieldName);
            return (Func<TOwner, TResult>)dynamicMethod.CreateDelegate(typeof(Func<TOwner, TResult>));
        }

        private static DynamicMethod GenerateFieldReadAccessorInternal<TResult>(Type ownerType, string fieldName)
        {
            var fieldInfo = GetFieldInfo(ownerType, fieldName);
            return GenerateFieldReadAccessorInternal<TResult>(fieldInfo);
        }

        private static DynamicMethod GenerateFieldReadAccessorInternal<TResult>(FieldInfo fieldInfo)
        {
            var resultType = typeof(TResult);
            if (!resultType.IsAssignableFrom(fieldInfo.FieldType))
            {
                throw new Exception(string.Format("The return type for field {0} does not inherit or implement {1}", fieldInfo.Name, resultType.AssemblyQualifiedName));
            }

            var ownerType = fieldInfo.DeclaringType;
            if (ownerType == null)
            {
                throw new NullReferenceException(nameof(ownerType));
            }

            var dynamicMethod = CreateDynamicMethod(ownerType, resultType, new[] { typeof(object) });

            var ilGenerator = dynamicMethod.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Castclass, ownerType);
            ilGenerator.Emit(OpCodes.Ldfld, fieldInfo);
            ilGenerator.Emit(OpCodes.Ret);

            return dynamicMethod;
        }

        #endregion

        #region Method Access

        public Func<TOwner, TResult> GenerateParameterlessMethodCaller<TOwner, TResult>(string methodName)
        {
            if (methodName == null)
            {
                throw new ArgumentNullException(nameof(methodName));
            }

            var ownerType = typeof(TOwner);
            var resultType = typeof(TResult);

            var methodCaller = GenerateMethodCallerInternal(ownerType, resultType, methodName);
            return owner => (TResult)methodCaller(owner);
        }

        public Func<object, TResult> GenerateParameterlessMethodCaller<TResult>(string assemblyName, string typeName, string methodName)
        {
            if (assemblyName == null)
            {
                throw new ArgumentNullException(nameof(assemblyName));
            }

            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (methodName == null)
            {
                throw new ArgumentNullException(nameof(methodName));
            }

            var ownerType = GetType(assemblyName, typeName);
            var resultType = typeof(TResult);

            var methodCaller = GenerateMethodCallerInternal(ownerType, resultType, methodName);
            return owner => (TResult)methodCaller(owner);
        }

        public bool TryGenerateParameterlessMethodCaller<TResult>(string assemblyName, string typeName, string methodName, out Func<object, TResult> accessor)
        {
            accessor = null;
            try
            {
                var methodCaller = GenerateParameterlessMethodCaller<TResult>(assemblyName, typeName, methodName);
                accessor = owner => (TResult)methodCaller(owner);
                return true;
            }
            catch (ArgumentNullException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        public Func<TOwner, TParameter, TResult> GenerateOneParameterMethodCaller<TOwner, TParameter, TResult>(string methodName)
        {
            if (methodName == null)
            {
                throw new ArgumentNullException(nameof(methodName));
            }

            var ownerType = typeof(TOwner);
            var resultType = typeof(TResult);
            var parameterType = typeof(TParameter);

            var methodCaller = GenerateMethodCallerInternal(ownerType, resultType, parameterType, methodName);
            return (owner, parameter) => (TResult)methodCaller(owner, parameter);
        }

        public Func<object, TParameter, TResult> GenerateOneParameterMethodCaller<TParameter, TResult>(string assemblyName, string typeName, string methodName)
        {
            if (assemblyName == null)
            {
                throw new ArgumentNullException(nameof(assemblyName));
            }

            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (methodName == null)
            {
                throw new ArgumentNullException(nameof(methodName));
            }

            var ownerType = GetType(assemblyName, typeName);
            var resultType = typeof(TResult);
            var parameterType = typeof(TParameter);

            var methodCaller = GenerateMethodCallerInternal(ownerType, resultType, parameterType, methodName);
            return (owner, parameter) => (TResult)methodCaller(owner, parameter);
        }

        public Func<object, object, TResult> GenerateOneParameterMethodCaller<TResult>(string assemblyName, string typeName, string methodName, string parameterTypeName)
        {
            if (assemblyName == null)
            {
                throw new ArgumentNullException(nameof(assemblyName));
            }

            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (methodName == null)
            {
                throw new ArgumentNullException(nameof(methodName));
            }

            if (parameterTypeName == null)
            {
                throw new ArgumentNullException(nameof(parameterTypeName));
            }

            var ownerType = GetType(assemblyName, typeName);
            var resultType = typeof(TResult);
            var parameterType = GetType(assemblyName, parameterTypeName);

            var methodCaller = GenerateMethodCallerInternal(ownerType, resultType, parameterType, methodName);
            return (owner, parameter) => (TResult)methodCaller(owner, parameter);
        }

        private static Func<object, object> GenerateMethodCallerInternal(Type ownerType, Type resultType, string methodName)
        {
            var methodInfo = GetMethodInfo(ownerType, methodName);
            return GenerateMethodCallerInternal(resultType, methodInfo);
        }

        private static Func<object, object, object> GenerateMethodCallerInternal(Type ownerType, Type resultType, Type parameterType, string methodName)
        {
            var methodInfo = GetMethodInfo(ownerType, methodName);
            return GenerateMethodCallerInternal(resultType, parameterType, methodInfo);
        }

        private static Func<object, object> GenerateMethodCallerInternal(Type resultType, MethodInfo methodInfo)
        {
            if (!resultType.IsAssignableFrom(methodInfo.ReturnType))
            {
                throw new Exception(string.Format("The return type {0} for method {1} does not inherit or implement {2}", methodInfo.ReturnType.AssemblyQualifiedName, methodInfo.Name, resultType.AssemblyQualifiedName));
            }

            var dynamicMethod = GenerateMethodCallerInternal(methodInfo);
            return (Func<object, object>)dynamicMethod.CreateDelegate(typeof(Func<object, object>));
        }

        private static Func<object, object, object> GenerateMethodCallerInternal(Type resultType, Type parameterType, MethodInfo methodInfo)
        {
            if (!resultType.IsAssignableFrom(methodInfo.ReturnType))
            {
                throw new Exception(string.Format("The return type {0} for method {1} does not inherit or implement {2}", methodInfo.ReturnType.AssemblyQualifiedName, methodInfo.Name, resultType.AssemblyQualifiedName));
            }

            var parameters = methodInfo.GetParameters();
            if (parameters.Length != 1)
            {
                throw new Exception(string.Format("The number of parameters expected by method {0} ({1}) does not match the number provided (1)", methodInfo.Name, parameters.Length));
            }

            var actualParameterType = parameters[0].ParameterType;
            if (!parameterType.IsAssignableFrom(actualParameterType))
            {
                throw new Exception(string.Format("The parameter type {0} for parameter 1 of method {1} does not inherit or implement {2}", parameterType.AssemblyQualifiedName, methodInfo.Name, actualParameterType.AssemblyQualifiedName));
            }

            var dynamicMethod = GenerateMethodCallerInternal(methodInfo);
            return (Func<object, object, object>)dynamicMethod.CreateDelegate(typeof(Func<object, object, object>));
        }

        private static DynamicMethod GenerateMethodCallerInternal(MethodInfo methodInfo)
        {
            var ownerType = methodInfo.DeclaringType;
            if (ownerType == null)
            {
                throw new NullReferenceException(nameof(ownerType));
            }

            var resultType = methodInfo.ReturnType;
            var returnType = typeof(object);
            var parameters = methodInfo.GetParameters();
            var parameterTypes = Enumerable.Repeat(typeof(object), parameters.Length + 1).ToArray();

            var dynamicMethod = CreateDynamicMethod(ownerType, returnType, parameterTypes);

            var ilGenerator = dynamicMethod.GetILGenerator();
            var failureLabel = ilGenerator.DefineLabel();

            // check to make sure the parameters are of the right type
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Isinst, ownerType);
            ilGenerator.Emit(OpCodes.Brfalse_S, failureLabel);
            for (var i = 0; i < parameters.Length; ++i)
            {
                ilGenerator.Emit(OpCodes.Ldarg, (ushort)i + 1);
                ilGenerator.Emit(OpCodes.Isinst, parameters[i].ParameterType);
                ilGenerator.Emit(OpCodes.Brfalse_S, failureLabel);
            }

            // push 'this' and all of the parameters onto the stack
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Isinst, ownerType);
            for (var i = 0; i < parameters.Length; ++i)
            {
                ilGenerator.Emit(OpCodes.Ldarg, (ushort)i + 1);
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

        public Func<object, TResult> GeneratePropertyAccessor<TResult>(Type ownerType, string propertyName)
        {
            if (ownerType == null)
            {
                throw new ArgumentNullException(nameof(ownerType));
            }

            if (propertyName == null)
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            var resultType = typeof(TResult);

            var propertyGetter = GeneratePropertyAccessorInternal(ownerType, resultType, propertyName);
            return owner => (TResult)propertyGetter(owner);
        }

        public bool TryGeneratePropertyAccessor<TResult>(Type ownerType, string propertyName, out Func<object, TResult> accessor)
        {
            accessor = null;
            try
            {
                var propertyGetter = GeneratePropertyAccessor<TResult>(ownerType, propertyName);
                accessor = owner => (TResult)propertyGetter(owner);
                return true;
            }
            catch (ArgumentNullException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        public Func<TOwner, TResult> GeneratePropertyAccessor<TOwner, TResult>(string propertyName)
        {
            if (propertyName == null)
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            var ownerType = typeof(TOwner);
            var resultType = typeof(TResult);

            var propertyGetter = GeneratePropertyAccessorInternal(ownerType, resultType, propertyName);
            return owner => (TResult)propertyGetter(owner);
        }

        public Func<object, TResult> GeneratePropertyAccessor<TResult>(string assemblyName, string typeName, string propertyName)
        {
            if (propertyName == null)
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            if (assemblyName == null)
            {
                throw new ArgumentNullException(nameof(assemblyName));
            }

            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            var ownerType = GetType(assemblyName, typeName);
            var resultType = typeof(TResult);

            var propertyGetter = GeneratePropertyAccessorInternal(ownerType, resultType, propertyName);
            return owner => (TResult)propertyGetter(owner);
        }

        private Func<object, object> GeneratePropertyAccessorInternal(Type ownerType, Type resultType, string propertyName)
        {
            var propertyInfo = GetPropertyInfo(ownerType, propertyName);
            if (propertyInfo == null)
            {
                throw new KeyNotFoundException(string.Format("Could not find property {0} on type {1}", propertyName, ownerType.AssemblyQualifiedName));
            }

            var propertyGetter = GetPropertyGetter(ownerType, propertyName);
            return GenerateMethodCallerInternal(resultType, propertyGetter);
        }

        // Do not cache the delegate returned by this method; it is only valid for the specific "owner" object instance
        public Action<TValue> GeneratePropertySetter<TValue>(object owner, string propertyName)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (propertyName == null)
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            var setterMethodInfo = GetPropertySetter(owner.GetType(), propertyName);
            return (Action<TValue>)setterMethodInfo.CreateDelegate(typeof(Action<TValue>), owner);
        }

        #endregion

        #region Constructor Access

        public Func<object> GenerateTypeFactory(string assemblyName, string typeName)
        {
            if (assemblyName == null)
            {
                throw new ArgumentNullException(nameof(assemblyName));
            }

            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            var type = GetType(assemblyName, typeName);
            return GenerateTypeFactory<Func<object>>(type, new Type[] { });
        }

        public Func<TParam, object> GenerateTypeFactory<TParam>(string assemblyName, string typeName)
        {
            if (assemblyName == null)
            {
                throw new ArgumentNullException(nameof(assemblyName));
            }

            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            var type = GetType(assemblyName, typeName);
            return GenerateTypeFactory<Func<TParam, object>>(type, new[] { typeof(TParam) });
        }

        public Func<TParam1, TParam2, object> GenerateTypeFactory<TParam1, TParam2>(string assemblyName, string typeName)
        {
            if (assemblyName == null)
            {
                throw new ArgumentNullException(nameof(assemblyName));
            }

            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            var type = GetType(assemblyName, typeName);
            return GenerateTypeFactory<Func<TParam1, TParam2, object>>(type, new[] { typeof(TParam1), typeof(TParam2) });
        }

        public Func<TResult> GenerateTypeFactory<TResult>()
        {
            var type = typeof(TResult);
            var constructor = GenerateTypeFactory<Func<object>>(type, new Type[] { });
            return () => (TResult)constructor();
        }

        public Func<TParam, TResult> GenerateTypeFactory<TParam, TResult>()
        {
            var type = typeof(TResult);
            var constructor = GenerateTypeFactory<Func<TParam, object>>(type, new[] { typeof(TParam) });
            return param => (TResult)constructor(param);
        }

        public Func<TParam1, TParam2, TResult> GenerateTypeFactory<TParam1, TParam2, TResult>()
        {
            var type = typeof(TResult);
            var constructor = GenerateTypeFactory<Func<TParam1, TParam2, object>>(type, new[] { typeof(TParam1), typeof(TParam2) });
            return (param1, param2) => (TResult)constructor(param1, param2);
        }

        private T GenerateTypeFactory<T>(Type type, params Type[] parameterTypes) where T : class
        {
            var constructor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, parameterTypes, null);
            if (constructor == null)
            {
                var parameterNames = parameterTypes
                        .Where(parameterType => parameterType != null)
                        .Select(parameterType => parameterType.FullName)
                        .ToArray();
                var parameters = string.Join(", ", parameterNames);
                var message = string.Format("Unable to find constructor taking parameter types {0} in type {1}", parameters, type.AssemblyQualifiedName);
                throw new Exception(message);
            }

            var dynamicMethod = new DynamicMethod(Guid.NewGuid().ToString(), typeof(object), parameterTypes, type);
            var ilGenerator = dynamicMethod.GetILGenerator();
            for (var i = 0; i < parameterTypes.Length; ++i)
            {
                ilGenerator.Emit(OpCodes.Ldarg, i);
            }

            ilGenerator.Emit(OpCodes.Newobj, constructor);
            ilGenerator.Emit(OpCodes.Ret);

            var result = dynamicMethod.CreateDelegate(typeof(T)) as T;
            if (result == null)
            {
                throw new Exception(string.Format("Failed to create a delegate for the constructor of type {0} that matches the desired delegate type {1}", type.AssemblyQualifiedName, typeof(T).AssemblyQualifiedName));
            }

            return result;
        }

        #endregion

        #region Helpers

        private Type GetType(string assemblyName, string typeName)
        {
            var assembly = Assembly.Load(assemblyName);
            if (assembly == null)
            {
                throw new NullReferenceException(nameof(assembly));
            }

            var type = assembly.GetType(typeName, true);
            if (type == null)
            {
                throw new NullReferenceException(nameof(type));
            }

            return type;
        }

        private static FieldInfo GetFieldInfo(Type type, string fieldName)
        {
            var fieldInfo = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (fieldInfo == null)
            {
                throw new KeyNotFoundException(string.Format("Unable to find field {0} in type {1}", fieldName, type.AssemblyQualifiedName));
            }

            return fieldInfo;
        }

        private static MethodInfo GetMethodInfo(Type type, string methodName)
        {
            var methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (methodInfo == null)
            {
                throw new KeyNotFoundException(string.Format("Unable to find method {0} in type {1}", methodName, type.AssemblyQualifiedName));
            }

            return methodInfo;
        }

        public Func<TResult> GenerateParameterlessStaticMethodCaller<TResult>(string assemblyName, string typeName, string methodName)
        {
            var ownerType = GetType(assemblyName, typeName);

            var methodInfo = ownerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (methodInfo == null)
            {
                throw new KeyNotFoundException(string.Format("Unable to find method {0} in type {1}", methodName, ownerType.AssemblyQualifiedName));
            }
            return (Func<TResult>)methodInfo.CreateDelegate(typeof(Func<TResult>));
        }

        private static PropertyInfo GetPropertyInfo(Type type, string propertyName)
        {
            var propertyInfo = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (propertyInfo == null)
            {
                throw new KeyNotFoundException(string.Format("Unable to find property {0} in type {1}", propertyName, type.AssemblyQualifiedName));
            }

            return propertyInfo;
        }

        private static MethodInfo GetPropertyGetter(Type type, string propertyName)
        {
            var propertyInfo = GetPropertyInfo(type, propertyName);
            var methodInfo = propertyInfo.GetGetMethod(true);
            if (methodInfo == null)
            {
                throw new Exception(string.Format("Property {0} on type {1} does not have a getter", propertyInfo.Name, type.AssemblyQualifiedName));
            }

            return methodInfo;
        }

        private static MethodInfo GetPropertySetter(Type type, string propertyName)
        {
            var propertyInfo = GetPropertyInfo(type, propertyName);
            var methodInfo = propertyInfo.GetSetMethod(true);
            if (methodInfo == null)
            {
                throw new Exception(string.Format("Property {0} on type {1} does not have a setter", propertyInfo.Name, type.AssemblyQualifiedName));
            }

            return methodInfo;
        }

        private static DynamicMethod CreateDynamicMethod(Type ownerType, Type resultType, params Type[] parameterTypes)
        {
            return new DynamicMethod(Guid.NewGuid().ToString(), resultType, parameterTypes, ownerType, skipVisibility: true);
        }

        #endregion
    }
}
