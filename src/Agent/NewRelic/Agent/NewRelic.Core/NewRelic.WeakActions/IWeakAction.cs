using System;

namespace NewRelic.WeakActions
{
    public interface IWeakAction<T>
    {
        Action<T> Action { get; }
    }

    public interface IWeakAction<T1, T2>
    {
        Action<T1, T2> Action { get; }
    }
}
