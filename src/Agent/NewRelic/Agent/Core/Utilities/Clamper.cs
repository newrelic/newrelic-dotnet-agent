// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Utilities
{

    /// <summary>
    /// Implements clamping policy on parameters received from calls into the API.
    /// </summary>
    public class Clamper
    {
        const int CLAMPED_STRING_LENGTH = 1000;
        /// <summary>
        /// Clamp the linearized length of fromUser to a fixed maximum length.
        /// </summary>
        /// <param name="fromUser">The string given to the API from the user; may be nefariously constructed.</param>
        /// <returns>fromUser or a copy thereof, appropriately modified</returns>
        public static string ClampLength(string fromUser)
        {
            return ClampLength(fromUser, CLAMPED_STRING_LENGTH);
        }

        const int CLAMPED_EXCEPTION_LENGTH = 10 * 1000;
        /// <summary>
        /// Clamp the linearized length of fromUser to a fixed maximum length.
        /// </summary>
        /// <param name="fromUser">The exception given to the API from the user; may be nefariously constructed.</param>
        /// <returns>fromUser or a copy thereof, appropriately modified</returns>
        public static Exception ClampLength(Exception fromUser)
        {
            return ClampLength(fromUser, CLAMPED_EXCEPTION_LENGTH);
        }

        /// <summary>
        /// Clamp the length of the string fromUser to a maximum length of maxLength characters.
        /// </summary>
        /// <param name="fromUser">The string given to use from the user, which may be nefariously constructed.</param>
        /// <param name="maxLength">The maximum length allowable, in characters.</param>
        /// <returns>fromUser if it is small enough, or a copy of fromUser retaining only the prefix of length maxLength characters.</returns>
        public static string ClampLength(string fromUser, int maxLength)
        {
            if (fromUser.Length > maxLength)
            {
                return fromUser.Substring(0, Math.Min(fromUser.Length, maxLength));
            }
            else
            {
                return fromUser;
            }
        }

        /// <summary>
        /// Clamp the length of a linearized version of fromUser to a maximum length of maxLength characters.
        /// </summary>
        /// <param name="fromUser">The dictionary given to use from the user, which may be nefariously constructed.</param>
        /// <param name="maxLength">The maximum length allowable, in characters.</param>
        /// <returns>fromUser if it is small enough, or a copy of fromUser as randomly modified to have less than maxLength characters.</returns>
        public static IDictionary<string, string> ClampLength(IDictionary<string, string> fromUser, int maxLength)
        {
            if (fromUser == null)
            {
                return null;
            }

            // Measure the total length, and return the given dictionary if it is small enough.
            {
                int totalLength = 0;
                foreach (KeyValuePair<string, string> kvp in fromUser)
                {
                    totalLength += (kvp.Key != null) ? kvp.Key.Length : 0;
                    totalLength += (kvp.Value != null) ? kvp.Value.Length : 0;
                    if (totalLength > maxLength)
                    {
                        break;
                    }
                }
                if (totalLength <= maxLength)
                {
                    return fromUser;
                }
            }

            // Copy fromUser, with clamping on the total length.
            {
                IDictionary<string, string> clamped = new Dictionary<string, string>();
                int totalLength = 0;
                foreach (KeyValuePair<string, string> kvp in fromUser)
                {
                    totalLength += (kvp.Key != null) ? kvp.Key.Length : 0;
                    totalLength += (kvp.Value != null) ? kvp.Value.Length : 0;
                    if (totalLength > maxLength)
                    {
                        break;
                    }
                    clamped[kvp.Key] = kvp.Value;
                }
                return clamped;
            }
        }

        /// <summary>
        /// Clamp the length of any message resulting from fromUser to a maximum length of maxLength characters.
        /// </summary>
        /// <param name="fromUser">The exception given to use from the user, which may be nefariously constructed.</param>
        /// <param name="maxLength">The maximum length allowable, in characters.</param>
        /// <returns>fromUser, appropriate modified</returns>
        public static Exception ClampLength(Exception fromUser, int maxLength)
        {
            {
                int totalLength = 0;
                Exception chain = fromUser;
                while (chain != null)
                {
                    totalLength += chain.Message.Length;
                    chain = chain.InnerException;
                }
                if (totalLength < maxLength)
                {
                    return fromUser;
                }
            }

            return fromUser;
        }
    }
}
