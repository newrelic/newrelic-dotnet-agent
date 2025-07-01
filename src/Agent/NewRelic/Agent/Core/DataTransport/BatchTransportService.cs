// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Extensions.Collections;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface IBatchTransportService<TRequest> : IDisposable
    {
        bool IsServiceEnabled { get; }
        int BatchSizeConfigValue { get; }
        int BatchExportIntervalMilliseconds { get; }

        void Shutdown(bool withRestart);
        void StartConsumingCollection(PartitionedBlockingCollection<TRequest> collection);
        void Wait(int millisecondsTimeout = -1);
    }

    public abstract class BatchTransportService<TRequest> : IBatchTransportService<TRequest>
    {
        protected readonly IDataTransportService _dataTransportService;
        private PartitionedBlockingCollection<TRequest> _collection;
        private readonly AutoResetEvent _exportTrigger = new AutoResetEvent(false);
        private readonly ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
        private Thread _harvestThread;

        public abstract bool IsServiceEnabled { get; }
        public int BatchSizeConfigValue { get; private set; } = 2000; // Placeholder for actual implementation
        public int BatchExportIntervalMilliseconds { get; private set; } = 5000; // Placeholder for actual implementation

        public BatchTransportService(IDataTransportService dataTransportService)
        {
            _dataTransportService = dataTransportService;
        }

        public void Dispose()
        {
            // Placeholder for actual implementation
            _shutdownEvent.Set();
            _harvestThread?.Join();
            _exportTrigger.Dispose();
            _shutdownEvent.Dispose();
        }

        public void Shutdown(bool withRestart)
        {
            // Placeholder for actual implementation
            _shutdownEvent.Set();
            _harvestThread?.Join();
            _harvestThread = null;
        }

        public void StartConsumingCollection(PartitionedBlockingCollection<TRequest> collection)
        {
            _collection = collection;

            _shutdownEvent.Reset();

            _harvestThread = new Thread(RunHarvest)
            {
                IsBackground = true,
                Name = "HarvestThread-" + GetType()
            };
            _harvestThread.Start();
        }

        public void RequestHarvest()
        {
            _exportTrigger.Set();
        }
        public void Wait(int millisecondsTimeout)
        {
            Log.Debug("{0}: Waiting up to {1} milliseconds for workers to finish streaming data...", GetType(), millisecondsTimeout);

            var task = Task.Run(async () =>
            {
                // Wait until there are no spans to be sent. Performance ?????
                while (_collection?.Count > 0)
                {
                    await Task.Delay(100);
                }
            });

            if (task.Wait(TimeSpan.FromMilliseconds(millisecondsTimeout)))
            {
                Log.Debug("{0}: Finished streaming span data on exit.", GetType());
            }
            else
            {
                Log.Debug("{0}: Could not finish streaming span data on exit: {1} span events need to be sent.", GetType(), _collection?.Count);
            }
        }

        public void RunHarvest()
        {
            WaitHandle[] allExportTriggers = { _exportTrigger, _shutdownEvent };
            while (true)
            {
                WaitHandle.WaitAny(allExportTriggers, BatchExportIntervalMilliseconds);
                try
                {
                    // TODO: Determine if we need to respect sendDataOnExit if the shutdown event is set.
                    InternalHarvest();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error during span event harvest.");
                }
            }
        }

        private void InternalHarvest()
        {
            Log.Finest("{0} harvest starting.", GetType());

            var itemsInBatch = new List<TRequest>(BatchSizeConfigValue);
            for (int i = 0; i < BatchSizeConfigValue; i++)
            {
                if (_collection == null)
                {
                    Log.Finest("Nothing to harvest yet.");
                    return;
                }

                if (!_collection.TryTake(out var item))
                {
                    break;
                }

                itemsInBatch.Add(item);
            }

            var eventHarvestData = new EventHarvestData(itemsInBatch.Count, itemsInBatch.Count);

            // if we don't have any events to publish then don't
            var eventCount = itemsInBatch.Count;
            if (eventCount > 0)
            {
                var responseStatus = SendBatchToDataTransportService(eventHarvestData, itemsInBatch, null);
                HandleResponse(responseStatus, itemsInBatch);
            }

            Log.Finest("{0} harvest finished.", GetType());
        }

        protected abstract DataTransportResponseStatus SendBatchToDataTransportService(EventHarvestData eventHarvestData, IEnumerable<TRequest> batch, string transactionId);

        private void HandleResponse(DataTransportResponseStatus responseStatus, List<TRequest> items)
        {
            // TODO: Fix the event type logged here, it should be specific to the type of request being sent.
            switch (responseStatus)
            {
                case DataTransportResponseStatus.RequestSuccessful:
                    //_agentHealthReporter.ReportSpanEventsSent(items.Count);
                    break;
                case DataTransportResponseStatus.Retain:
                    RetainEvents(items);
                    Log.Debug("Retaining {count} span events.", items.Count);
                    break;
                case DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard:
                    ReduceBatchSize((int)(items.Count * 0.5));
                    RetainEvents(items);
                    Log.Debug("Reservoir size reduced. Retaining {count} span events.", items.Count);
                    break;
                case DataTransportResponseStatus.Discard:
                default:
                    Log.Debug("Discarding {count} span events.", items.Count);
                    break;
            }
        }

        private void ReduceBatchSize(int newSize)
        {
            BatchSizeConfigValue = Math.Max(newSize, 1);
        }

        private void RetainEvents(List<TRequest> items)
		{
			if (_collection == null)
			{
				Log.Finest("No collection available to retain events.");
				return;
			}

            var retainedItemCount = _collection.TryAdd(items);

			Log.Debug("Retained {count} events.", retainedItemCount);
		}
    }

    public class SpanBatchDataTransportService : BatchTransportService<ISpanEventWireModel>
    {
        public SpanBatchDataTransportService(IDataTransportService dataTransportService)
            : base(dataTransportService)
        {
        }

        // TODO: Check the appropriate configuration setting to determine if the service is enabled.
        public override bool IsServiceEnabled => false;

        protected override DataTransportResponseStatus SendBatchToDataTransportService(EventHarvestData eventHarvestData, IEnumerable<ISpanEventWireModel> batch, string transactionId)
        {
            return _dataTransportService.Send(eventHarvestData, batch, transactionId);
        }
    }
}
