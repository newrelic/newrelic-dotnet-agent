// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Telerik.JustMock;

namespace NewRelic.Core.Tests.NewRelic.SystemInterfaces
{
	[TestFixture]
	public class PerformanceCounterProxyFactoryTests
	{
		private int _instanceNameLookupCount;
		private IPerformanceCounterProxy _expectedPerformanceCounter;
		private Func<string, string, string, IPerformanceCounterProxy> _createProcessIdPerformanceCounter;
		private Func<string, string, string, IPerformanceCounterProxy> _createPerformanceCounter;
		private IPerformanceCounterCategoryProxy _performanceCounterCategory;
		private Func<string, IPerformanceCounterCategoryProxy> _createPerformanceCounterCategory;
		private IProcessStatic _processStatic;
		private IProcess _currentProcess;
		private PerformanceCounterProxyFactory _factory;

		[SetUp]
		public void SetUp()
		{
			_instanceNameLookupCount = 0;
			_expectedPerformanceCounter = Mock.Create<IPerformanceCounterProxy>();
			_createPerformanceCounter = (_, __, ___) => _expectedPerformanceCounter;

			_performanceCounterCategory = Mock.Create<IPerformanceCounterCategoryProxy>();
			_createPerformanceCounterCategory = _ => _performanceCounterCategory;

			_currentProcess = Mock.Create<IProcess>();
			_processStatic = Mock.Create<IProcessStatic>();
			Mock.Arrange(() => _processStatic.GetCurrentProcess()).Returns(_currentProcess);

			_factory = new PerformanceCounterProxyFactory(_processStatic, CreatePerformanceCounterCategory, CreatePerformanceCounter);
		}

        [TearDown]
        public void TearDown()
        {
            _expectedPerformanceCounter.Dispose();
        }

		[Test]
		public void ShouldCreatePerformanceCounter()
		{

			//Setup
			const string currentProcessName = "myprocess";
			const int currentProcessId = 42;
			var instanceNameToIdMap = new Dictionary<string, int>
			{
				{ "p1", 1 },
				{ "p2", 2 },
				{ currentProcessName, currentProcessId },
				{ "p3", 3 }
			};

			GivenCurrentProcessHasThisNameAndId(currentProcessName, currentProcessId);
			GivenPerfCategoryHasTheseInstanceNames(instanceNameToIdMap.Keys.ToArray());
			GivenPerfCategoryHasTheseProcessPerformanceCounters(instanceNameToIdMap);

            var testCategoryName = GetTestCategoryName();

			//Act
            var processInstanceName = _factory.GetCurrentProcessInstanceNameForCategory(testCategoryName, null);
			var performanceCounter = _factory.CreatePerformanceCounterProxy(testCategoryName, "mycounter", processInstanceName);

            //Assert
            Assert.That(performanceCounter, Is.Not.Null);
            Assert.That(performanceCounter, Is.EqualTo(_expectedPerformanceCounter));
		}

		[Test]
		public void ShouldCacheInstanceNameLookup()
		{
			const string currentProcessName = "myprocess";
			const int currentProcessId = 1;

            var instanceNameToIdMap = new Dictionary<string, int>
			{
				{ currentProcessName + "1", currentProcessId }
			};

			GivenCurrentProcessHasThisNameAndId(currentProcessName, currentProcessId);
			GivenPerfCategoryHasTheseInstanceNames(instanceNameToIdMap.Keys.ToArray());
			GivenPerfCategoryHasTheseProcessPerformanceCounters(instanceNameToIdMap);

			var newCatName1 = GetTestCategoryName();
			var newCatName2 = GetTestCategoryName();

            var processInstanceName1 = _factory.GetCurrentProcessInstanceNameForCategory(newCatName1, null);
            var processInstanceName2 = _factory.GetCurrentProcessInstanceNameForCategory(newCatName2, null);

            _factory.CreatePerformanceCounterProxy(newCatName1, "mycounter1", processInstanceName1);
			_factory.CreatePerformanceCounterProxy(newCatName1, "mycounter2", processInstanceName1);
			_factory.CreatePerformanceCounterProxy(newCatName2, "mycounter", processInstanceName2);

            Assert.That(_instanceNameLookupCount, Is.EqualTo(2));
		}

		[Test]
		public void ShouldThrowExceptionIfCreatePerfCounterWithEmptyInstanceName()
		{
			const string currentProcessName = "myprocess";
			const int currentProcessId = 1;
			var instanceNameToIdMap = new Dictionary<string, int>
			{
				{ currentProcessName + "1", 3 },
				{ currentProcessName + "2", 4 }
			};

			GivenCurrentProcessHasThisNameAndId(currentProcessName, currentProcessId);
			GivenPerfCategoryHasTheseInstanceNames(instanceNameToIdMap.Keys.ToArray());
			GivenPerfCategoryHasTheseProcessPerformanceCounters(instanceNameToIdMap);

            var testCategoryName = GetTestCategoryName();

            var processInstanceName = _factory.GetCurrentProcessInstanceNameForCategory(testCategoryName, null);

            Assert.Throws<ArgumentException>(() => _factory.CreatePerformanceCounterProxy(testCategoryName, "mycounter", processInstanceName));
		}

