using System;
using System.Reflection;

namespace NewRelic.WeakActions
{
    public class WeakAction
    {
        protected readonly WeakReference WeakReference;
        protected readonly MethodInfo MethodInfo;

        protected WeakAction(Object target, MethodInfo methodInfo)
        {
            if (methodInfo == null)
                throw new ArgumentException("action.Method is null.", "methodInfo");

            // save off a weak reference to the target of this action
            WeakReference = new WeakReference(target);
            MethodInfo = methodInfo;
        }
    }

    public class WeakAction<TTarget, T> : WeakAction, IWeakAction<T> where TTarget : class
    {
        private delegate void OpenDelegate(TTarget @this, T type);
        private readonly OpenDelegate _openDelegate;
        private Action<Action<T>> _actionWasGarbageCollectedCallback;

        public WeakAction(Action<T> action, Action<Action<T>> actionWasGarbageCollectedCallback) :
            base(action.Target, action.Method)
        {
            // create an open delegate to the action
            _openDelegate = (OpenDelegate)Delegate.CreateDelegate(typeof(OpenDelegate), MethodInfo);
            // save off the delegate we should call should the target of this action be destroyed
            _actionWasGarbageCollectedCallback = actionWasGarbageCollectedCallback;
        }

        private void Invoke(T parameter)
        {
            // get a strong reference to the target before we look at it
            var target = (TTarget)WeakReference.Target;

            // if the target is valid then invoke the method on the target, otherwise unregister it
            if (target != null)
            {
                _openDelegate(target, parameter);
            }
            else if (_actionWasGarbageCollectedCallback != null)
            {
                _actionWasGarbageCollectedCallback(Invoke);
                _actionWasGarbageCollectedCallback = null;
            }
        }

        public Action<T> Action
        {
            get { return Invoke; }
        }
    }

    public class WeakAction<TTarget, T1, T2> : WeakAction, IWeakAction<T1, T2> where TTarget : class
    {
        private delegate void OpenDelegate(TTarget @this, T1 type1, T2 type2);
        private readonly OpenDelegate _openDelegate;
        private Action<Action<T1, T2>> _actionWasGarbageCollectedCallback;

        public WeakAction(Action<T1, T2> action, Action<Action<T1, T2>> actionWasGarbageCollectedCallback) :
            base(action.Target, action.Method)
        {
            // save off the delegate we should call should the target of this action be destroyed
            _actionWasGarbageCollectedCallback = actionWasGarbageCollectedCallback;
            // create an open delegate to the action
            _openDelegate = (OpenDelegate)Delegate.CreateDelegate(typeof(OpenDelegate), MethodInfo);
        }

        private void Invoke(T1 parameter1, T2 parameter2)
        {
            // get a strong reference to the target before we look at it
            var target = (TTarget)WeakReference.Target;

            // if the target is valid then invoke the method on the target, otherwise unregister it
            if (target != null)
            {
                _openDelegate(target, parameter1, parameter2);
            }
            else if (_actionWasGarbageCollectedCallback != null)
            {
                _actionWasGarbageCollectedCallback(Invoke);
                _actionWasGarbageCollectedCallback = null;
            }
        }

        public Action<T1, T2> Action
        {
            get { return Invoke; }
        }
    }
}
