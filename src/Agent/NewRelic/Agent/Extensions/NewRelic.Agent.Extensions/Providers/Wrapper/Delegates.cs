// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    /// <summary>
    /// A delegate that is returned by a wrapper and will be called when the wrapped method completes.
    /// </summary>
    public delegate void AfterWrappedMethodDelegate(object result, Exception exception);

    public static class Delegates
    {
        /// <summary>
        /// A delegate that does nothing after a wrapped method completes.
        /// </summary>
        public static readonly AfterWrappedMethodDelegate NoOp = (_, __) => { };

        /// <summary>
        /// Creates a delegate that will call each of the provided actions that aren't null, as appropriate. <paramref name="onComplete"/> will always be called after a wrapped method call is finished. <paramref name="onSuccess"/> will be called only if the wrapped method completes without an exception being thrown. <paramref name="onFailure"/> will only be called if the wrapped method throws an exception. <paramref name="onComplete"/> will always be called AFTER <paramref name="onSuccess"/> and <paramref name="onFailure"/>.
        /// </summary>
        /// <typeparam name="T">The return type of the wrapped method.</typeparam>
        /// <param name="onComplete">Called when the wrapped method finishes, success or failure.</param>
        /// <param name="onSuccess">Called when the wrapped method finishes successfully.</param>
        /// <param name="onFailure">Called when the wrapped method finishes unsuccessfully.</param>
        /// <returns>A delegate that will call the provided actions after the wrapped method call is finished.</returns>
        public static AfterWrappedMethodDelegate GetDelegateFor<T>(Action onComplete = null, Action<T> onSuccess = null, Action<Exception> onFailure = null)
        {
            return (result, exception) =>
            {
                if (onSuccess != null && exception == null && (result == null || result is T))
                    onSuccess((T)result);

                if (onFailure != null && exception != null)
                    onFailure(exception);

                if (onComplete != null)
                    onComplete();
            };
        }

        /// <summary>
        /// Creates a delegate that will call each of the provided actions that aren't null, as appropriate. <paramref name="onComplete"/> will always be called after a wrapped method call is finished. <paramref name="onSuccess"/> will be called only if the wrapped method completes without an exception being thrown. <paramref name="onFailure"/> will only be called if the wrapped method throws an exception. <paramref name="onComplete"/> will always be called AFTER <paramref name="onSuccess"/> and <paramref name="onFailure"/>.
        /// 
        /// This overload is useful for wrapped methods that return void, or for methods where you do not care about the return value.
        /// </summary>
        /// <param name="onComplete">Called when the wrapped method finishes, success or failure.</param>
        /// <param name="onSuccess">Called when the wrapped method finishes successfully.</param>
        /// <param name="onFailure">Called when the wrapped method finishes unsuccessfully.</param>
        /// <returns>A delegate that will call the provided actions after the wrapped method call is finished.</returns>
        public static AfterWrappedMethodDelegate GetDelegateFor(Action onComplete = null, Action onSuccess = null, Action<Exception> onFailure = null)
        {
            return (result, exception) =>
            {
                if (onSuccess != null && exception == null)
                    onSuccess();

                if (onFailure != null && exception != null)
                    onFailure(exception);

                if (onComplete != null)
                    onComplete();
            };
        }

        /// <summary>
        /// Returns a delegate that calls Segment.End() onComplete.
        /// </summary>
        /// <param name="segment"></param>
        public static AfterWrappedMethodDelegate GetDelegateFor(ISegment segment)
        {
            return segment.IsValid ? GetDelegateFor(segment.End) : NoOp;
        }
    }
}
