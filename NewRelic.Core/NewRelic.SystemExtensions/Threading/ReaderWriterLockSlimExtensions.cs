using System;
using System.Threading;
using JetBrains.Annotations;

namespace NewRelic.SystemExtensions.Threading
{
	public static class ReaderWriterLockSlimExtensions
	{

		[NotNull]
		public static Func<IDisposable> ReusableDisposableReadLock([NotNull] this ReaderWriterLockSlim readerWriterLockSlim)
		{
			if (readerWriterLockSlim == null)
				throw new ArgumentNullException("readerWriterLockSlim");

			return ReusableDisposableLock(readerWriterLockSlim.EnterReadLock, readerWriterLockSlim.ExitReadLock);
		}

		[NotNull]
		public static Func<IDisposable> ReusableDisposableWriteLock([NotNull] this ReaderWriterLockSlim readerWriterLockSlim)
		{
			if (readerWriterLockSlim == null)
				throw new ArgumentNullException("readerWriterLockSlim");

			return ReusableDisposableLock(readerWriterLockSlim.EnterWriteLock, readerWriterLockSlim.ExitWriteLock);
		}

		[NotNull]
		private static Func<IDisposable> ReusableDisposableLock(
			[NotNull] Action acquire, [NotNull] Action release)
		{

			var disposable = new DisposeDelegate(release);
			return () =>
			{
				//prevent ThreadAbortException from occurring during lock acquisition
				try { }
				finally
				{
					acquire();
					//implement the same change introduced to c# 4.0 to capture if the lock was taken (without an exception)
					//if the next line executes, then the lock should be released when we call disposable.Dispose()
					disposable.LockWasTaken = true;
				}
				return disposable;
			};
		}

		private sealed class DisposeDelegate : IDisposable
		{
			[NotNull] private readonly Action _action;
			public bool LockWasTaken { private get; set; } = false;

			public DisposeDelegate(Action action)
			{
				_action = action;
			}

			/// <summary>
			/// CAVEAT: this Dispose method is designed to be called from within a finally block (explicit or using-generated)
			/// </summary>
			public void Dispose()
			{
				//try{}finally{_action();} is not required due to usage of this class being called from using-generated finally block.
				if (LockWasTaken)
				{
					_action();
				}
			}
		}
		
	}
}
