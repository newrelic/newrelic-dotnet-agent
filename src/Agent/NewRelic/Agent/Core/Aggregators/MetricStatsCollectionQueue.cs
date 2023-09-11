// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.Aggregators
{
    /// <summary>
    /// Ported, with some tweaks, from the Java Agent.  Allows the agent to use multiple MetricStatsCollections to aggregate
    /// metrics between harvests, so that a lock on a single MetricStatsCollection does not become a throughput bottleneck at high load.
    /// At harvest time all the MetricStatsCollections are combined.
    /// </summary>
    public class MetricStatsCollectionQueue
    {
        private ConcurrentQueue<MetricStatsCollection> _statsCollectionQueue;

        internal MetricStatsCollectionQueue()
        {
            _statsCollectionQueue = new ConcurrentQueue<MetricStatsCollection>();
        }

        /// <summary>
        /// This MetricStatsCollectionQueue uses a ConcurrentQueue (allows multiple readers, but only one writer, at a time) to mediate
        /// between metrics that need to merge into one of the collections (readers), and the harvest job (writer), which replaces the 
        /// queue and merges the collections in the old queue to create the harvest payload.
        /// 
        /// </summary>
        /// <param name="metrics"></param>
        /// <returns></returns>
        public bool MergeMetrics(IAllMetricStatsCollection metrics)
        {
            var statsCollectionQueue = _statsCollectionQueue;
            if (statsCollectionQueue == null)
            {
                // We've already been harvested.  Caller should try again, at which point a whole new MetricStatsCollectionQueue
                // will have been created for the next harvest cycle.
                return false;
            }

            AddMetricsToCollection(statsCollectionQueue, metrics);
            return true;
        }

        /// <summary>
        /// Take a MetricStatsCollection off the queue and merge metric(s) into it, then put it back
        /// on the queue.  Create one if all the existing ones are "checked out" -- others can use it later.
        ///  
        /// We are one of (possibly) several readers of the MetricStatsCollection, so harvest will
        /// wait for us to finish before fetching/replacing the entire queue
        /// </summary>
        /// <param name="statsCollectionQueue"></param>
        /// <param name="metric"></param>
        private void AddMetricsToCollection(ConcurrentQueue<MetricStatsCollection> statsCollectionQueue, IAllMetricStatsCollection metrics)
        {
            MetricStatsCollection statsCollectionToMergeWith = null;
            try
            {
                if (!statsCollectionQueue.TryDequeue(out statsCollectionToMergeWith))
                {
                    statsCollectionToMergeWith = new MetricStatsCollection();
                }

                metrics.AddMetricsToCollection(statsCollectionToMergeWith);
            }
            catch (Exception e)
            {
                Log.Warn(e, "Exception dequeueing/creating stats collection");
            }
            finally
            {
                statsCollectionQueue.Enqueue(statsCollectionToMergeWith);
            }
        }

        /// <summary>
        /// Null out the ConcurrentQueue contained in this MetricStatsCollectionQueue so that any subsequent
        /// attempts to read from the queue before this MetricStatsCollectionQueue is replaced will fail.  Combine
        /// all the MetricStatsCollections in the old ConcurrentQueue according to the merge function, and return the result. 
        /// </summary>
        /// <returns></returns>
        public MetricStatsCollection GetStatsCollectionForHarvest()
        {
            var statsCollectionQueue = _statsCollectionQueue;

            // Clear the reference to the queue so that future calls to MergeMetric() get short-circuited.
            _statsCollectionQueue = null;

            var harvestMetricsStatsCollection = new MetricStatsCollection();
            foreach (var statsCollection in statsCollectionQueue)
            {
                harvestMetricsStatsCollection.Merge(statsCollection);
            }

            return harvestMetricsStatsCollection;
        }
    }
}
