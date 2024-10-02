// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Utilities
{
    public class Memoizer
    {
        /// <summary>
        /// Checks to see if <paramref name="backer"/> is null and then either populates <paramref name="backer"/> and returns it (null case) or just returns it (not null case).
        /// </summary>
        /// <typeparam name="T">The type of the object to be memoized. Can be inferred from parameters.</typeparam>
        /// <param name="backer">The place to store the memoized <typeparamref name="T"/>.</param>
        /// <param name="evaluator">The method to call when backer is null. This should not return null.</param>
        /// <returns>The memoized <typeparamref name="T"/>.</returns>
        public static T Memoize<T>(ref T backer, Func<T> evaluator) where T : class
        {
            if (evaluator == null)
                throw new ArgumentNullException("evaluator");

            return backer ?? (backer = evaluator());
        }

        /// <summary>
        /// Checks to see if <paramref name="backer"/> is null and then either populates <paramref name="backer"/> and returns it (null case) or just returns it (not null case).
        /// </summary>
        /// <typeparam name="T">The type of the object to be memoized. Can be inferred from parameters.</typeparam>
        /// <param name="backer">The place to store the memoized <typeparamref name="T"/>.</param>
        /// <param name="evaluator">The method to call when backer is null.</param>
        /// <returns>The memoized <typeparamref name="T"/>.</returns>
        public static T Memoize<T>(ref T? backer, Func<T> evaluator) where T : struct
        {
            if (evaluator == null)
                throw new ArgumentNullException("evaluator");

            return (backer ?? (backer = evaluator())).Value;
        }
    }
}
