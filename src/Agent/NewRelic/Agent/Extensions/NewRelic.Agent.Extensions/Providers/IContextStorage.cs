namespace NewRelic.Agent.Extensions.Providers
{
    /// <summary>
    /// Classes that implement this interface provide storage mechanism for object instances within the context
    /// of code execution (a transaction).  A context is a place to store an object instance.  For the most basic applications, this may be backed by a 
    /// [ThreadStatic] dictionary while more complex contexts may have logic that ensures the instance 
    /// correctly jumps threads as code execution moves between processing threads.  IStorageContextFactory 
    /// should also be implemented which will create the IStorageContext instance.
    /// </summary>
    public interface IContextStorage<T>
    {
        /// <summary>
        /// The priority this context should have over other contexts.  Higher priority contexts trump lower priority contexts.
        /// </summary>
        /// <remarks>The .NET Agent built-in contexts will all be less than 128 so a user can override all built-in contexts by choosing a priority over 128.  If two contexts have the same priority, it is undefined which one will take precedence.</remarks>
        byte Priority { get; }
        /// <summary>
        /// Identifies whether this context can provide a context at this time.  In some cases, a context might not be able to provide a context due to the current state of the system, in which case the next context in the list (based on priority) will be used.
        /// </summary>
        /// <remarks>Implementations of this method should be very performant as it will be called with fairly high frequency.</remarks>
        bool CanProvide { get; }
        /// <summary>
        /// Get the data stored in the current context.
        /// </summary>
        /// <returns>The value or null if it doesn't exist.</returns>
        /// <remarks>This should be as performant as possible since it will be called quite frequently.</remarks>
        T GetData();
        /// <summary>
        /// Set the data stored in the current context.  If this is invoked multiple times it should always overwrite
        /// the previous value without throwing an exception.
        /// </summary>
        /// <param name="value">The data that should be stored in the current context.</param>
        /// <remarks>This should be as performant as possible since it will be called quite frequently.  A null <paramref name="value"/> can be treated as a removal of the key if desired.</remarks>
        void SetData(T value);
        void Clear();
    }
}
