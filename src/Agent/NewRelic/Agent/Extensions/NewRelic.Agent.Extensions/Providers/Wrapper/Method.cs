// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public class Method
    {
        public readonly Type Type;
        public readonly string MethodName;
        public readonly string ParameterTypeNames;
        private readonly int _hashCode;

        public Method(Type type, string methodName, string parameterTypeNames, int hashCode)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            if (methodName == null)
                throw new ArgumentNullException("methodName");

            if (parameterTypeNames == null)
                throw new ArgumentNullException("parameterTypeNames");

            Type = type;
            MethodName = methodName;
            ParameterTypeNames = parameterTypeNames;
            _hashCode = hashCode;
        }

        public Method(Type type, string methodName, string parameterTypeNames) :
            this(type, methodName, parameterTypeNames, GetHashCode(type, methodName, parameterTypeNames))
        {
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        private static int GetHashCode(Type type, string methodName, string parameterTypeNames)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + type.GetHashCode();
                hash = hash * 23 + methodName.GetHashCode();
                hash = hash * 23 + parameterTypeNames.GetHashCode();
                return hash;
            }
        }

        public override bool Equals(object other)
        {
            if (!(other is Method))
                return false;

            var otherMethod = (Method)other;

            if (otherMethod.Type != Type)
                return false;

            if (otherMethod.MethodName != MethodName)
                return false;

            if (otherMethod.ParameterTypeNames != ParameterTypeNames)
                return false;

            return true;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}({2})", Type.AssemblyQualifiedName, MethodName, ParameterTypeNames);
        }
    }
}
