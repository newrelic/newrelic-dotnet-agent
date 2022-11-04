// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework.Internal;
using Telerik.JustMock;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Helpers;

namespace NewRelic.Agent.Core.Attributes.Tests
{
    [TestFixture]
    public class LogContextDataFilterTests
    {
        private IConfigurationService _configurationService;
        private Dictionary<string, object> _unfilteredContextData =
            new Dictionary<string, object>()
            {
                { "key1", "value1" },
                { "key2", 1 }
            };

    [SetUp]
        public void SetUp()
        {
            _configurationService = Mock.Create<IConfigurationService>();
        }

        //        Inlcudes     Excludes     Expected
        [TestCase("",          "",          "key1,key2", TestName = "Empty include and exclude")]
        [TestCase("key1",      "",          "key1",      TestName = "Explicit include, empty exclude")]
        [TestCase("key1,key2", "key2",      "key1",      TestName = "Explicit include and exclude")]
        [TestCase("",          "key1",      "key2",      TestName = "Empty include, explicit exclude")]
        [TestCase("",          "key1,key2", "",          TestName = "Explicitly exclude all")]
        [TestCase("key*",      "",          "key1,key2", TestName = "Wildcard include, empty exclude")]
        [TestCase("",          "key*",      "",          TestName = "Empty include, wildcard exclude")]
        [TestCase("key1",      "key*",      "key1",      TestName = "More-specific explicit include overrides wildcard exclude")]
        [TestCase("key3",      "",          "",          TestName = "Explicit include of non-existent key")]
        [TestCase("",          "key3",      "key1,key2", TestName = "Explicit exclude of non-existent key")]
        [TestCase("Key1,Key2", "",          "",          TestName = "Includes are case-sensitive")]
        [TestCase("",          "Key1,Key2", "key1,key2", TestName = "Excludes are case-sensitive")]
        [TestCase("*",         "",          "key1,key2", TestName = "Include everything with wildcard")]
        [TestCase("",          "*",         "",          TestName = "Exclude everything with wildcard")]
        [TestCase("*",         "*",         "",          TestName = "Exclude wins over include")]
        public void FilterLogContextData(string includeList, string excludeList, string expectedAttributeNames)
        {
            Mock.Arrange(() => _configurationService.Configuration.ContextDataInclude)
                .Returns(includeList.Split(new[] { StringSeparators.CommaChar, ' ' }, StringSplitOptions.RemoveEmptyEntries));
            Mock.Arrange(() => _configurationService.Configuration.ContextDataExclude)
                .Returns(excludeList.Split(new[] { StringSeparators.CommaChar, ' ' }, StringSplitOptions.RemoveEmptyEntries));

            var filter = new LogContextDataFilter(_configurationService);
            var filteredData = filter.FilterLogContextData(_unfilteredContextData);

            Assert.AreEqual(expectedAttributeNames, filteredData == null ? "" : string.Join(",", filteredData.Keys.ToList()));
        }

        [TestCase("abc", true, "abc", false, 3)]
        [TestCase("ab*", true, "ab", true, 2)]
        [TestCase("creditCard", false, "creditCard", false, 10)]
        [TestCase("*", false, "", true, 0)]
        public void LogContextDataFilterRule(string inputRuleText, bool isInclude, string expectedRuleText, bool isWildcard, int specificity)
        {
            var rule = new LogContextDataFilterRule(inputRuleText, isInclude);
            Assert.AreEqual(isInclude, rule.Include);
            Assert.AreEqual(expectedRuleText, rule.Text);
            Assert.AreEqual(isWildcard, rule.IsWildCard);
            Assert.AreEqual(specificity, rule.Specificity);

        }

    }
}
