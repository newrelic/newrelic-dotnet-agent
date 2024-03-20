// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
    public interface ITransactionAttributeMaker
    {
        IAttributeValueCollection GetAttributes(ImmutableTransaction immutableTransaction, TransactionMetricName transactionMetricName, TimeSpan? apdexT, TimeSpan totalTime, TransactionMetricStatsCollection txStats);
        void SetUserAndAgentAttributes(IAttributeValueCollection attribValues, ITransactionAttributeMetadata metadata);
    }

    public class TransactionAttributeMaker : ITransactionAttributeMaker
    {
       
        private readonly IConfigurationService _configurationService;
        private readonly IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;


        public TransactionAttributeMaker(IConfigurationService configurationService, IAttributeDefinitionService attribDefSvc)
        {
            _configurationService = configurationService;
            _attribDefSvc = attribDefSvc;
        }

        public IAttributeValueCollection GetAttributes(ImmutableTransaction immutableTransaction, TransactionMetricName transactionMetricName, TimeSpan? apdexT, TimeSpan totalTime, TransactionMetricStatsCollection txStats)
        {
            var attribVals = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

            SetUserAndAgentAttributes(attribVals, immutableTransaction.TransactionMetadata);
            SetIntrinsicAttributes(attribVals, immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            return attribVals;
        }

        private void SetIntrinsicAttributes(IAttributeValueCollection attribValues, ImmutableTransaction immutableTransaction, TransactionMetricName transactionMetricName, TimeSpan? apdexT, TimeSpan totalTime, TransactionMetricStatsCollection txStats)
        {
            // Required transaction attributes
            _attribDefs.GetTypeAttribute(TypeAttributeValue.Transaction).TrySetDefault(attribValues);
            _attribDefs.Timestamp.TrySetValue(attribValues, immutableTransaction.StartTime);

            _attribDefs.TransactionName.TrySetValue(attribValues, transactionMetricName.PrefixedName);
            _attribDefs.TransactionNameForError.TrySetValue(attribValues, transactionMetricName.PrefixedName);
            _attribDefs.Guid.TrySetValue(attribValues, immutableTransaction.Guid);

            // Duration is just EndTime minus StartTime for non-web transactions and response time otherwise
            _attribDefs.Duration.TrySetValue(attribValues, immutableTransaction.ResponseTimeOrDuration);

            // Total time is the total amount of time spent, even when work is happening parallel, which means it is the sum of all exclusive times.
            // https://source.datanerd.us/agents/agent-specs/blob/master/Total-Time-Async.md
            _attribDefs.TotalTime.TrySetValue(attribValues, totalTime);

            // CPU time is the total time spent actually doing work rather than waiting. Basically, it's TotalTime minus TimeSpentWaiting.
            // Our agent does not yet the ability to calculate time spent waiting, so we cannot generate this metric.
            // https://source.datanerd.us/agents/agent-specs/blob/master/Total-Time-Async.md
            //attributes.Add(Attribute.BuildCpuTime(immutableTransaction.Duration));

            // Optional transaction attributes
            _attribDefs.QueueDuration.TrySetValue(attribValues, immutableTransaction.TransactionMetadata.QueueTime);
            _attribDefs.ApdexPerfZone.TrySetValue(attribValues, ApdexStats.GetApdexPerfZoneOrNull(immutableTransaction.ResponseTimeOrDuration, apdexT));


            if (immutableTransaction.IsWebTransaction())
            {
                _attribDefs.WebDuration.TrySetValue(attribValues, immutableTransaction.ResponseTimeOrDuration);
            }

            var externalData = txStats.GetUnscopedStat(MetricNames.ExternalAll);
            if (externalData != null)
            {
                _attribDefs.ExternalDuration.TrySetValue(attribValues, externalData.Value1);
                _attribDefs.ExternalCallCount.TrySetValue(attribValues, (float)externalData.Value0);
            }

            var databaseData = txStats.GetUnscopedStat(MetricNames.DatastoreAll);
            if (databaseData != null)
            {
                _attribDefs.DatabaseDuration.TrySetValue(attribValues, databaseData.Value1);
                _attribDefs.DatabaseCallCount.TrySetValue(attribValues, databaseData.Value0);
            }

            if (_configurationService.Configuration.ErrorCollectorEnabled)
            {
                if (immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.HasError)
                {
                    var errorData = immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.ErrorData;

                    _attribDefs.GetTypeAttribute(TypeAttributeValue.TransactionError).TrySetDefault(attribValues);

                    _attribDefs.TimestampForError.TrySetValue(attribValues, errorData.NoticedAt);
                    _attribDefs.ErrorClass.TrySetValue(attribValues, errorData.ErrorTypeName);
                    _attribDefs.ErrorType.TrySetValue(attribValues, errorData.ErrorTypeName);
                    _attribDefs.ErrorMessage.TrySetValue(attribValues, errorData.ErrorMessage);
                    _attribDefs.ErrorDotMessage.TrySetValue(attribValues, errorData.ErrorMessage);
                    _attribDefs.IsError.TrySetValue(attribValues, true);
                    _attribDefs.ErrorEventSpanId.TrySetValue(attribValues, immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.ErrorDataSpanId);

                    if (errorData.IsExpected)
                    {
                        _attribDefs.IsErrorExpected.TrySetValue(attribValues, true);
                    }
                }
                else
                {
                    _attribDefs.IsError.TrySetValue(attribValues, false);
                }
            }

            var isCatParticipant = IsCatParticipant(immutableTransaction);
            var isSyntheticsParticipant = IsSyntheticsParticipant(immutableTransaction);
            var isDistributedTraceParticipant = immutableTransaction.TracingState != null && immutableTransaction.TracingState.HasDataForAttributes;

            // Add the tripId attribute unconditionally, when DT disabled, so it can be used to correlate with 
            // this app's PageView events. If CrossApplicationReferrerTripId is null then this transaction started the first external request, 
            // so use its guid.
            if (!_configurationService.Configuration.DistributedTracingEnabled)
            {
                var tripId = immutableTransaction.TransactionMetadata.CrossApplicationReferrerTripId ?? immutableTransaction.Guid;
                _attribDefs.TripId.TrySetValue(attribValues, tripId);
                _attribDefs.CatNrTripId.TrySetValue(attribValues, tripId);
            }

            if (isCatParticipant)
            {
                _attribDefs.NrGuid.TrySetValue(attribValues, immutableTransaction.Guid);
                _attribDefs.CatReferringPathHash.TrySetValue(attribValues, immutableTransaction.TransactionMetadata.CrossApplicationReferrerPathHash);
                _attribDefs.CatPathHash.TrySetValue(attribValues, immutableTransaction.TransactionMetadata.CrossApplicationPathHash);
                _attribDefs.CatNrPathHash.TrySetValue(attribValues, immutableTransaction.TransactionMetadata.CrossApplicationPathHash);
                _attribDefs.ClientCrossProcessId.TrySetValue(attribValues, immutableTransaction.TransactionMetadata.CrossApplicationReferrerProcessId);
                _attribDefs.CatReferringTransactionGuidForEvents.TrySetValue(attribValues, immutableTransaction.TransactionMetadata.CrossApplicationReferrerTransactionGuid);
                _attribDefs.CatReferringTransactionGuidForTraces.TrySetValue(attribValues, immutableTransaction.TransactionMetadata.CrossApplicationReferrerTransactionGuid);
                _attribDefs.CatAlternativePathHashes.TrySetValue(attribValues, immutableTransaction.TransactionMetadata.CrossApplicationAlternatePathHashes);

            }
            else if (isDistributedTraceParticipant)
            {
                _attribDefs.ParentSpanId.TrySetValue(attribValues, immutableTransaction.TracingState.ParentId ?? immutableTransaction.TracingState.Guid);
                _attribDefs.ParentTransportType.TrySetValue(attribValues, immutableTransaction.TracingState.TransportType);
                _attribDefs.ParentTransportTypeForSpan.TrySetValue(attribValues, immutableTransaction.TracingState.TransportType);

                if (immutableTransaction.TracingState.HasDataForParentAttributes)
                {
                    _attribDefs.ParentTypeForDistributedTracing.TrySetValue(attribValues, immutableTransaction.TracingState.Type);
                    _attribDefs.ParentApp.TrySetValue(attribValues, immutableTransaction.TracingState.AppId);
                    _attribDefs.ParentAccount.TrySetValue(attribValues, immutableTransaction.TracingState.AccountId);
                    _attribDefs.ParentId.TrySetValue(attribValues, immutableTransaction.TracingState.TransactionId);
                    _attribDefs.ParentTransportDuration.TrySetValue(attribValues, immutableTransaction.TracingState.TransportDuration);

                    _attribDefs.ParentTypeForDistributedTracingForSpan.TrySetValue(attribValues, immutableTransaction.TracingState.Type);
                    _attribDefs.ParentAppForSpan.TrySetValue(attribValues, immutableTransaction.TracingState.AppId);
                    _attribDefs.ParentAccountForSpan.TrySetValue(attribValues, immutableTransaction.TracingState.AccountId);
                    _attribDefs.ParentTransportDurationForSpan.TrySetValue(attribValues, immutableTransaction.TracingState.TransportDuration);
                }
            }

            if (_configurationService.Configuration.DistributedTracingEnabled)
            {
                _attribDefs.DistributedTraceId.TrySetValue(attribValues, immutableTransaction.TraceId);
                _attribDefs.Priority.TrySetValue(attribValues, immutableTransaction.Priority);
                _attribDefs.Sampled.TrySetValue(attribValues, immutableTransaction.Sampled);
            }

            if (isSyntheticsParticipant)
            {
                _attribDefs.NrGuid.TrySetValue(attribValues, immutableTransaction.Guid);

                _attribDefs.SyntheticsResourceId.TrySetValue(attribValues, immutableTransaction.TransactionMetadata.SyntheticsResourceId);
                _attribDefs.SyntheticsResourceIdForTraces.TrySetValue(attribValues, immutableTransaction.TransactionMetadata.SyntheticsResourceId);

                _attribDefs.SyntheticsJobId.TrySetValue(attribValues, immutableTransaction.TransactionMetadata.SyntheticsJobId);
                _attribDefs.SyntheticsJobIdForTraces.TrySetValue(attribValues, immutableTransaction.TransactionMetadata.SyntheticsJobId);

                _attribDefs.SyntheticsMonitorId.TrySetValue(attribValues, immutableTransaction.TransactionMetadata.SyntheticsMonitorId);
                _attribDefs.SyntheticsMonitorIdForTraces.TrySetValue(attribValues, immutableTransaction.TransactionMetadata.SyntheticsMonitorId);
            }
        }

        public void SetUserAndAgentAttributes(IAttributeValueCollection attribValues, ITransactionAttributeMetadata metadata)
        {
            _attribDefs.RequestMethod.TrySetValue(attribValues, metadata.RequestMethod);
            _attribDefs.RequestUri.TrySetValue(attribValues, metadata.Uri ?? "/Unknown");

            // original_url should only be generated if it is distinct from the current URI
            if (metadata.OriginalUri != metadata.Uri)
            {
                _attribDefs.OriginalUrl.TrySetValue(attribValues, metadata.OriginalUri);
            }

            _attribDefs.RequestReferrer.TrySetValue(attribValues, metadata.ReferrerUri);
            _attribDefs.QueueWaitTime.TrySetValue(attribValues, metadata.QueueTime);
            _attribDefs.ResponseStatus.TrySetValue(attribValues, metadata.HttpResponseStatusCode);
            _attribDefs.HttpStatusCode.TrySetValue(attribValues, metadata.HttpResponseStatusCode);

            _attribDefs.HostDisplayName.TrySetValue(attribValues, _configurationService.Configuration.ProcessHostDisplayName);

            
            if (_configurationService.Configuration.ErrorCollectorEnabled && metadata.ReadOnlyTransactionErrorState.HasError && metadata.ReadOnlyTransactionErrorState.ErrorData != null && metadata.ReadOnlyTransactionErrorState.ErrorData.CustomAttributes != null)
            {
                foreach(var errAttrib in metadata.ReadOnlyTransactionErrorState.ErrorData.CustomAttributes)
                {
                    _attribDefs.GetCustomAttributeForError(errAttrib.Key).TrySetValue(attribValues, errAttrib.Value);

                }
            }

            if (metadata.IsLlmTransaction)
            {
                _attribDefs.LlmTransaction.TrySetValue(attribValues, true);
            }

            attribValues.AddRange(metadata.UserAndRequestAttributes);
        }

        private static bool IsCatParticipant(ImmutableTransaction immutableTransaction)
        {
            // You are a CAT participant if you received valid CAT headers on an inbound request data or you received an inbound response with CAT data
            return immutableTransaction.TransactionMetadata.CrossApplicationReferrerProcessId != null
                || immutableTransaction.TransactionMetadata.HasCatResponseHeaders;
        }

        private static bool IsSyntheticsParticipant(ImmutableTransaction immutableTransaction)
        {
            return immutableTransaction.TransactionMetadata.SyntheticsResourceId != null
                && immutableTransaction.TransactionMetadata.SyntheticsJobId != null
                && immutableTransaction.TransactionMetadata.SyntheticsMonitorId != null;

        }
    }
}
