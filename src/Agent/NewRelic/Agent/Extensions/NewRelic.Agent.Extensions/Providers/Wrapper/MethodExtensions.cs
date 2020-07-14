using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
	public static class MethodExtensions
	{
		public static Boolean MatchesAny(this Method actualMethod, String assemblyName, String typeName, [NotNull] IEnumerable<String> methodNames, [CanBeNull] IEnumerable<String> parameterSignatures = null)
		{
			var assemblyNameActual = actualMethod.Type.Assembly.GetName().Name;
			var typeNameActual = actualMethod.Type.FullName;

			return assemblyNameActual == assemblyName && typeNameActual == typeName &&
				methodNames.Any(expectedMethodName => expectedMethodName == actualMethod.MethodName) &&
				(parameterSignatures == null ||
				parameterSignatures.Any(expectedSignaturesName => expectedSignaturesName == actualMethod.ParameterTypeNames));
		}

		public static Boolean MatchesAny(this Method actualMethod, String assemblyName, String typeName, [NotNull] IEnumerable<MethodSignature> methodSignatures)
		{
			var assemblyNameActual = actualMethod.Type.Assembly.GetName().Name;
			var typeNameActual = actualMethod.Type.FullName;

			if (assemblyNameActual != assemblyName || typeNameActual != typeName)
				return false;

			return methodSignatures.Any(methodSignature => (methodSignature.MethodName == actualMethod.MethodName) && (methodSignature.ParameterSignature == null || methodSignature.ParameterSignature == actualMethod.ParameterTypeNames));
		}

		public static Boolean MatchesAny(this Method actualMethod, IEnumerable<String> assemblyNames, IEnumerable<String> typeNames, IEnumerable<String> methodNames, [CanBeNull] IEnumerable<String> parameterSignatures = null)
		{
			var assemblyName = actualMethod.Type.Assembly.GetName().Name;
			var typeName = actualMethod.Type.FullName;

			return assemblyNames.Any(expectedAssemblyName => expectedAssemblyName == assemblyName) &&
				typeNames.Any(expectedTypeName => expectedTypeName == typeName) &&
				methodNames.Any(expectedMethodName => expectedMethodName == actualMethod.MethodName) &&
				(parameterSignatures == null ||
				parameterSignatures.Any(expectedSignaturesName => expectedSignaturesName == actualMethod.ParameterTypeNames));
		}

		public static Boolean MatchesAny(this Method actualMethod, String assemblyName, String typeName, String methodName, IEnumerable<String> parameterSignatures)
		{
			return MatchesAny(actualMethod, new[] { assemblyName }, new[] { typeName }, new[] { methodName }, parameterSignatures);
		}

		public static Boolean MatchesAny(this Method actualMethod, String assemblyName, IEnumerable<String> typeNames, String methodName)
		{
			return MatchesAny(actualMethod, new[] { assemblyName }, typeNames, new[] { methodName });
		}

		public static Boolean MatchesAny(this Method actualMethod, String assemblyName, String typeName, String methodName)
		{
			return MatchesAny(actualMethod, new[] { assemblyName }, new[] { typeName }, new[] { methodName });
		}

		public static Boolean MatchesAny(this Method actualMethod, String assemblyName, String typeName, String methodName, String parameterSignature)
		{
			return MatchesAny(actualMethod, new[] { assemblyName }, new[] { typeName }, new[] { methodName }, new[] { parameterSignature });
		}

		public static Boolean MatchesAny(this Method actualMethod, IEnumerable<String> assemblyNames, IEnumerable<String> typeNames, IEnumerable<String> methodNames, String parameterSignature)
		{
			return MatchesAny(actualMethod, assemblyNames, typeNames, methodNames, new[] { parameterSignature });
		}

		public static Boolean MatchesAny(this Method actualMethod, IEnumerable<String> assemblyNames, IEnumerable<String> typeNames, String methodName, String parameterSignature)
		{
			return MatchesAny(actualMethod, assemblyNames, typeNames, new[] { methodName }, new[] { parameterSignature });
		}

		public static Boolean MatchesAny(this Method actualMethod, IEnumerable<String> assemblyNames, String typeName, String methodName, String parameterSignature)
		{
			return MatchesAny(actualMethod, assemblyNames, new[] { typeName }, new[] { methodName }, new[] { parameterSignature });
		}
	}
}
