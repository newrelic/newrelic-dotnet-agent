// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilities
{
    public class Class_DisposableCollection
    {
        private class Disposable : IDisposable
        {
            public bool HasBeenDisposed = false;

            public void Dispose()
            {
                HasBeenDisposed = true;
            }
        }

        private Disposable _disposable1;
        private Disposable _disposable2;
        private DisposableCollection _disposableCollection;

        [SetUp]
        public void SetUp()
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
            // ACT
            _disposableCollection.Add(null);

            // ASSERT
            Assert.That(_disposableCollection, Has.Count.EqualTo(2));
        }

        [Test]
        public void when_enumerated_loops_over_all_items()
        {
            // ARRANGE
            var disposable1Seen = false;
            var disposable2Seen = false;

            // ACT
            foreach (var disposable in _disposableCollection)
            {
                if (disposable == _disposable1)
                    disposable1Seen = true;
                else if (disposable == _disposable2)
                    disposable2Seen = true;
            }

            Assert.Multiple(() =>
            {
                // ASSERT
                Assert.That(disposable1Seen, Is.True);
                Assert.That(disposable2Seen, Is.True);
            });
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
        public void when_contains_item_then_contains_returns_true()
        {
            // ACT
            var result = _disposableCollection.Contains(_disposable1);

            // ASSERT
            Assert.That(result, Is.True);
        }

        [Test]
        public void when_does_not_contain_item_then_contains_returns_false()
        {
            // ACT
            var result = _disposableCollection.Contains(new Disposable());

            // ASSERT
            Assert.That(result, Is.False);
        }

        [Test]
        public void when_contains_called_with_null_then_contains_returns_false()
        {
            // ACT
            var result = _disposableCollection.Contains(null);

            // ASSERT
            Assert.That(result, Is.False);
        }

        [Test]
        public void when_copy_to_then_copies_to()
        {
            // ARRANGE
            var array = new Disposable[10];
            var disposable1Found = false;
            var disposable2Found = false;

            // ACT
            _disposableCollection.CopyTo(array, 0);

            // ASSERT
            foreach (var copiedDisposable in array)
            {
                if (copiedDisposable == _disposable1)
                    disposable1Found = true;
                else if (copiedDisposable == _disposable2)
                    disposable2Found = true;
            }

            Assert.Multiple(() =>
            {
                Assert.That(disposable1Found, Is.True);
                Assert.That(disposable2Found, Is.True);
            });
        }

        [Test]
        public void IsReadOnly_returns_false()
        {
            // ASSERT
            Assert.That(_disposableCollection.IsReadOnly, Is.False);
        }

        [Test]
        public void when_remove_null_then_returns_false()
        {
            // ACT
            var result = _disposableCollection.Remove(null);

            // ASSERT
            Assert.That(result, Is.False);
        }

        [Test]
        public void when_remove_null_then_does_not_change_collection()
        {
            // ACT
            _disposableCollection.Remove(null);

            // ASSERT
            Assert.That(_disposableCollection, Has.Count.EqualTo(2));
        }

        [Test]
        public void when_remove_non_existant_item_then_returns_false()
        {
            // ACT
            var result = _disposableCollection.Remove(new Disposable());

            // ASSERT
            Assert.That(result, Is.False);
        }

        [Test]
        public void when_remove_non_existant_item_then_does_not_change_collection()
        {
            // ACT
            _disposableCollection.Remove(new Disposable());

            // ASSERT
            Assert.That(_disposableCollection, Has.Count.EqualTo(2));
        }

        [Test]
        public void when_remove_existing_item_then_returns_true()
        {
            // ACT
            var result = _disposableCollection.Remove(_disposable1);

            // ASSERT
            Assert.That(result, Is.True);
        }

        [Test]
        public void when_remove_existing_item_then_changes_collection()
        {
            // ACT
            _disposableCollection.Remove(_disposable1);

            // ASSERT
            Assert.That(_disposableCollection, Has.Count.EqualTo(1));
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
            // ACT
            _disposableCollection.Remove(_disposable1);

            // ASSERT
            Assert.That(_disposable2.HasBeenDisposed, Is.False);
        }

    }
}