		[Test]
		public void ShouldReturnNullIfCannotObtainPerfCounterInstanceName()
		{
			const string currentProcessName = "myprocess";
			const int currentProcessId = 1;
			var instanceNameToIdMap = new Dictionary<string, int>
			{
				{ currentProcessName + "1", currentProcessId }
			};

			GivenCurrentProcessHasThisNameAndId(currentProcessName, currentProcessId);
			GivenPerfCategoryHasTheseInstanceNames(instanceNameToIdMap.Keys.ToArray());
			GivenPerfCategoryHasTheseProcessPerformanceCounters(instanceNameToIdMap);
			_createProcessIdPerformanceCounter = (_, __, ___) => throw new Exception("Cannot create process performance counter.");

            var testCategoryName = GetTestCategoryName();

            Assert.That(_factory.GetCurrentProcessInstanceNameForCategory(testCategoryName, null), Is.Null);
		}

        [Test]
        public void ShouldThrowIfObtainPerfCounterInstanceNameIsUnauthorized()
        {
            const string currentProcessName = "myprocess";
            const int currentProcessId = 1;
            var instanceNameToIdMap = new Dictionary<string, int>
            {
                { currentProcessName + "1", currentProcessId }
            };

            GivenCurrentProcessHasThisNameAndId(currentProcessName, currentProcessId);
            GivenPerfCategoryHasTheseInstanceNames(instanceNameToIdMap.Keys.ToArray());
            GivenPerfCategoryHasTheseProcessPerformanceCounters(instanceNameToIdMap);
            _createProcessIdPerformanceCounter = (_, __, ___) => throw new UnauthorizedAccessException("Cannot create process performance counter.");

            var testCategoryName = GetTestCategoryName();

            Assert.Throws<UnauthorizedAccessException>(()=>_factory.GetCurrentProcessInstanceNameForCategory(testCategoryName, null));

            //Assert.Throws<UnauthorizedAccessException>(()=>_factory.CreatePerformanceCounterProxy(testCategoryName, "mycounter", processInstanceName));
        }

        [Test]
        public void ShouldThrowIfObtainPerfCounterProxyIsUnauthorized()
        {
            const string currentProcessName = "myprocess";
            const int currentProcessId = 1;
            var instanceNameToIdMap = new Dictionary<string, int>
            {
                { currentProcessName + "1", currentProcessId }
            };

            GivenCurrentProcessHasThisNameAndId(currentProcessName, currentProcessId);
            GivenPerfCategoryHasTheseInstanceNames(instanceNameToIdMap.Keys.ToArray());
            GivenPerfCategoryHasTheseProcessPerformanceCounters(instanceNameToIdMap);
            _createPerformanceCounter = (_, __, ___) => throw new UnauthorizedAccessException("Test Unauthorized Access");

            var testCategoryName = GetTestCategoryName();

            var processInstanceName = _factory.GetCurrentProcessInstanceNameForCategory(testCategoryName, null);

            Assert.Throws<UnauthorizedAccessException>(()=>_factory.CreatePerformanceCounterProxy(testCategoryName, "mycounter", processInstanceName));
        }


        [Test]
		public void ShouldReturnNullIfNoInstanceNamesMatchTheProcessName()
		{
			const string currentProcessName = "myprocess";
			const int currentProcessId = 1;
			var instanceNameToIdMap = new Dictionary<string, int>
			{
				{ "p1", 2 }
			};

			GivenCurrentProcessHasThisNameAndId(currentProcessName, currentProcessId);
			GivenPerfCategoryHasTheseInstanceNames(instanceNameToIdMap.Keys.ToArray());
			GivenPerfCategoryHasTheseProcessPerformanceCounters(instanceNameToIdMap);

            var testCategoryName = GetTestCategoryName();

            Assert.That(_factory.GetCurrentProcessInstanceNameForCategory(testCategoryName, null), Is.Null);
		}

		private void GivenCurrentProcessHasThisNameAndId(string processName, int processId)
		{
			Mock.Arrange(() => _currentProcess.ProcessName).Returns(processName);
			Mock.Arrange(() => _currentProcess.Id).Returns(processId);
		}

		private void GivenPerfCategoryHasTheseInstanceNames(params string[] instanceNames)
		{
			Mock.Arrange(() => _performanceCounterCategory.GetInstanceNames())
				.Returns(() => {
						_instanceNameLookupCount++;
						return instanceNames;
					});
		}

		private void GivenPerfCategoryHasTheseProcessPerformanceCounters(Dictionary<string, int> instanceNameToIdMap)
		{
			_createProcessIdPerformanceCounter = (_, __, instanceName) =>
			{
				var processIdPerformanceCounter = Mock.Create<IPerformanceCounterProxy>();
				Mock.Arrange(() => processIdPerformanceCounter.NextValue())
					.Returns(() => instanceNameToIdMap[instanceName]);
				return processIdPerformanceCounter;
			};
		}

		private IPerformanceCounterCategoryProxy CreatePerformanceCounterCategory(string categoryName)
		{
			return _createPerformanceCounterCategory(categoryName);
		}

		private IPerformanceCounterProxy CreatePerformanceCounter(string categoryName, string counterName, string instanceName)
		{
			if (counterName == PerformanceCounterProxyFactory.ProcessIdCounterName)
			{
				return _createProcessIdPerformanceCounter(categoryName, counterName, instanceName);
			}

			return _createPerformanceCounter(categoryName, counterName, instanceName);
		}


		private static int _categoryID = 0;

		/// <summary>
		/// Generates unique category names so that we avoid reusing Category->Instance Name cached values.
		/// </summary>
		private static string GetTestCategoryName()
		{
			return $"MyCategory{_categoryID++}";
		}

	}
}
#endif
