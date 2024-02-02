// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;

namespace NewRelic.Agent.Core.Samplers
{
    [TestFixture]
    public class GcEventListenerNetCoreTests
    {

        [SetUp]
        public void SetUp()
        {
            GCEventsListener.EventSourceNameToMonitor = TestGCEventSource.EventSourceName;
        }


        [TearDown]
        public void TearDown()
        {
            GCEventsListener.EventSourceNameToMonitor = GCEventsListener.DotNetEventSourceName;
        }

        [Test]
        public void GCHeapStatsEventIsHandledCorrectly()
        {
            var expectedValues = new Dictionary<GCSampleType, float>()
            {
                { GCSampleType.Gen0Size, 1},
                { GCSampleType.Gen0Promoted,  2},
                { GCSampleType.Gen1Size,  3},
                { GCSampleType.Gen1Promoted,  4},
                { GCSampleType.Gen2Size,  5},
                { GCSampleType.Gen2Survived, 6 },
                { GCSampleType.LOHSize,  7},
                { GCSampleType.LOHSurvived,  8},
                { GCSampleType.InducedCount, 0 },
                { GCSampleType.HandlesCount, 100 },
                { GCSampleType.Gen0CollectionCount, 0 },
                { GCSampleType.Gen1CollectionCount, 0 },
                { GCSampleType.Gen2CollectionCount, 0 }
            };

            using (var listener = new GCEventsListener())
            using (var src = new TestGCEventSource())
            {
                src.GCHeapStats_V1(
                    (ulong)expectedValues[GCSampleType.Gen0Size],
                    (ulong)expectedValues[GCSampleType.Gen0Promoted],
                    (ulong)expectedValues[GCSampleType.Gen1Size],
                    (ulong)expectedValues[GCSampleType.Gen1Promoted],
                    (ulong)expectedValues[GCSampleType.Gen2Size],
                    (ulong)expectedValues[GCSampleType.Gen2Survived],
                    (ulong)expectedValues[GCSampleType.LOHSize],
                    (ulong)expectedValues[GCSampleType.LOHSurvived],
                    1000, 2000, 3000, 4000,
                    (uint)expectedValues[GCSampleType.HandlesCount],
                    1);

                var sampleValues = listener.Sample();

                Assert.That(sampleValues, Has.Count.EqualTo(expectedValues.Count));

                //Validate that each SampleType generated the expected MetricType
                foreach (var q in expectedValues)
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(sampleValues.ContainsKey(q.Key), Is.True, $"Missing value for {q.Key}");
                        Assert.That(sampleValues[q.Key], Is.EqualTo(q.Value), $"Mismatch on {q.Key}, expected {q.Value}, actual {q.Key}");
                    });
                }
            }
        }


        [Test]
        public void GCStartEventUpdatesInducedCountOnlyForSpecificReasons()
        {
            using (var listener = new GCEventsListener())
            using (var src = new TestGCEventSource())
            {
                //Send GCStartEvent for every reason.
                //Only two of the reasons should increment the counter
                for (uint i = 0; i <= 7; i++)
                {
                    src.GCStart_V1(1, 1, i, 1, 1);
                }

                var sampleValues = listener.Sample();

                Assert.That(sampleValues[GCSampleType.InducedCount], Is.EqualTo(2));
            }
        }

        [Test]
        public void GCStartEventUpdatesProperGenCollectionCount()
        {
            using (var listener = new GCEventsListener())
            using (var src = new TestGCEventSource())
            {
                //  Running Count
                //	G0	G1	G2
                //--------------
                src.GCStart_V1(1, 0, 1, 1, 1);      //	1	0	0	

                src.GCStart_V1(1, 1, 1, 1, 1);      //	2	1	0
                src.GCStart_V1(1, 1, 1, 1, 1);      //	3	2	0

                src.GCStart_V1(1, 2, 1, 1, 1);      //	4	3	1
                src.GCStart_V1(1, 2, 1, 1, 1);      //	5	4	2
                src.GCStart_V1(1, 2, 1, 1, 1);      //	6	5	3

                var sampleValues = listener.Sample();

                Assert.Multiple(() =>
                {
                    Assert.That(sampleValues[GCSampleType.Gen0CollectionCount], Is.EqualTo(6));
                    Assert.That(sampleValues[GCSampleType.Gen1CollectionCount], Is.EqualTo(5));
                    Assert.That(sampleValues[GCSampleType.Gen2CollectionCount], Is.EqualTo(3));
                });
            }
        }


        [Test]
        public void MultipleGCStatsEvents_StatsValuesAreLatestValues()
        {
            var expectedValues = new Dictionary<GCSampleType, float>()
            {
                { GCSampleType.Gen0Size, 1},
                { GCSampleType.Gen0Promoted,  2},
                { GCSampleType.Gen1Size,  3},
                { GCSampleType.Gen1Promoted,  4},
                { GCSampleType.Gen2Size,  5},
                { GCSampleType.Gen2Survived, 6 },
                { GCSampleType.LOHSize,  7},
                { GCSampleType.LOHSurvived,  8},
                { GCSampleType.HandlesCount, 9 },
                { GCSampleType.InducedCount, 0 },
                { GCSampleType.Gen0CollectionCount, 0 },
                { GCSampleType.Gen1CollectionCount, 0 },
                { GCSampleType.Gen2CollectionCount, 0 },
            };

            var expectedValues2 = expectedValues.ToDictionary(x => x.Key, x => x.Value * x.Value);

            using (var listener = new GCEventsListener())
            using (var src = new TestGCEventSource())
            {
                src.GCHeapStats_V1(
                    (ulong)expectedValues[GCSampleType.Gen0Size],
                    (ulong)expectedValues[GCSampleType.Gen0Promoted],
                    (ulong)expectedValues[GCSampleType.Gen1Size],
                    (ulong)expectedValues[GCSampleType.Gen1Promoted],
                    (ulong)expectedValues[GCSampleType.Gen2Size],
                    (ulong)expectedValues[GCSampleType.Gen2Survived],
                    (ulong)expectedValues[GCSampleType.LOHSize],
                    (ulong)expectedValues[GCSampleType.LOHSurvived], 100, 200, 300, 400,
                    (uint)expectedValues[GCSampleType.HandlesCount], 1);

                src.GCHeapStats_V1(
                    (ulong)expectedValues2[GCSampleType.Gen0Size],
                    (ulong)expectedValues2[GCSampleType.Gen0Promoted],
                    (ulong)expectedValues2[GCSampleType.Gen1Size],
                    (ulong)expectedValues2[GCSampleType.Gen1Promoted],
                    (ulong)expectedValues2[GCSampleType.Gen2Size],
                    (ulong)expectedValues2[GCSampleType.Gen2Survived],
                    (ulong)expectedValues2[GCSampleType.LOHSize],
                    (ulong)expectedValues2[GCSampleType.LOHSurvived], 100, 200, 300, 400,
                    (uint)expectedValues2[GCSampleType.HandlesCount], 1);

                var sampleValues = listener.Sample();

                Assert.That(sampleValues, Has.Count.EqualTo(expectedValues2.Count));

                //Validate that each SampleType generated the expected MetricType
                foreach (var q in expectedValues2)
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(sampleValues.ContainsKey(q.Key), Is.True, $"Missing value for {q.Key}");
                        Assert.That(sampleValues[q.Key], Is.EqualTo(q.Value), $"Mismatch on {q.Key}, expected {q.Value}, actual {q.Key}");
                    });
                }
            }
        }

        [Test]
        public void MultipleGCStartEvents_StatsValuesAreCumulative()
        {
            const uint reasonCDInduced1 = 1;
            const uint reasonCDInduced2 = 7;

            using (var listener = new GCEventsListener())
            using (var src = new TestGCEventSource())
            {
                src.GCStart_V1(1, 1, reasonCDInduced1, 1, 1);

                var sampleValues = listener.Sample();

                Assert.That(sampleValues[GCSampleType.InducedCount], Is.EqualTo(1));

                src.GCStart_V1(1, 1, reasonCDInduced1, 1, 1);
                src.GCStart_V1(1, 1, reasonCDInduced2, 1, 1);
                src.GCStart_V1(1, 1, reasonCDInduced1, 1, 1);
                src.GCStart_V1(1, 1, reasonCDInduced2, 1, 1);

                sampleValues = listener.Sample();

                Assert.That(sampleValues[GCSampleType.InducedCount], Is.EqualTo(4));
            }
        }


        [EventSource(Guid = "AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA", Name = "TestGCEventSource")]
        public class TestGCEventSource : EventSource
        {
            public static readonly Guid EventSourceID = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");
            public static readonly string EventSourceName = "TestGCEventSource";

            public const int GCStartReasonCode_SmallObjectHeapAlloc = 0x0;
            public const int GCStartReasonCode_Induced = 0x1;
            public const int GCStartReasonCode_LowMemory = 0x2;
            public const int GCStartReasonCode_Empty = 0x3;
            public const int GCStartReasonCode_LargeObjectHeapAlloc = 0x4;
            public const int GCStartReasonCode_OutOfSpace_SmallObjHeap = 0x5;
            public const int GCStartReasonCode_OutOfSpace_LargeObjHeap = 0x6;
            public const int GCStartReasonCode_InducedNotForcedBlocking = 0x7;

            [Event(GCEventsListener.EventID_GCStart, Level = EventLevel.Informational)]
            public void GCStart_V1(
                uint count,
                uint depth,
                uint reason,
                uint type,
                uint clrInstanceID)
            {
                WriteEvent(1, count, depth, reason, type, clrInstanceID);
            }

            [Event(GCEventsListener.EventID_GCHeapStats, Level = EventLevel.Informational)]
            public void GCHeapStats_V1(
                ulong generationSize0,
                ulong totalPromotedSize0,
                ulong generationSize1,
                ulong totalPromotedSize1,
                ulong generationSize2,
                ulong totalPromotedSize2,
                ulong generationSize3,
                ulong totalPromotedSize3,
                ulong finalizationPromotedSize,         //Ignored, but must be supplied for EventSourceToWorkProperly
                ulong finalizationPromotedCount,        //Ignored, but must be supplied for EventSourceToWorkProperly
                uint pinnedObjectCount,                 //Ignored, but must be supplied for EventSourceToWorkProperly
                uint sinkBlockCount,                    //Ignored, but must be supplied for EventSourceToWorkProperly
                uint gcHandleCount,
                ushort clrInstanceID)                   //Ignored, but must be supplied for EventSourceToWorkProperly
            {
                WriteEvent(4, generationSize0, totalPromotedSize0, generationSize1, totalPromotedSize1, generationSize2, totalPromotedSize2, generationSize3, totalPromotedSize3, finalizationPromotedSize, finalizationPromotedCount, pinnedObjectCount, sinkBlockCount, gcHandleCount, clrInstanceID);
            }
        }
    }
}






