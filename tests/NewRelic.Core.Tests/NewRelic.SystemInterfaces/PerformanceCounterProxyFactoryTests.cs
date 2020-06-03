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

			//Act
			var performanceCounter = _factory.CreatePerformanceCounterProxy(GetTestCategoryName(), "mycounter");
			
			//Assert
			Assert.NotNull(performanceCounter);
			Assert.AreEqual(_expectedPerformanceCounter, performanceCounter);
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

			_factory.CreatePerformanceCounterProxy(newCatName1, "mycounter1");
			_factory.CreatePerformanceCounterProxy(newCatName1, "mycounter2");
			_factory.CreatePerformanceCounterProxy(newCatName2, "mycounter");

			Assert.AreEqual(2, _instanceNameLookupCount);
		}

		[Test]
		public void ShouldThrowExceptionIfInstanceNameIsNotFound()
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

			Assert.Throws<Exception>(() => _factory.CreatePerformanceCounterProxy(GetTestCategoryName(), "mycounter"));
		}

		[Test]
		public void ShouldThrowExceptionIfProcessPerformanceCounterCannotOpen()
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

			Assert.Throws<Exception>(() => _factory.CreatePerformanceCounterProxy(GetTestCategoryName(), "mycounter"));
		}

		[Test]
		public void ShouldThrowExceptionIfNoInstanceNamesMatchTheProcessName()
		{
			const string currentProcessName = "myprocess";
			const int currentProcessId = 1;
			var instanceNameToIdMap = new Dictionary<string, int>
			{
				{ "p1", currentProcessId }
			};

			GivenCurrentProcessHasThisNameAndId(currentProcessName, currentProcessId);
			GivenPerfCategoryHasTheseInstanceNames(instanceNameToIdMap.Keys.ToArray());
			GivenPerfCategoryHasTheseProcessPerformanceCounters(instanceNameToIdMap);

			Assert.Throws<Exception>(() => _factory.CreatePerformanceCounterProxy(GetTestCategoryName(), "mycounter"));
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
