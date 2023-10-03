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
            Assert.AreEqual("fo", Clamper.ClampLength("foo", 2));
        }

        [Test]
        public void when_ClampString_with_maxlength_larger_than_string_then_string_is_not_trimmed()
        {
            Assert.AreEqual("foo", Clamper.ClampLength("foo", 200));
        }

        [Test]
        public void when_ClampDictionary_with_maxlength_equal_to_dict_dict_is_unchanged()
        {
            var dict = new Dictionary<string, string> { { "foo", "bar" } };
            var ndict = Clamper.ClampLength(dict, 6);
            Assert.AreEqual(1, ndict.Count);
            Assert.AreEqual("bar", ndict["foo"]);
        }

        [Test]
        public void when_ClampDictionary_with_maxlength_greaterthan_dict_dict_is_unchanged()
        {
            var dict = new Dictionary<string, string> { { "foo", "bar" } };
            var ndict = Clamper.ClampLength(dict, 7);
            Assert.AreEqual(1, ndict.Count);
            Assert.AreEqual("bar", ndict["foo"]);
        }

        [Test]
        public void when_ClampDictionary_with_maxlength_lessthan_dict_dict_is_trimmed()
        {
            var dict = new Dictionary<string, string> { { "foo", "bar" } };
            var ndict = Clamper.ClampLength(dict, 5);
            Assert.AreEqual(0, ndict.Count);
        }

        [Test]
        public void when_input_dictionary_is_null_output_dictionary_is_null()
        {
            var actualResult = Clamper.ClampLength(null as IDictionary<string, string>, 0);
            Assert.AreEqual(null, actualResult);
        }

        [Test]
        public void when_ClampException_with_maxlength_greaterthan_exception_then_exception_is_unchanged_()
        {
            var inner = new Exception("Inner");
            var outer = new Exception("Outer", inner);
            Assert.AreEqual(outer, Clamper.ClampLength(outer, 100));
        }

    }
}
