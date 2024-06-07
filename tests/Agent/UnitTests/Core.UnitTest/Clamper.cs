// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Utilities;
using NUnit.Framework;

namespace NewRelic.Agent.Core
{
    [TestFixture]
    class Class_Clamper
    {
        [Test]
        public void when_ClampString_then_string_is_trimmed()
        {
            Assert.That(Clamper.ClampLength("foo", 2), Is.EqualTo("fo"));
        }

        [Test]
        public void when_ClampString_with_maxlength_larger_than_string_then_string_is_not_trimmed()
        {
            Assert.That(Clamper.ClampLength("foo", 200), Is.EqualTo("foo"));
        }

        [Test]
        public void when_ClampDictionary_with_maxlength_equal_to_dict_dict_is_unchanged()
        {
            var dict = new Dictionary<string, string> { { "foo", "bar" } };
            var ndict = Clamper.ClampLength(dict, 6);
            Assert.That(ndict, Has.Count.EqualTo(1));
            Assert.That(ndict["foo"], Is.EqualTo("bar"));
        }

        [Test]
        public void when_ClampDictionary_with_maxlength_greaterthan_dict_dict_is_unchanged()
        {
            var dict = new Dictionary<string, string> { { "foo", "bar" } };
            var ndict = Clamper.ClampLength(dict, 7);
            Assert.That(ndict, Has.Count.EqualTo(1));
            Assert.That(ndict["foo"], Is.EqualTo("bar"));
        }

        [Test]
        public void when_ClampDictionary_with_maxlength_lessthan_dict_dict_is_trimmed()
        {
            var dict = new Dictionary<string, string> { { "foo", "bar" } };
            var ndict = Clamper.ClampLength(dict, 5);
            Assert.That(ndict, Is.Empty);
        }

        [Test]
        public void when_input_dictionary_is_null_output_dictionary_is_null()
        {
            var actualResult = Clamper.ClampLength(null as IDictionary<string, string>, 0);
            Assert.That(actualResult, Is.EqualTo(null));
        }

        [Test]
        public void when_ClampException_with_maxlength_greaterthan_exception_then_exception_is_unchanged_()
        {
            var inner = new Exception("Inner");
            var outer = new Exception("Outer", inner);
            Assert.That(Clamper.ClampLength(outer, 100), Is.EqualTo(outer));
        }

    }
}
