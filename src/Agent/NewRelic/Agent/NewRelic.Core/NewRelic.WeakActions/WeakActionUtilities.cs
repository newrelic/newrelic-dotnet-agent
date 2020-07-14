using System;
using System.Reflection;
using JetBrains.Annotations;

namespace NewRelic.WeakActions
{
	public class WeakActionUtilities
	{
		[NotNull]
		public static IWeakAction<T> MakeWeak<T>([NotNull] Action<T> action, Action<Action<T>> actionGarbageCollectedCallback)
		{
			if (action == null)
				throw new ArgumentNullException("action");

			var weakActionType = CreateWeakActionType(action);
			var weakActionConstructor = GetWeakActionConstructor<T>(weakActionType);
			var weakAction = ConstructWeakAction(weakActionConstructor, action, actionGarbageCollectedCallback);

			return weakAction;
		}

		[NotNull]
		private static Type CreateWeakActionType<T>([NotNull] Action<T> action)
		{
			if (action.Method == null)
				throw new ArgumentException("Action must have a MethodInfo.", "action");

			var typeOfWeakAction = typeof(WeakAction<,>);
			return typeOfWeakAction.MakeGenericType(action.Method.DeclaringType, typeof(T));
		}

		[NotNull]
		private static ConstructorInfo GetWeakActionConstructor<T>([NotNull] Type weakActionGenericInstantiationType)
		{
			var constructor = weakActionGenericInstantiationType.GetConstructor(new[] { typeof(Action<T>), typeof(Action<Action<T>>) });
			if (constructor == null)
				throw new NullReferenceException("Unable to locate an appropriate constructor for WeakAction.");

			return constructor;
		}

		[NotNull]
		private static IWeakAction<T> ConstructWeakAction<T>([NotNull] ConstructorInfo weakActionConstructor, [NotNull] Action<T> action, [CanBeNull] Action<Action<T>> actionGarbageCollectedCallback)
		{
			var weakAction = (IWeakAction<T>)weakActionConstructor.Invoke(new Object[] { action, actionGarbageCollectedCallback });
			if (weakAction == null)
				throw new NullReferenceException("Unable to invoke weakEventHandlerConstructor.");

			return weakAction;
		}

		[NotNull]
		public static IWeakAction<T1, T2> MakeWeak<T1, T2>([NotNull] Action<T1, T2> action, Action<Action<T1, T2>> actionGarbageCollectedCallback)
		{
			if (action == null)
				throw new ArgumentNullException("action");

			var weakActionType = CreateWeakActionType(action);
			var weakActionConstructor = GetWeakActionConstructor<T1, T2>(weakActionType);
			var weakAction = ConstructWeakAction(weakActionConstructor, action, actionGarbageCollectedCallback);

			return weakAction;
		}

		[NotNull]
		private static Type CreateWeakActionType<T1, T2>([NotNull] Action<T1, T2> action)
		{
			if (action.Method == null)
				throw new ArgumentException("Action must have a MethodInfo.", "action");

			var typeOfWeakAction = typeof(WeakAction<,,>);
			return typeOfWeakAction.MakeGenericType(action.Method.DeclaringType, typeof(T1), typeof(T2));
		}

		[NotNull]
		private static ConstructorInfo GetWeakActionConstructor<T1, T2>([NotNull] Type weakActionGenericInstantiationType)
		{
			var constructor = weakActionGenericInstantiationType.GetConstructor(new[] { typeof(Action<T1, T2>), typeof(Action<Action<T1, T2>>) });
			if (constructor == null)
				throw new NullReferenceException("Unable to locate an appropriate constructor for WeakAction.");

			return constructor;
		}

		[NotNull]
		private static IWeakAction<T1, T2> ConstructWeakAction<T1, T2>([NotNull] ConstructorInfo weakActionConstructor, [NotNull] Action<T1, T2> action, [CanBeNull] Action<Action<T1, T2>> actionGarbageCollectedCallback)
		{
			var weakAction = (IWeakAction<T1, T2>)weakActionConstructor.Invoke(new Object[] { action, actionGarbageCollectedCallback });
			if (weakAction == null)
				throw new NullReferenceException("Unable to invoke weakEventHandlerConstructor.");

			return weakAction;
		}
	}
}
