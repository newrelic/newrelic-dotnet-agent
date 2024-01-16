// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Collections.UnitTests
{
    [TestFixture]
    public class Class_DisposableCollection
    {
        private class Disposable : IDisposable
        {
            public bool HasBeenDisposed;

            void IDisposable.Dispose()
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

        [Test]
        public void when_disposed_then_disposes_all_items()
        {
            // ACT
            _disposableCollection.Dispose();

            // ASSERT
            Assert.That(_disposable1.HasBeenDisposed);
            Assert.That(_disposable2.HasBeenDisposed);
        }

        [Test]
        public void when_disposed_then_clears_collection()
        {
            // ACT
            _disposableCollection.Dispose();

            // ASSERT
            ClassicAssert.AreEqual(0, _disposableCollection.Count);
        }

        [Test]
        public void when_null_is_added_then_collection_does_not_change()
        {
            var obj = new Class_DisposableCollection();

            // ACT
            _disposableCollection.Add(null);

            // ASSERT
            ClassicAssert.AreEqual(2, _disposableCollection.Count);
        }

        [Test]
        public void when_cleared_then_disposes_all_items()
        {
            // ACT
            _disposableCollection.Clear();

            // ASSERT
            Assert.That(_disposable1.HasBeenDisposed);
            Assert.That(_disposable2.HasBeenDisposed);
        }

        [Test]
        public void when_cleared_then_clears_collection()
        {
            // ACT
            _disposableCollection.Clear();

            // ASSERT
            ClassicAssert.AreEqual(0, _disposableCollection.Count);
        }

        [Test]
        public void when_remove_existing_item_then_disposes_item_being_removed()
        {
            // ACT
            _disposableCollection.Remove(_disposable1);

            // ASSERT
            Assert.That(_disposable1.HasBeenDisposed);
        }

        [Test]
        public void when_remove_existing_item_then_does_not_dispose_other_items()
        {
            var obj = new Class_DisposableCollection();

            // ACT
            _disposableCollection.Remove(_disposable1);

            // ASSERT
            ClassicAssert.False(_disposable2.HasBeenDisposed);
        }

    }
}
