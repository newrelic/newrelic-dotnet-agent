using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DependencyInjection;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.SystemExtensions;
using NewRelic.SystemExtensions.Collections.Generic;
using IEnumerableExtensions = NewRelic.SystemExtensions.Collections.Generic.IEnumerableExtensions;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
    public interface ITransactionTraceMaker
    {
        [NotNull]
        TransactionTraceWireModel GetTransactionTrace([NotNull] ImmutableTransaction immutableTransaction, IEnumerable<ImmutableSegmentTreeNode> segmentTrees, TransactionMetricName transactionMetricName, Attributes attributes);
    }

    public class TransactionTraceMaker : ITransactionTraceMaker
    {
        [NotNull]
        private readonly IAttributeService _attributeService;

        [NotNull]
        private readonly IConfigurationService _configurationService;

        public TransactionTraceMaker([NotNull] IAttributeService attributeService, [NotNull] IConfigurationService configurationService)
        {
            _attributeService = attributeService;
            _configurationService = configurationService;
        }

        public TransactionTraceWireModel GetTransactionTrace(ImmutableTransaction immutableTransaction, IEnumerable<ImmutableSegmentTreeNode> segmentTrees, TransactionMetricName transactionMetricName, Attributes attributes)
        {
            segmentTrees = segmentTrees.ToList();

            if (!segmentTrees.Any())
                throw new ArgumentException("There must be at least one segment to create a trace");

            var filteredAttributes = _attributeService.FilterAttributes(attributes, AttributeDestinations.TransactionTrace);

            // See spec for details on these fields: https://source.datanerd.us/agents/agent-specs/blob/master/Transaction-Trace-LEGACY.md
            var startTime = immutableTransaction.StartTime;
            var duration = immutableTransaction.Duration;
            var uri = immutableTransaction.TransactionMetadata.Uri?.TrimAfter("?") ?? "/Unknown";
            var guid = immutableTransaction.Guid;
            var xraySessionId = null as UInt64?; // The .NET agent does not support xray sessions

            var isSynthetics = immutableTransaction.TransactionMetadata.IsSynthetics;
            var syntheticsResourceId = immutableTransaction.TransactionMetadata.SyntheticsResourceId;
            var rootSegment = GetRootSegment(segmentTrees, immutableTransaction);
            var agentAttributes = filteredAttributes.GetAgentAttributesDictionary();
            var intrinsicAttributes = filteredAttributes.GetIntrinsicsDictionary();
            var userAttributes = filteredAttributes.GetUserAttributesDictionary();

            var traceData = new TransactionTraceData(startTime, rootSegment, agentAttributes, intrinsicAttributes, userAttributes);

            var trace = new TransactionTraceWireModel(startTime, duration, transactionMetricName.PrefixedName, uri, traceData, guid, xraySessionId, syntheticsResourceId, isSynthetics);

            return trace;
        }

        [NotNull]
        private TransactionTraceSegment GetRootSegment([NotNull] IEnumerable<ImmutableSegmentTreeNode> segmentTrees, [NotNull] ImmutableTransaction immutableTransaction)
        {
            var relativeStartTime = TimeSpan.Zero;
            var relativeEndTime = immutableTransaction.Duration;
            const String name = "ROOT";
            var segmentParameters = new Dictionary<String, Object>();

            // Due to a bug in the UI, we must insert a fake top-level segment to be the parent of all of the REAL top-level segments. The UI does not know how to handle multiple top-level segments, but inserting a single faux segment to be the parent is a reasonable workaround.
            var fauxTopLevelSegment = GetFauxTopLevelSegment(segmentTrees, immutableTransaction);
            var children = new[] { fauxTopLevelSegment };

            var firstSegmentClassName = fauxTopLevelSegment.ClassName;
            var firstSegmentMethodName = fauxTopLevelSegment.MethodName;

            return new TransactionTraceSegment(relativeStartTime, relativeEndTime, name, segmentParameters, children, firstSegmentClassName, firstSegmentMethodName);
        }

        [NotNull]
        private TransactionTraceSegment GetFauxTopLevelSegment([NotNull] IEnumerable<ImmutableSegmentTreeNode> segmentTrees, [NotNull] ImmutableTransaction immutableTransaction)
        {
            var relativeStartTime = TimeSpan.Zero;
            var relativeEndTime = immutableTransaction.Duration;
            const String name = "Transaction";
            var segmentParameters = new Dictionary<String, Object>();
            var children = segmentTrees.Select(childNode => CreateTransactionTraceSegment(childNode, immutableTransaction)).ToList();
            var firstSegmentClassName = children.First().ClassName;
            var firstSegmentMethodName = children.First().MethodName;

            return new TransactionTraceSegment(relativeStartTime, relativeEndTime, name, segmentParameters, children, firstSegmentClassName, firstSegmentMethodName);
        }

        [NotNull]
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

        public Boolean IsSynthetics { get; }

        public TransactionMetricName TransactionMetricName { get; }

        public delegate TransactionTraceWireModel GenerateWireModel();

        private readonly GenerateWireModel _generateWireModel;

        public TransactionTraceWireModelComponents(TransactionMetricName transactionMetricName, TimeSpan duration, Boolean isSynthetics, GenerateWireModel generateWireModel)
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
