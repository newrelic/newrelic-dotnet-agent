using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.AssemblyLoading
{
	/// <summary>
	/// This service registers an AssemblyResolve handler that will be fired whenever assembly resolution fails. The handler essentially acts as an assembly version redirect for any assembly that loaded by NewRelic code.
	/// </summary>
	public class AssemblyResolutionService : IDisposable
	{
		public AssemblyResolutionService()
		{
			AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolutionFailure;
		}

		public void Dispose()
		{
			AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolutionFailure;
		}

		private static Assembly OnAssemblyResolutionFailure(Object sender, ResolveEventArgs args)
		{
			if (args == null)
				return null;
			if (!IsWrapperServiceOrTracerInStack())
				return null;

			return TryGetAlreadyLoadedAssemblyFromFullName(args.Name);
		}

		private static Assembly TryGetAlreadyLoadedAssemblyFromFullName(String fullAssemblyName)
		{
			if (fullAssemblyName == null)
				return null;

			var simpleAssemblyName = new AssemblyName(fullAssemblyName).Name;
			if (simpleAssemblyName == null)
				return null;

			return TryGetAlreadyLoadedAssemblyBySimpleName(simpleAssemblyName);
		}

		private static Assembly TryGetAlreadyLoadedAssemblyBySimpleName(String simpleAssemblyName)
		{
			return AppDomain.CurrentDomain.GetAssemblies()
				.Where(assembly => assembly != null)
				.Where(assembly => assembly.GetName().Name == simpleAssemblyName)
				.FirstOrDefault();
		}

		private static Boolean IsWrapperServiceOrTracerInStack()
		{
			var stackFrames = new StackTrace().GetFrames();
			if (stackFrames == null)
				return false;

			return stackFrames
				.Select(GetFrameType)
				.Where(type => type != null)
				.Where(type => type != typeof (AssemblyResolutionService))
				.Where(IsNewRelicType)
				.Any();
		}

		[CanBeNull]
		private static Type GetFrameType([CanBeNull] StackFrame stackFrame)
		{
			if (stackFrame == null)
				return null;

			var method = stackFrame.GetMethod();
			if (method == null)
				return null;

			return method.DeclaringType;
		}

		private static Boolean IsNewRelicType([NotNull] Type type)
		{
			var typeName = type.FullName;
			if (typeName == null)
				return false;

			return typeName.StartsWith("NewRelic");
		}
	}
}