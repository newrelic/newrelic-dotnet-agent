// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Api.Experimental;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Api.Experimental
{
    /// <summary>
    /// This interface contains methods we may eventually move to <see cref="ITransaction"/> once they have been sufficiently vetted.
    /// Methods on this interface are subject to refactoring or removal in future versions of the API.
    /// </summary>
    public interface ITransactionExperimental
    {
        /// <summary>
        /// Returns the object that uniquely identifies the starting wrapper.
        /// </summary>
        /// <returns></returns>
        object GetWrapperToken();

        /// <summary>
        /// Set the object that uniquely identifies the starting wrapper.
        /// </summary>
        /// <param name="wrapperToken">Wrapper token.</param>
        void SetWrapperToken(object wrapperToken);

        /// <summary>
        /// Starts a segment that can represent any type of method call including datastore and external operations.
        /// </summary>
        /// <param name="methodCall">The method call that is responsible for starting this segment.</param>
        /// <returns>An opaque object that will be needed when you want to end the segment.</returns>
        ISegment StartSegment(MethodCall methodCall);

        /// <summary>
        /// Creates an object that holds the data that represents an external request. This data can be added to a
        /// segment so that a segment can represent an external request.
        /// </summary>
        /// <param name="destinationUri">The destination URI of the external request.</param>
        /// <param name="method">The method of the request, such as an HTTP verb (e.g. GET or POST).</param>
        /// <returns>An object that can be used to manage all of the data we support for external requests.</returns>
        IExternalSegmentData CreateExternalSegmentData(Uri destinationUri, string method);

        /// <summary>
        /// Creates an object that holds the data that represents a datastore request. This data can be added to a
        /// segment so that a segment can represent a datastore request.
        /// </summary>
        /// <param name="sqlStatement">An object containing information about the request being made.</param>
        /// <param name="connectionInfo">An object containing information about the service the request is being made to.</param>
        /// <param name="commandText">An string with the raw datastore statement text. Can be an empty string.</param>
        /// <param name="queryParameters">A dictionary of query parameter names and values. Can be null.</param>
        /// <returns>An object that can be used to manage all of the data we support for datastore requests.</returns>
        IDatastoreSegmentData CreateDatastoreSegmentData(ParsedSqlStatement sqlStatement, ConnectionInfo connectionInfo, string commandText, IDictionary<string, IConvertible> queryParameters);

        DateTime StartTime { get; }

        ISegment StartStackExchangeRedisSegment(int invocationTargetHashCode, ParsedSqlStatement parsedSqlStatement, ConnectionInfo connectionInfo, TimeSpan relativeStartTime, TimeSpan relativeEndTime);

        /// <summary>
        /// Starts a segment from an Activity.
        /// </summary>
        /// <param name="methodCall">The method call that is responsible for starting this segmnt.</param>
        /// <param name="activity">The Activity that corresponds to the segment.</param>
        /// <returns>An opaque object that will be needed when you want to end the segment.</returns>
        ISegment StartActivitySegment(MethodCall methodCall, INewRelicActivity activity);
    }
}
