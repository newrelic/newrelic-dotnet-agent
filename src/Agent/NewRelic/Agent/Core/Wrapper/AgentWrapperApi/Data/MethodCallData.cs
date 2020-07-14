using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data
{
	public class MethodCallData
	{
		[NotNull]
		public readonly String TypeName;

		[NotNull]
		public readonly String MethodName;

		public readonly Int32 InvocationTargetHashCode;

		public MethodCallData([NotNull] String typeName, [NotNull] String methodName, Int32 invocationTargetHashCode)
		{
			TypeName = typeName;
			MethodName = methodName;
			InvocationTargetHashCode = invocationTargetHashCode;
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
			return hash;
		}

		public override bool Equals(object obj)
		{
			if (obj is MethodCallData)
			{
				var other = (MethodCallData)obj;
				return InvocationTargetHashCode == other.InvocationTargetHashCode &&
					MethodName.Equals(other.MethodName) &&
					TypeName.Equals(other.TypeName);
			}
			return false;
		}
	}
}
