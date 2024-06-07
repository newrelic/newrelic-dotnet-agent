// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.SystemExtensions.Threading;
using NUnit.Framework;


namespace NewRelic.SystemExtensions.UnitTests.Threading
{
    public class ReaderWriterLockSlimExtensions
    {
        [Test]
        public void when_null_object_read_lock_then_ArgumentNullException()
        {
            // arrange
            ReaderWriterLockSlim slimLock = null;

            // act/assert
            Assert.Throws<ArgumentNullException>(() => slimLock.ReusableDisposableReadLock());
        }

        [Test]
        public void when_null_object_write_lock_then_ArgumentNullException()
        {
            // arrange
            ReaderWriterLockSlim slimLock = null;

            // act/assert
            Assert.Throws<ArgumentNullException>(() => slimLock.ReusableDisposableWriteLock());
        }

        [Test]
        public void disposing_releases_lock()
        {
            // arrange
            var timeout = TimeSpan.FromSeconds(1);
            var slimLock = new ReaderWriterLockSlim();
            var firstAcquire = new Task(() => slimLock.ReusableDisposableWriteLock()().Dispose());
            var secondAcquire = new Task(() => slimLock.ReusableDisposableWriteLock()().Dispose());

            // act
            firstAcquire.Start();
            var firstCompletedSuccessfully = firstAcquire.Wait(timeout);
            secondAcquire.Start();
            var secondCompletedSuccessfully = secondAcquire.Wait(timeout);

            Assert.Multiple(() =>
            {
                // assert
                Assert.That(firstCompletedSuccessfully, Is.True);
                Assert.That(secondCompletedSuccessfully, Is.True);
            });
        }

        [Test]
        public void undisposed_write_can_not_be_read()
        {
            // arrange
            var timeout = TimeSpan.FromMilliseconds(100);
            var slimLock = new ReaderWriterLockSlim();
            var innerAcquire = new Task(() => slimLock.ReusableDisposableReadLock()().Dispose());

            // act
            var outerDisposableLock = slimLock.ReusableDisposableWriteLock()();
            innerAcquire.Start();
            var innerCompletedSuccessfully = innerAcquire.Wait(timeout);
            outerDisposableLock.Dispose();

            // assert
            Assert.That(innerCompletedSuccessfully, Is.False);
        }

        [Test]
        public void undisposed_read_can_be_read()
        {
            // arrange
            var timeout = TimeSpan.FromMilliseconds(100);
            var slimLock = new ReaderWriterLockSlim();
            var innerAcquire = new Task(() => slimLock.ReusableDisposableReadLock()().Dispose());

            // act
            var outerDisposableLock = slimLock.ReusableDisposableReadLock()();
            innerAcquire.Start();
            var innerCompletedSuccessfully = innerAcquire.Wait(timeout);
            outerDisposableLock.Dispose();

            // assert
            Assert.That(innerCompletedSuccessfully, Is.True);
        }

        [Test]
        public void undisposed_read_can_not_be_written()
        {
            // arrange
            var timeout = TimeSpan.FromMilliseconds(100);
            var slimLock = new ReaderWriterLockSlim();
            var innerAcquire = new Task(() => slimLock.ReusableDisposableWriteLock()().Dispose());

            // act
            var outerDisposableLock = slimLock.ReusableDisposableReadLock()();
            innerAcquire.Start();
            var innerCompletedSuccessfully = innerAcquire.Wait(timeout);
            outerDisposableLock.Dispose();

            // assert
            Assert.That(innerCompletedSuccessfully, Is.False);
        }

    }
}
