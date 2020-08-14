// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public static class MethodExtensions
    {
        public static bool MatchesAny(this Method actualMethod, string assemblyName, string typeName, IEnumerable<string> methodNames, IEnumerable<string> parameterSignatures = null)
        {
            var assemblyNameActual = actualMethod.Type.Assembly.GetName().Name;
            var typeNameActual = actualMethod.Type.FullName;

            return assemblyNameActual == assemblyName && typeNameActual == typeName &&
                methodNames.Any(expectedMethodName => expectedMethodName == actualMethod.MethodName) &&
                (parameterSignatures == null ||
                parameterSignatures.Any(expectedSignaturesName => expectedSignaturesName == actualMethod.ParameterTypeNames));
        }

        public static bool MatchesAny(this Method actualMethod, string assemblyName, string typeName, IEnumerable<MethodSignature> methodSignatures)
        {
            var assemblyNameActual = actualMethod.Type.Assembly.GetName().Name;
            var typeNameActual = actualMethod.Type.FullName;

            if (assemblyNameActual != assemblyName || typeNameActual != typeName)
                return false;

            return methodSignatures.Any(methodSignature => (methodSignature.MethodName == actualMethod.MethodName) && (methodSignature.ParameterSignature == null || methodSignature.ParameterSignature == actualMethod.ParameterTypeNames));
        }

        public static bool MatchesAny(this Method actualMethod, IEnumerable<string> assemblyNames, IEnumerable<string> typeNames, IEnumerable<string> methodNames, IEnumerable<string> parameterSignatures = null)
        {
            var assemblyName = actualMethod.Type.Assembly.GetName().Name;
            var typeName = actualMethod.Type.FullName;

            return assemblyNames.Any(expectedAssemblyName => expectedAssemblyName == assemblyName) &&
                typeNames.Any(expectedTypeName => expectedTypeName == typeName) &&
                methodNames.Any(expectedMethodName => expectedMethodName == actualMethod.MethodName) &&
                (parameterSignatures == null ||
                parameterSignatures.Any(expectedSignaturesName => expectedSignaturesName == actualMethod.ParameterTypeNames));
        }

        public static bool MatchesAny(this Method actualMethod, string assemblyName, string typeName, string methodName, IEnumerable<string> parameterSignatures)
        {
            return MatchesAny(actualMethod, new[] { assemblyName }, new[] { typeName }, new[] { methodName }, parameterSignatures);
        }

        public static bool MatchesAny(this Method actualMethod, string assemblyName, IEnumerable<string> typeNames, string methodName)
        {
            return MatchesAny(actualMethod, new[] { assemblyName }, typeNames, new[] { methodName });
        }

        public static bool MatchesAny(this Method actualMethod, string assemblyName, string typeName, string methodName)
        {
            return MatchesAny(actualMethod, new[] { assemblyName }, new[] { typeName }, new[] { methodName });
        }

        public static bool MatchesAny(this Method actualMethod, string assemblyName, string typeName, string methodName, string parameterSignature)
        {
            return MatchesAny(actualMethod, new[] { assemblyName }, new[] { typeName }, new[] { methodName }, new[] { parameterSignature });
        }

        public static bool MatchesAny(this Method actualMethod, IEnumerable<string> assemblyNames, IEnumerable<string> typeNames, IEnumerable<string> methodNames, string parameterSignature)
        {
            return MatchesAny(actualMethod, assemblyNames, typeNames, methodNames, new[] { parameterSignature });
        }

        public static bool MatchesAny(this Method actualMethod, IEnumerable<string> assemblyNames, IEnumerable<string> typeNames, string methodName, string parameterSignature)
        {
            return MatchesAny(actualMethod, assemblyNames, typeNames, new[] { methodName }, new[] { parameterSignature });
        }

        public static bool MatchesAny(this Method actualMethod, IEnumerable<string> assemblyNames, string typeName, string methodName, string parameterSignature)
        {
            return MatchesAny(actualMethod, assemblyNames, new[] { typeName }, new[] { methodName }, new[] { parameterSignature });
        }
    }
}
