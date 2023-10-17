// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data
{
    public class MethodCallData
    {
        public readonly string TypeName;
        public readonly string MethodName;
        public readonly int InvocationTargetHashCode;
        public readonly bool IsAsync;

        public MethodCallData(string typeName, string methodName, int invocationTargetHashCode, bool isAsync = false)
        {
            TypeName = typeName;
            MethodName = methodName;
            InvocationTargetHashCode = invocationTargetHashCode;
            IsAsync = isAsync;
        }

        public override string ToString()
        {
            return TypeName + '.' + MethodName;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + TypeName.GetHashCode();
            hash = hash * 23 + MethodName.GetHashCode();
            hash = hash * 23 + InvocationTargetHashCode;
            hash = hash * 29 + IsAsync.GetHashCode();
            return hash;
        }

        public override bool Equals(object obj)
        {
            if (obj is MethodCallData)
            {
                var other = (MethodCallData)obj;
                return InvocationTargetHashCode == other.InvocationTargetHashCode &&
                    MethodName.Equals(other.MethodName) &&
                    TypeName.Equals(other.TypeName) &&
                    IsAsync == other.IsAsync;
            }
            return false;
        }
    }
}
