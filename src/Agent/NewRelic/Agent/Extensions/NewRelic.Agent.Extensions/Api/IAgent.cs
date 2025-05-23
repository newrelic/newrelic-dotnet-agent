// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NewRelic.Agent.Api
{
    /// <summary>
    /// The API that the agent provides to wrappers.
    /// </summary>
    public interface IAgent : IAgentExperimental
    {
        IConfiguration Configuration { get; }

        ILogger Logger { get; }

        /// <summary>
        /// Returns the current transaction.  This will either return a transaction
        /// if one has already been started or a dummy instance of a transaction
        /// if one does not already exist.
        /// </summary>
        ITransaction CurrentTransaction { get; }

        /// <summary>
        /// Create a new transaction for processing a request.
        /// </summary>
        /// <param name="destinationType"></param>
        /// <param name="brokerVendorName">The name of the message broker vendor. Must not be null.</param>
        /// <param name="destination">The destination queue of the message being handled. Can be null.</param>
        /// <param name="onCreate">A callback that is called if a transaction is created. Can be null.</param>
        /// <returns></returns>
        ITransaction CreateTransaction(MessageBrokerDestinationType destinationType, string brokerVendorName, string destination = null, Action wrapperOnCreate = null);


        /// <summary>
        /// Create a new transaction for processing a request, conforming to the naming requirements of the Kafka spec.
        /// </summary>
        /// <param name="destinationType"></param>
        /// <param name="brokerVendorName">The name of the message broker vendor. Must not be null.</param>
        /// <param name="destination">The destination queue of the message being handled. Can be null.</param>
        /// <param name="onCreate">A callback that is called if a transaction is created. Can be null.</param>
        /// <returns></returns>
        ITransaction CreateKafkaTransaction(MessageBrokerDestinationType destinationType, string brokerVendorName, string destination = null, Action wrapperOnCreate = null);

        /// <summary>
        /// Create a new transaction for processing a request.
        /// </summary>
        /// <param name="isWeb"></param>
        /// <param name="category"></param>
        /// <param name="transactionDisplayName"></param>
        /// <param name="doNotTrackAsUnitOfWork"></param>
        /// <param name="wrapperOnCreate"></param>
        /// <returns></returns>
        ITransaction CreateTransaction(bool isWeb, string category, string transactionDisplayName, bool doNotTrackAsUnitOfWork, Action wrapperOnCreate = null);

        /// <summary>
        /// Instructs the Agent to try to track async work under a new transaction where there is a desire to track work spawned on a new thread as a separate transaction.
        /// This may be useful in asynchronous scenarios where there is a desire to track Fire and Forget actions.
        /// </summary>
        /// <returns>true if the work will be tracked as a separate transaction</returns>
        bool TryTrackAsyncWorkOnNewTransaction();

        /// <summary>
        /// Casts an object as an ISegment instance.  This should be used when casting values retrieved from 
        /// dictionaries as segments because it guarantees a non-null segment return value.
        /// </summary>
        /// <param name="segment">An object that should be an instance of ISegment</param>
        /// <returns>A non-null ISegment instance.</returns>
        ISegment CastAsSegment(object segment);

        /// <summary>
        /// Sets up the resources necessary to execute an explain plan.
        /// </summary>
        /// <param name="segment">The datastore segment candidate for an explain plan</param>
        /// <param name="allocateExplainPlanResources">Function which returns the resources necessary for executing the explain plan</param>
        /// <param name="generateExplainPlan">Function for executing the explain plan</param>
        /// <param name="vendorValidateShouldExplain">Function for executing any additional vendor validation on if an explain plan should be ran</param>
        void EnableExplainPlans(ISegment segment, Func<object> allocateExplainPlanResources, Func<object, ExplainPlan> generateExplainPlan, Func<VendorExplainValidationResult> vendorValidateShouldExplain);

        /// <summary>
        /// Tell the agent about an error that just occurred in the wrapper. Normally exceptions should just be thrown so that the agent can handle them directly, but this method is useful in situations where exceptions are happening outside the scope of the agent (for example, on another thread). This method is thread-safe.
        /// </summary>
        /// <param name="exception">The exception that occurred. Must not be null.</param>
        void HandleWrapperException(Exception exception);

        /// <summary>
        /// Returns a stream that will inject content that the agent thinks is important into <paramref name="stream"/>, or null.
        /// 
        /// This method should be called as late as possible (i.e. just before the stream is read) to ensure that the metadata passed in (encoding, contentType, etc) is no longer volatile.
        /// 
        /// This method will return null under many different conditions, including due to configuration settings or internal business logic.
        /// </summary>
        /// <param name="stream">The stream to inject content into.</param>
        /// <param name="encoding">The encoding of the data in the stream.</param>
        /// <param name="contentType">The type of content in the stream.</param>
        /// <param name="requestPath">The path of the request</param>
        Stream TryGetStreamInjector(Stream stream, Encoding encoding, string contentType, string requestPath);

        /// <summary>
        /// Used by AspNetCore6Plus, injects the RUM script if various conditions are met. Assumes (perhaps boldly) that the
        /// page content is UTF-8 encoded.
        /// 
        /// This method should be called as late as possible (i.e. just before the stream is read) to ensure that the metadata passed in (encoding, contentType, etc) is no longer volatile.
        /// </summary>
        /// <param name="contentType">The type of content in the stream.</param>
        /// <param name="requestPath">The path of the request</param>
        /// <param name="buffer">A UTF-8 encoded buffer of the content for this request</param>
        /// <param name="baseStream">The stream into which the script (and buffer) should be injected</param>
        /// <returns></returns>
        Task TryInjectBrowserScriptAsync(string contentType, string requestPath, byte[] buffer, Stream baseStream);

        /// <summary>
        /// Returns the Trace Metadata of the currently executing transaction.
        /// </summary>
        ITraceMetadata TraceMetadata { get; }

        /// <summary>
        /// Returns the Linking Metadata that is used to correlate application data in the New Relic backend.
        /// </summary>
        /// <returns>Dictionary of key/value pairs.</returns>
        Dictionary<string, string> GetLinkingMetadata();

        /// <summary>
        /// Sets metadata needed by the ServerlessModeDataTransportService
        /// </summary>
        /// <param name="lambdaFunctionVersion"></param>
        /// <param name="lambdaFunctionArn"></param>
        void SetServerlessParameters(string lambdaFunctionVersion, string lambdaFunctionArn);
    }
}
