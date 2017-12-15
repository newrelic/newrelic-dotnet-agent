using System;
using NewRelic.Agent;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;

namespace Tests
{
	[TestFixture]
	public class LazyMapTests
	{
		[Test]
		public void simple_case()
		{
			// arrange
			var expectedWrapper = Mock.Create<IWrapper>();
			Mock.Arrange(() => expectedWrapper.CanWrap(Arg.IsAny<InstrumentedMethodInfo>())).Returns(new CanWrapResponse(true));

			// act
			var lazyMap = new LazyMap<MethodCall, IWrapper>(new[] { expectedWrapper }, (method, wrapper) => wrapper.CanWrap(Arg.IsAny<InstrumentedMethodInfo>()).CanWrap);
			var methodCall = GetObjectMethodCall("GetHashCode");
			var actualWrapper = lazyMap.Get(methodCall);

			// assert
			Assert.AreEqual(expectedWrapper, actualWrapper);
		}
		
		[Test]
		public void when_constructed_with_null_wrapper_then_returns_null()
		{
			// act
			var lazyMap = new LazyMap<MethodCall, IWrapper>(null, (method, wrapper) => wrapper.CanWrap(Arg.IsAny<InstrumentedMethodInfo>()).CanWrap);
			var methodCall = GetObjectMethodCall("GetHashCode");
			var actualWrapper = lazyMap.Get(methodCall);

			// assert
			Assert.Null(actualWrapper);
		}

		[Test]
		public void when_no_wrappers_present_then_returns_null()
		{
			// act
			var lazyMap = new LazyMap<MethodCall, IWrapper>(new IWrapper[] { }, (method, wrapper) => wrapper.CanWrap(Arg.IsAny<InstrumentedMethodInfo>()).CanWrap);
			var methodCall = GetObjectMethodCall("GetHashCode");
			var actualWrapper = lazyMap.Get(methodCall);

			// assert
			Assert.Null(actualWrapper);
		}

		[Test]
		public void when_no_matching_wrapper_present_then_returns_null()
		{
			// arrange
			var expectedWrapper = Mock.Create<IWrapper>();
			Mock.Arrange(() => expectedWrapper.CanWrap(Arg.IsAny<InstrumentedMethodInfo>())).Returns(new CanWrapResponse(false));

			// act
			var lazyMap = new LazyMap<MethodCall, IWrapper>(new[] { expectedWrapper }, (method, wrapper) => wrapper.CanWrap(Arg.IsAny<InstrumentedMethodInfo>()).CanWrap);
			var methodCall = GetObjectMethodCall("GetHashCode");
			var actualWrapper = lazyMap.Get(methodCall);

			// assert
			Assert.Null(actualWrapper);
		}

		[Test]
		public void when_same_method_is_looked_up_multiple_times_CanWrap_is_only_called_once()
		{
			// arrange
			var expectedWrapper = Mock.Create<IWrapper>();
			Mock.Arrange(() => expectedWrapper.CanWrap(Arg.IsAny<InstrumentedMethodInfo>())).Returns(new CanWrapResponse(true)).Occurs(1);

			// act
			var lazyMap = new LazyMap<MethodCall, IWrapper>(new[] { expectedWrapper }, (method, wrapper) => wrapper.CanWrap(Arg.IsAny<InstrumentedMethodInfo>()).CanWrap);
			var methodCall = GetObjectMethodCall("GetHashCode");
			lazyMap.Get(methodCall);
			var actualWrapper = lazyMap.Get(methodCall);

			// assert
			Assert.AreEqual(expectedWrapper, actualWrapper);
			Mock.Assert(expectedWrapper);
		}

		[Test]
		public void when_different_methods_are_looked_up_then_CanWrap_is_called_multiple_times()
		{
			// arrange
			var expectedWrapper = Mock.Create<IWrapper>();
			Mock.Arrange(() => expectedWrapper.CanWrap(Arg.IsAny<InstrumentedMethodInfo>())).Returns(new CanWrapResponse(true)).Occurs(2);

			// act
			var lazyMap = new LazyMap<MethodCall, IWrapper>(new[] { expectedWrapper }, (method, wrapper) => wrapper.CanWrap(Arg.IsAny<InstrumentedMethodInfo>()).CanWrap);
			var methodCall1 = GetObjectMethodCall("ToString");
			lazyMap.Get(methodCall1);
			var methodCall2 = GetObjectMethodCall("GetHashCode");
			var actualWrapper = lazyMap.Get(methodCall2);

			// assert
			Assert.AreEqual(expectedWrapper, actualWrapper);
			Mock.Assert(expectedWrapper);
		}

		private MethodCall GetObjectMethodCall(string methodName)
		{
			var method = new Method(typeof(Object), methodName, String.Empty);
			var methodCall = new MethodCall(method, this, new object[] { });

			return methodCall;
		}
	}
}
