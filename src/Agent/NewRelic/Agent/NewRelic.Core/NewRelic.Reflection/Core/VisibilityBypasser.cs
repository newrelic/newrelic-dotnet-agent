#if NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using JetBrains.Annotations;

namespace NewRelic.Reflection
{

	public class ReflectionImpl : IReflection
	{
		public ConstructorInfo GetConstructor(Type type, Type[] parameterTypes)
		{
			return type.GetConstructor(parameterTypes);
		}
	}

	public class VisibilityBypasser : VisibilityBypasserBase
	{
		public static readonly VisibilityBypasser Instance = new VisibilityBypasser();

		private VisibilityBypasser() : base(new ReflectionImpl())
		{
		}
	}
}
#endif
