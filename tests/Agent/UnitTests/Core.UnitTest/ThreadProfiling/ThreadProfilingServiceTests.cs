// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NewRelic.Agent.Core.DataTransport;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    [TestFixture]
    public class ThreadProfilingServiceTests
    {
        private IDataTransportService _dataTransportService;
        private INativeMethods _nativeMethods;
        private ThreadProfilingService _threadProfilingService;

        [SetUp]
        public void SetUp()
        {
            _dataTransportService = Mock.Create<IDataTransportService>();
            _nativeMethods = Mock.Create<INativeMethods>();
            _threadProfilingService = new ThreadProfilingService(_dataTransportService, _nativeMethods);
        }

        [TearDown]
        public void TearDown()
        {
            _threadProfilingService.Dispose();
        }

        [Test]
        public void StartThreadProfilingSession_StartsNewSession_ReturnsTrue()
        {
            var profileSessionId = 1;
            uint frequencyInMsec = 100;
            uint durationInMsec = 1000;

            var result = _threadProfilingService.StartThreadProfilingSession(profileSessionId, frequencyInMsec, durationInMsec);

            Assert.That(result, Is.True);
        }

        [Test]
        public void StopThreadProfilingSession_StopsSession_ReturnsTrue()
        {
            var profileSessionId = 1;
            uint frequencyInMsec = 100;
            uint durationInMsec = 1000;

            _threadProfilingService.StartThreadProfilingSession(profileSessionId, frequencyInMsec, durationInMsec);
            var result = _threadProfilingService.StopThreadProfilingSession(profileSessionId);

            Assert.That(result, Is.True);
        }

        [Test]
        public void StopThreadProfilingSession_WhenNotStarted_ReturnsFalse()
        {
            var result = _threadProfilingService.StopThreadProfilingSession(9999);

            Assert.That(result, Is.False);
        }


        [Test]
        public void StopThreadProfilingSession_AfterStarted_InvalidSessionId_ReturnsFalse()
        {
            var profileSessionId = 1;
            uint frequencyInMsec = 100;
            uint durationInMsec = 1000;

            _threadProfilingService.StartThreadProfilingSession(profileSessionId, frequencyInMsec, durationInMsec);

            var bogusProfileSessionId = 9999;
            var result = _threadProfilingService.StopThreadProfilingSession(bogusProfileSessionId);

            Assert.That(result, Is.False);
        }

        [Test]
        public void SampleAcquired_AllErrorCode0_UpdatesTreeWithThreadSnapshots()
        {
            // Arrange
            var threadSnapshots = new[]
            {
                new ThreadSnapshot { ThreadId = (UIntPtr)1, ErrorCode = 0, FunctionIDs = new[] { (UIntPtr)1, (UIntPtr)2 } },
                new ThreadSnapshot { ThreadId = (UIntPtr)2, ErrorCode = 0, FunctionIDs = new[] { (UIntPtr)3 } }
            };

            var expectedBucketNodeCount = threadSnapshots.Sum(ts => ts.FunctionIDs.Length);

            // Act
            _threadProfilingService.SampleAcquired(threadSnapshots);

            // Assert
            Assert.That(_threadProfilingService.GetTotalBucketNodeCount(), Is.EqualTo(expectedBucketNodeCount));
        }

        [Test]
        public void SampleAcquired_NonZeroErrorCode_DoesNotUpdateTreeWithThreadSnapshots()
        {
            // Arrange
            var threadSnapshots = new[]
            {
                new ThreadSnapshot { ThreadId = (UIntPtr)1, ErrorCode = 1, FunctionIDs = new[] { (UIntPtr)1, (UIntPtr)2 } },
                new ThreadSnapshot { ThreadId = (UIntPtr)2, ErrorCode = 2, FunctionIDs = new[] { (UIntPtr)3 } },
                new ThreadSnapshot { ThreadId = (UIntPtr)2, ErrorCode = 2, FunctionIDs = new[] { (UIntPtr)3 } } // duplicate to exercise a code path in AddFailedThreadProfile()
            };

            // Act
            _threadProfilingService.SampleAcquired(threadSnapshots);

            // Assert
            Assert.That(_threadProfilingService.GetTotalBucketNodeCount(), Is.EqualTo(0));
        }

        [Test]
        public void FullCycleTest_IsSuccessful()
        {

            // Arrange
            var typeOfFidTypeMethodName = typeof(FidTypeMethodName);
            var sizeOfFidTypeMethodName = Marshal.SizeOf(typeOfFidTypeMethodName);
            var fidGizmo = new FidTypeMethodName() { FunctionID = UIntPtr.Zero, MethodName = "SomeMethod", TypeName = "SomeType" };
            IntPtr fidGizmoIntPtr = Marshal.AllocHGlobal(Marshal.SizeOf(fidGizmo) * 3);
            Marshal.StructureToPtr(fidGizmo, fidGizmoIntPtr, false);
            Marshal.StructureToPtr(fidGizmo, fidGizmoIntPtr + sizeOfFidTypeMethodName, false);
            Marshal.StructureToPtr(fidGizmo, fidGizmoIntPtr + sizeOfFidTypeMethodName * 2, false);

            Mock.Arrange(() =>
                    _nativeMethods.RequestFunctionNames(Arg.IsAny<UIntPtr[]>(), Arg.AnyInt, out fidGizmoIntPtr))
                .Returns(0);

            var actualModels = new List<ThreadProfilingModel>();
            Mock.Arrange(() =>
                    _dataTransportService.SendThreadProfilingData(Arg.IsAny<IEnumerable<ThreadProfilingModel>>()))
                .DoInstead((IEnumerable<ThreadProfilingModel> models) =>
                {
                    actualModels.AddRange(models);
                });

            var threadSnapshots = new[]
            {
                new ThreadSnapshot { ThreadId = (UIntPtr)1, ErrorCode = 0, FunctionIDs = new[] { (UIntPtr)1, (UIntPtr)2 } },
                new ThreadSnapshot { ThreadId = (UIntPtr)2, ErrorCode = 0, FunctionIDs = new[] { (UIntPtr)3 } }
            };

            // Act
            _threadProfilingService.Start();
            _threadProfilingService.StartThreadProfilingSession(1, 60000, 120000);
            _threadProfilingService.SampleAcquired(threadSnapshots);
            _threadProfilingService.SamplingComplete();
            _threadProfilingService.Stop();

            // Assert
            Mock.Assert(() => _dataTransportService.SendThreadProfilingData(Arg.IsAny<IEnumerable<ThreadProfilingModel>>()), Occurs.Once());
            Assert.That(actualModels, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(actualModels[0].TotalThreadCount, Is.EqualTo(2));
                Assert.That(actualModels[0].NumberOfSamples, Is.EqualTo(1));
                Assert.That((actualModels[0].Samples["OTHER"] as ProfileNodes), Is.Empty);
            });

            // Teardown
            Marshal.FreeHGlobal(fidGizmoIntPtr);
        }

        [Test]
        public void PerformAggregation_HandlesException()
        {
            Mock.Arrange(() => _dataTransportService.SendThreadProfilingData(Arg.IsAny<IEnumerable<ThreadProfilingModel>>()))
                .Throws(new Exception("Test Exception", new Exception("Test Inner Exception")));

            try
            {
                _threadProfilingService.PerformAggregation();
            }
            catch
            {
                Assert.Fail("Exception was not handled");
            }
        }


        [Test]
        public void AddNodeToPruningList_AddsNodeToPruningList()
        {
            // Arrange
            var node = new ProfileNode((UIntPtr)1, 0, 0);

            // Act
            _threadProfilingService.AddNodeToPruningList(node);

            // Assert
            var actualCount = _threadProfilingService.PruningList.Count;
            Assert.Multiple(() =>
            {
                Assert.That(actualCount, Is.EqualTo(1));
                Assert.That(_threadProfilingService.PruningList[0], Is.EqualTo(node));
            });
        }

        [Test]
        public void SortPruningTree_DoesNothing_WhenMaxAggregatedNodesIsNotExceeded()
        {
            // Arrange
            var node1 = new ProfileNode((UIntPtr)1, 5, 1);
            var node2 = new ProfileNode((UIntPtr)2, 10, 0);
            var node3 = new ProfileNode((UIntPtr)3, 5, 0);

            _threadProfilingService.AddNodeToPruningList(node1);
            _threadProfilingService.AddNodeToPruningList(node2);
            _threadProfilingService.AddNodeToPruningList(node3);

            // Act
            _threadProfilingService.SortPruningTree();

            // Assert
            var pruningList = _threadProfilingService.PruningList;
            Assert.That(pruningList, Has.Count.EqualTo(3));
            Assert.Multiple(() =>
            {
                Assert.That(pruningList[0], Is.EqualTo(node1));
                Assert.That(pruningList[1], Is.EqualTo(node2));
                Assert.That(pruningList[2], Is.EqualTo(node3));
            });
        }

        [Test]
        public void SortPruningTree_SortsPruningListBasedOnRunnableCountAndDepth()
        {
            // Arrange
            var node1 = new ProfileNode((UIntPtr)1, 5, 1);
            var node2 = new ProfileNode((UIntPtr)2, 10, 0);
            var node3 = new ProfileNode((UIntPtr)3, 5, 0);

            var threadProfilingService = new ThreadProfilingService(_dataTransportService, _nativeMethods, 1);

            threadProfilingService.AddNodeToPruningList(node1);
            threadProfilingService.AddNodeToPruningList(node2);
            threadProfilingService.AddNodeToPruningList(node3);

            // Act
            threadProfilingService.SortPruningTree();

            // Assert
            var pruningList = threadProfilingService.PruningList;
            Assert.That(pruningList, Has.Count.EqualTo(3));
            Assert.Multiple(() =>
            {
                Assert.That(pruningList[0], Is.EqualTo(node2));
                Assert.That(pruningList[1], Is.EqualTo(node3));
                Assert.That(pruningList[2], Is.EqualTo(node1));
            });
        }

        [Test]
        public void ResetCache_ClearsPruningList()
        {
            // Arrange
            var node1 = new ProfileNode((UIntPtr)1, 5, 1);
            var node2 = new ProfileNode((UIntPtr)2, 10, 0);
            var node3 = new ProfileNode((UIntPtr)3, 5, 0);

            _threadProfilingService.AddNodeToPruningList(node1);
            _threadProfilingService.AddNodeToPruningList(node2);
            _threadProfilingService.AddNodeToPruningList(node3);

            // Act
            _threadProfilingService.ResetCache();

            // Assert
            Assert.That(_threadProfilingService.PruningList, Is.Empty);
        }
    }
}
