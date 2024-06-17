// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Helpers;
using NewRelic.Agent.Extensions.SystemExtensions;
using NewRelic.Agent.Extensions.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
    public interface ITransactionTraceMaker
    {
        TransactionTraceWireModel GetTransactionTrace(ImmutableTransaction immutableTransaction, IEnumerable<ImmutableSegmentTreeNode> segmentTrees, TransactionMetricName transactionMetricName, IAttributeValueCollection attributes);
    }

    public class TransactionTraceMaker : ITransactionTraceMaker
    {
        private readonly IConfigurationService _configurationService;
        private readonly IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;

        public TransactionTraceMaker(IConfigurationService configurationService, IAttributeDefinitionService attribDefSvc)
        {
            _configurationService = configurationService;
            _attribDefSvc = attribDefSvc;
        }

        public TransactionTraceWireModel GetTransactionTrace(ImmutableTransaction immutableTransaction, IEnumerable<ImmutableSegmentTreeNode> segmentTrees, TransactionMetricName transactionMetricName, IAttributeValueCollection attribValues)
        {
            segmentTrees = segmentTrees.ToList();

            if (!segmentTrees.Any())
                throw new ArgumentException("There must be at least one segment to create a trace");

            var filteredAttributes = new AttributeValueCollection(attribValues, AttributeDestinations.TransactionTrace);

            // See spec for details on these fields: https://source.datanerd.us/agents/agent-specs/blob/master/Transaction-Trace-LEGACY.md
            var startTime = immutableTransaction.StartTime;
            var duration = immutableTransaction.ResponseTimeOrDuration;
            string uri = null;

            if(_attribDefs.RequestUri.IsAvailableForAny(AttributeDestinations.TransactionTrace))
            { 
                uri = immutableTransaction.TransactionMetadata.Uri?.TrimAfterAChar(StringSeparators.QuestionMarkChar) ?? "/Unknown";
            }

            var guid = immutableTransaction.Guid;
            var xraySessionId = null as ulong?; // The .NET agent does not support xray sessions

            var isSynthetics = immutableTransaction.TransactionMetadata.IsSynthetics;
            var syntheticsResourceId = immutableTransaction.TransactionMetadata.SyntheticsResourceId;
            var rootSegment = GetRootSegment(segmentTrees, immutableTransaction);
            

            var traceData = new TransactionTraceData(startTime, rootSegment, attribValues);

            var trace = new TransactionTraceWireModel(startTime, duration, transactionMetricName.PrefixedName, uri, traceData, guid, xraySessionId, syntheticsResourceId, isSynthetics);

            return trace;
        }

        private TransactionTraceSegment GetRootSegment(IEnumerable<ImmutableSegmentTreeNode> segmentTrees, ImmutableTransaction immutableTransaction)
        {
            var relativeStartTime = TimeSpan.Zero;
            var relativeEndTime = immutableTransaction.Duration;
            const string name = "ROOT";
            var segmentParameters = new Dictionary<string, object>();

            // Due to a bug in the UI, we must insert a fake top-level segment to be the parent of all of the REAL top-level segments. The UI does not know how to handle multiple top-level segments, but inserting a single faux segment to be the parent is a reasonable workaround.
            var fauxTopLevelSegment = GetFauxTopLevelSegment(segmentTrees, immutableTransaction);
            var children = new[] { fauxTopLevelSegment };

            var firstSegmentClassName = fauxTopLevelSegment.ClassName;
            var firstSegmentMethodName = fauxTopLevelSegment.MethodName;

            return new TransactionTraceSegment(relativeStartTime, relativeEndTime, name, segmentParameters, children, firstSegmentClassName, firstSegmentMethodName);
        }

        private TransactionTraceSegment GetFauxTopLevelSegment(IEnumerable<ImmutableSegmentTreeNode> segmentTrees, ImmutableTransaction immutableTransaction)
        {
            var relativeStartTime = TimeSpan.Zero;
            var relativeEndTime = immutableTransaction.Duration;
            const string name = "Transaction";
            var segmentParameters = new Dictionary<string, object>();
            var children = segmentTrees.Select(childNode => CreateTransactionTraceSegment(childNode, immutableTransaction)).ToList();
            var firstSegmentClassName = children.First().ClassName;
            var firstSegmentMethodName = children.First().MethodName;

            return new TransactionTraceSegment(relativeStartTime, relativeEndTime, name, segmentParameters, children, firstSegmentClassName, firstSegmentMethodName);
        }

        private TransactionTraceSegment CreateTransactionTraceSegment(ImmutableSegmentTreeNode node, ImmutableTransaction immutableTransaction)
        {

            var relativeStartTime = node.Segment.RelativeStartTime;
            var relativeEndTime = relativeStartTime + node.Segment.DurationOrZero;
            var name = node.Segment.GetTransactionTraceName();
            var segmentParameters = node.Segment.Parameters.ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);
            var children = node.Children.Select(childNode => CreateTransactionTraceSegment(childNode, immutableTransaction)).ToList();
            var className = node.Segment.MethodCallData.TypeName;
            var methodName = node.Segment.MethodCallData.MethodName;

            node.Segment.Data.AddTransactionTraceParameters(_configurationService, node.Segment, segmentParameters, immutableTransaction);

            // See Total Time spec for a description of this segment parameters: https://source.datanerd.us/agents/agent-specs/blob/master/2015-07-0011-Total-Time-Async.md
            // Note that because the .NET agent can't tell which segments are asynchronous we just assume that every trace has some asynchronous work.
            var exclusiveTime = node.Segment.ExclusiveDurationOrZero;
            segmentParameters["exclusive_duration_millis"] = exclusiveTime.TotalMilliseconds;

            if (node.Unfinished)
                segmentParameters["unfinished"] = "This segment's duration is unknown because it did not complete before the end of the transaction";

            return new TransactionTraceSegment(relativeStartTime, relativeEndTime, name, segmentParameters, children, className, methodName);
        }

    }

    public class TransactionTraceWireModelComponents
    {
        public TimeSpan Duration { get; }

        public bool IsSynthetics { get; }

        public TransactionMetricName TransactionMetricName { get; }

        public delegate TransactionTraceWireModel GenerateWireModel();

        private readonly GenerateWireModel _generateWireModel;

        public TransactionTraceWireModelComponents(TransactionMetricName transactionMetricName, TimeSpan duration, bool isSynthetics, GenerateWireModel generateWireModel)
        {
            _generateWireModel = generateWireModel;
            Duration = duration;
            IsSynthetics = isSynthetics;
            TransactionMetricName = transactionMetricName;
        }

        /// <summary>
        /// Creates the wire model.  ITransactionCollectors should never call this method.  
        /// The TransactionTraceAggregator should be the only caller.
        /// </summary>
        internal TransactionTraceWireModel CreateWireModel()
        {
            return _generateWireModel();
        }
    }
}
