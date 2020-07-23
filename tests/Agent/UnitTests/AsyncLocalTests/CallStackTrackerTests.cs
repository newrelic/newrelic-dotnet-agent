using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Providers.CallStack.AsyncLocal;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Providers.CallStack.AsyncLocalTests
{

    [TestFixture]
    public class CallStackTrackerTests
    {
        [NotNull]
        private ICallStackManager _tracker;

        [SetUp]
        public void SetUp()
        {
            _tracker = new SyncToAsyncCallStackManager(new CallContextStorage<int?>("mykey"));
            _tracker.AttachToAsync();
        }

        [TearDown]
        public void TestDown()
        {
            _tracker.Clear();
        }

        /*
		[Test]
		public void Push_AddsObjectsAcrossAppDomains()
		{
			var remoteAppDomain = AppDomain.CreateDomain("RemoteAppDomain");
			var stackInRemoteAppDomain = CreateImmutableStackInRemoteAppDomain(remoteAppDomain);
			var tracker = Mock.NonPublic.MakeStaticPrivateAccessor(typeof(CallContextStorage));
			tracker.SetProperty("CurrentCallStack", stackInRemoteAppDomain);

			Assert.DoesNotThrow(() => _tracker.Push(new Object()));
		}

		[Test]
		public void Pop_RemovesTopElementAcrossAppDomains()
		{
			var remoteAppDomain = AppDomain.CreateDomain("RemoteAppDomain");
			var stackInRemoteAppDomain = CreateImmutableStackInRemoteAppDomain(remoteAppDomain);
			var tracker = Mock.NonPublic.MakeStaticPrivateAccessor(typeof(CallContextStorage));
			tracker.SetProperty("CurrentCallStack", stackInRemoteAppDomain);

			Assert.DoesNotThrow(() =>
			{
				_tracker.Push(new Object());
				_tracker.Pop();
			});
		}
		*/

        [Test]
        public void Pop_RemovesTopElement()
        {
            _tracker.Push(666);
            Assert.IsTrue(_tracker.TryPeek().HasValue);
            _tracker.TryPop(666, null);

            Assert.IsFalse(_tracker.TryPeek().HasValue);
        }

        [Test]
        public void SwitchToAsync()
        {
            var tracker = new SyncToAsyncCallStackManager(new CallContextStorage<int?>("mykey"));
            tracker.Push(666);
            Assert.IsTrue(tracker.TryPeek().HasValue);

            tracker.AttachToAsync();

            Assert.AreEqual(666, tracker.TryPeek());
        }

        [Test]
        public void Peek_ReturnsEmpty_IfStackIsEmpty()
        {
            Assert.IsNull(_tracker.TryPeek());
        }

        /*

		[Test]
		public void TryPeek_ReturnsEmpty_IfStackIsEmpty_AnotherAppDomain()
		{
			var remoteAppDomain = AppDomain.CreateDomain("RemoteAppDomain");
			var stackInRemoteAppDomain = CreateImmutableStackInRemoteAppDomain(remoteAppDomain);
			Assert.NotNull(stackInRemoteAppDomain);

			Assert.IsNotNull(Environment.CurrentDirectory);

			var tracker = Mock.NonPublic.MakeStaticPrivateAccessor(typeof(CallContextStorage));
			tracker.SetProperty("CurrentCallStack", stackInRemoteAppDomain);

			var stackInCallStackTracker = tracker.GetProperty("CurrentCallStack") as MarshalByRefImmutableStack<Object>;
			var appDomainIdOfStack = GetAppDomainIdOfStack(stackInCallStackTracker);

			Assert.IsNull(_tracker.TryPeek());

			NrAssert.Multiple(
				// verifies the MarshalByRefImmutableStack was created in the remote app domain
				() => Assert.AreEqual(appDomainIdOfStack, remoteAppDomain.Id),
				// verifies the MarshalByRefImmutableStack's app domain is not the current app domain (that the two app domains are distinct)
				() => Assert.AreNotEqual(appDomainIdOfStack, AppDomain.CurrentDomain.Id)
			);
		}*/

        [Test]
        public void Peek_ReturnsPushedItem_IfOneItemIsPushed()
        {
            var id = 666;
            _tracker.Push(id);

            Assert.AreEqual(id, _tracker.TryPeek());
        }

        [Test]
        public void Peek_SuccessivelyReturnMostRecentPushedItem_IfMultipleItemsArePushedAndThenPopped()
        {
            var id1 = 1;
            var id2 = 2;
            _tracker.Push(id1);
            _tracker.Push(id2);

            Assert.AreEqual(id2, _tracker.TryPeek());
            _tracker.TryPop(id2, id1);
            Assert.AreEqual(id1, _tracker.TryPeek());
        }

        [Test]
        public void Peek_DoesNotModifyStack()
        {
            var id = 1;
            _tracker.Push(id);

            Assert.AreEqual(id, _tracker.TryPeek());
            Assert.AreEqual(id, _tracker.TryPeek());
        }

        [Test]
        public void ClearFrames_RemovesAllFrames()
        {
            _tracker.Push(1);
            _tracker.Push(2);
            _tracker.Push(3);
            _tracker.Clear();

            Assert.IsFalse(_tracker.TryPeek().HasValue);
        }

        [Test]
        public void AsyncLocalCallStackTracker_MaintainsDifferentStacks_PerThread()
        {
            _tracker.Push(1);
            var thread2Task = Task.Run(() =>
            {
                _tracker.Push(2);
                return _tracker.TryPeek();
            });
            thread2Task.Wait();

            var thread1Top = _tracker.TryPeek();
            var thread2Top = thread2Task.Result;

            NrAssert.Multiple(
                () => Assert.NotNull(thread1Top),
                () => Assert.NotNull(thread2Top),
                () => Assert.AreNotEqual(thread1Top, thread2Top)
                );
        }

        /*
		private MarshalByRefImmutableStack<Object> CreateImmutableStackInRemoteAppDomain(AppDomain remoteAppDomain)
		{
			var type = typeof(MarshalByRefImmutableStack<Object>);
			var assemblyLoc = typeof(MarshalByRefImmutableStack<Object>).Assembly.Location;

			return (MarshalByRefImmutableStack<Object>)remoteAppDomain.CreateInstanceFromAndUnwrap(assemblyLoc, type.FullName);
		}

		private int GetAppDomainIdOfStack(MarshalByRefImmutableStack<Object> stack)
		{
			var rp = RemotingServices.GetRealProxy(stack);
			return (int)rp.GetType().GetField("_domainID", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rp);
		}
		*/
    }

}
