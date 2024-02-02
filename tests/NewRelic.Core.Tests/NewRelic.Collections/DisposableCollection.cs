// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;


namespace NewRelic.Collections.UnitTests
{
    [TestFixture]
    public class Class_DisposableCollection
    {
        private class Disposable : IDisposable
        {
            public bool HasBeenDisposed;

            public void Dispose()
            {
                HasBeenDisposed = true;
            }
        }

        private Disposable _disposable1;
        private Disposable _disposable2;
        private DisposableCollection _disposableCollection;

        public Class_DisposableCollection()
        {
            _disposable1 = new Disposable();
            _disposable2 = new Disposable();
            _disposableCollection = new DisposableCollection
            {
                _disposable1,
                _disposable2,
            };
        }

        [SetUp]
        public void Setup()
        {
            _disposable1 = new Disposable();
            _disposable2 = new Disposable();
            _disposableCollection = new DisposableCollection
            {
                _disposable1,
                _disposable2,
            };
        }

        [TearDown]
        [OneTimeTearDown]
        public void TearDown()
        {
            _disposable1.Dispose();
            _disposable2.Dispose();
            _disposableCollection.Dispose();
        }

        [Test]
        public void when_disposed_then_disposes_all_items()
        {
            // ACT
            _disposableCollection.Dispose();

            Assert.Multiple(() =>
            {
                // ASSERT
                Assert.That(_disposable1.HasBeenDisposed, Is.True);
                Assert.That(_disposable2.HasBeenDisposed, Is.True);
            });
        }

        [Test]
        public void when_disposed_then_clears_collection()
        {
            // ACT
            _disposableCollection.Dispose();

            // ASSERT
            Assert.That(_disposableCollection, Is.Empty);
        }

        [Test]
        public void when_null_is_added_then_collection_does_not_change()
        {
            var obj = new Class_DisposableCollection();

            // ACT
            _disposableCollection.Add(null);

            // ASSERT
            Assert.That(_disposableCollection, Has.Count.EqualTo(2));
        }

        [Test]
        public void when_cleared_then_disposes_all_items()
        {
            // ACT
            _disposableCollection.Clear();

            Assert.Multiple(() =>
            {
                // ASSERT
                Assert.That(_disposable1.HasBeenDisposed, Is.True);
                Assert.That(_disposable2.HasBeenDisposed, Is.True);
            });
        }

        [Test]
        public void when_cleared_then_clears_collection()
        {
            // ACT
            _disposableCollection.Clear();

            // ASSERT
            Assert.That(_disposableCollection, Is.Empty);
        }

        [Test]
        public void when_remove_existing_item_then_disposes_item_being_removed()
        {
            // ACT
            _disposableCollection.Remove(_disposable1);

            // ASSERT
            Assert.That(_disposable1.HasBeenDisposed, Is.True);
        }

        [Test]
        public void when_remove_existing_item_then_does_not_dispose_other_items()
        {
            var obj = new Class_DisposableCollection();

            // ACT
            _disposableCollection.Remove(_disposable1);

            // ASSERT
            Assert.That(_disposable2.HasBeenDisposed, Is.False);
        }

    }
}
