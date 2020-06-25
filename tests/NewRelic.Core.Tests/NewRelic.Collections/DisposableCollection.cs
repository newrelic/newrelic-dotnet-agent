/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
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
            Assert.True(_disposable1.HasBeenDisposed);
            Assert.True(_disposable2.HasBeenDisposed);
        }

        [Test]
        public void when_disposed_then_clears_collection()
        {
            // ACT
            _disposableCollection.Dispose();

            // ASSERT
            Assert.AreEqual(0, _disposableCollection.Count);
        }

        [Test]
        public void when_null_is_added_then_collection_does_not_change()
        {
            var obj = new Class_DisposableCollection();

            // ACT
            _disposableCollection.Add(null);

            // ASSERT
            Assert.AreEqual(2, _disposableCollection.Count);
        }

        [Test]
        public void when_cleared_then_disposes_all_items()
        {
            // ACT
            _disposableCollection.Clear();

            // ASSERT
            Assert.True(_disposable1.HasBeenDisposed);
            Assert.True(_disposable2.HasBeenDisposed);
        }

        [Test]
        public void when_cleared_then_clears_collection()
        {
            // ACT
            _disposableCollection.Clear();

            // ASSERT
            Assert.AreEqual(0, _disposableCollection.Count);
        }

        [Test]
        public void when_remove_existing_item_then_disposes_item_being_removed()
        {
            // ACT
            _disposableCollection.Remove(_disposable1);

            // ASSERT
            Assert.True(_disposable1.HasBeenDisposed);
        }

        [Test]
        public void when_remove_existing_item_then_does_not_dispose_other_items()
        {
            var obj = new Class_DisposableCollection();

            // ACT
            _disposableCollection.Remove(_disposable1);

            // ASSERT
            Assert.False(_disposable2.HasBeenDisposed);
        }

    }
}
