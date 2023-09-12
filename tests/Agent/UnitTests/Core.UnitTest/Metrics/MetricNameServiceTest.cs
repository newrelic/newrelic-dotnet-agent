// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Metrics
{
    [TestFixture]
    class MetricNameServiceTest
    {
        private MetricNameService _metricNameService;
        private IConfiguration _configuration;
        private ConfigurationAutoResponder _configurationResponder;

        [SetUp]
        public void SetUp()
        {
            _configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _configuration.ConfigurationVersion).Returns(2);
            _configurationResponder = new ConfigurationAutoResponder(_configuration);
            _metricNameService = new MetricNameService();
        }

        [TearDown]
        public void TearDown()
        {
            _metricNameService.Dispose();
            _configurationResponder.Dispose();
        }

        #region NormalizeUrl

        [Test]
        [TestCase("/apple", "/APPLE", Description = "Simple rule")]
        [TestCase("/banana", "/banana", Description = "Rule that doesn't match")]
        [TestCase("/banana/pie", "/BANANA/*", Description = "Rule with regex that matches")]
        [TestCase("/apple/banana/pie", "/APPLE/BANANA/*", Description = "Two matching rules that don't conflict")]
        [TestCase("/banana/apple/pie", "/BANANA/*", Description = "Two matching rules that conflict -- evaluation order matters!")]
        public void NormalizeUrl_RenamesUrlAccordingToRules_IfRuleIsNotIgnore(string input, string expectedOutput)
        {
            Mock.Arrange(() => _configuration.UrlRegexRules).Returns(new List<RegexRule>
            {
                new RegexRule("/apple", "/APPLE", false, 1, false, false, false),
                new RegexRule("/banana/.*", "/BANANA/*", false, 2, false, false, false)
            });

            var actualOutput = _metricNameService.NormalizeUrl(input);

            Assert.AreEqual(expectedOutput, actualOutput);
        }

        [Test]
        [TestCase("/apple", true)]
        [TestCase("/banana", false)]
        public void NormalizeUrl_Throws_IfIgnoreRuleMatchesInput(string input, bool shouldThrow)
        {
            Mock.Arrange(() => _configuration.UrlRegexRules).Returns(new List<RegexRule>
            {
                new RegexRule("/apple", "ThisWillNeverHappen/apple", false, 10, false, false, false),
                new RegexRule("/apple", null, true, 10, false, false, false)
            });

            Action normalizeAction = () => _metricNameService.NormalizeUrl(input);

            if (shouldThrow)
                NrAssert.Throws<IgnoreTransactionException>(normalizeAction);
            else
                normalizeAction();
        }

        [Test]
        public void NormalizeUrl_StripsQueryStringParametersBeforeProcessingRules()
        {
            Mock.Arrange(() => _configuration.UrlRegexRules).Returns(new List<RegexRule>
            {
                new RegexRule("customer/get", "customer/put", false, 10, false, false, false)
            });

            var result = _metricNameService.NormalizeUrl("/customer/get?ssn=4356334443");

            Assert.AreEqual("/customer/put", result);
        }

        [TestCaseSource(typeof(MetricNameServiceTest), "CrossAgentRegexRuleTestCases")]
        public void NormalizeUrl_PassesAllCrossAgentUrlTests(RegexRuleTestCase testCase)
        {
            var regexRules = DefaultConfiguration.GetRegexRules(testCase.Rules);
            Mock.Arrange(() => _configuration.UrlRegexRules).Returns(regexRules);

            foreach (var test in testCase.Tests)
            {
                if (test == null)
                    continue;

                string actualOutput;
                try
                {
                    actualOutput = _metricNameService.NormalizeUrl(test.Input);
                }
                catch (IgnoreTransactionException)
                {
                    actualOutput = null;
                }

                Assert.AreEqual(test.Expected, actualOutput);
            }
        }

        #endregion NormalizeUrl

        #region TryGetApdex_t

        [Test]
        [TestCase("WebTransaction/Touchdown/Throw", 0.9)]
        [TestCase("WebTransaction/Touchdown/throw", 0.4)]
        public void TryGetApdex_t_ReturnsCorrectApdexValue_IfMatchIsFound(string input, double expectedOutput)
        {
            Mock.Arrange(() => _configuration.WebTransactionsApdex).Returns(new Dictionary<string, double>
            {
                {"WebTransaction/Touchdown/Throw", 0.9},
                {"WebTransaction/Touchdown/throw", 0.4}
            });

            var apdexResult = _metricNameService.TryGetApdex_t(input);

            Assert.NotNull(apdexResult);
            Assert.AreEqual(TimeSpan.FromSeconds(expectedOutput), apdexResult.Value);
        }

        [Test]
        [TestCase("WebTransaction/Touchdown/throw")]
        [TestCase("WebTransaction/TD/Run")]
        public void TryGetApdex_t_ReturnsNull_IfMatchIsNotFound(string input)
        {
            Mock.Arrange(() => _configuration.WebTransactionsApdex).Returns(new Dictionary<string, double>
            {
                {"WebTransaction/Touchdown/Throw", 0.9}
            });

            var apdexResult = _metricNameService.TryGetApdex_t(input);

            Assert.IsNull(apdexResult);
        }

        #endregion TryGetApdex_t

        #region RenameTransaction

        [Test]
        [TestCase("WebTransaction/apple", "WebTransaction/APPLE", Description = "Simple rule")]
        [TestCase("WebTransaction/banana", "WebTransaction/banana", Description = "Rule that doesn't match")]
        [TestCase("WebTransaction/banana/pie", "WebTransaction/BANANA/*", Description = "Rule with regex that matches")]
        [TestCase("WebTransaction/apple/banana/pie", "WebTransaction/APPLE/BANANA/*", Description = "Two matching rules that don't conflict")]
        [TestCase("WebTransaction/banana/apple/pie", "WebTransaction/BANANA/*", Description = "Two matching rules that conflict -- evaluation order matters!")]
        public void RenameTransaction_RenamesTransactionAccordingToRules_IfRuleIsNotIgnore(string originalName, string expectedOutput)
        {
            var originalMetricName = AsTransactionMetricName(originalName);

            Mock.Arrange(() => _configuration.TransactionNameRegexRules).Returns(new List<RegexRule>
            {
                new RegexRule("/apple", "/APPLE", false, 1, false, false, false),
                new RegexRule("/banana/.*", "/BANANA/*", false, 2, false, false, false)
            });

            var actualOutput = _metricNameService.RenameTransaction(originalMetricName);

            Assert.NotNull(actualOutput);
            Assert.AreEqual(expectedOutput, actualOutput.PrefixedName);
        }

        [Test]
        [TestCase("WebTransaction/apple", null)]
        [TestCase("WebTransaction/banana", "WebTransaction/banana")]
        public void RenameTransaction_ReturnsShouldIgnore_IfIgnoreRuleMatchesInput(string originalName, string expectedOutput)
        {
            var originalMetricName = AsTransactionMetricName(originalName);

            Mock.Arrange(() => _configuration.TransactionNameRegexRules).Returns(new List<RegexRule>
            {
                new RegexRule("/apple", "ThisWillNeverHappen/apple", false, 10, false, false, false),
                new RegexRule("/apple", null, true, 10, false, false, false)
            });

            var actualOutput = _metricNameService.RenameTransaction(originalMetricName);

            if (expectedOutput == null)
            {
                Assert.IsTrue(actualOutput.ShouldIgnore);
            }
            else
            {
                Assert.IsFalse(actualOutput.ShouldIgnore);
                Assert.AreEqual(expectedOutput, actualOutput.PrefixedName);
            }
        }

        [Test]
        public void RenameTransaction_ShouldNotRenameTransaction_IfRuleCreatesInvalidName()
        {
            // The result of this rule is an invalid name -- the renamed metric must still be a WebTransaction
            Mock.Arrange(() => _configuration.TransactionNameRegexRules).Returns(new List<RegexRule>
            {
                new RegexRule("WebTransaction/foo/bar", "*", false, 10, false, false, false)
            });

            var originalMetricName = new TransactionMetricName("WebTransaction", "foo/bar");
            var newName = _metricNameService.RenameTransaction(originalMetricName);

            Assert.NotNull(newName);
            Assert.AreEqual(originalMetricName.PrefixedName, newName.PrefixedName);
        }

        private void CallRenameTransactionAndAssert(string originalName, string expectedRename, bool isWebTransaction = true)
        {
            var originalMetricName = AsTransactionMetricName(originalName);
            var newMetricName = _metricNameService.RenameTransaction(originalMetricName);

            Assert.NotNull(newMetricName);
            Assert.AreEqual(expectedRename, newMetricName.PrefixedName);
        }

        private TransactionMetricName AsTransactionMetricName(string originalName)
        {
            var segments = originalName.Split(MetricNames.PathSeparatorChar);
            return new TransactionMetricName(segments[0], string.Join(MetricNames.PathSeparator, segments.Skip(1)));
        }

        [Test]
        public void RenameTransaction_AppliesRegexRulesBeforeWhitelistRules()
        {
            Mock.Arrange(() => _configuration.TransactionNameRegexRules).Returns(new List<RegexRule>
            {
                new RegexRule("/InvalidSegment", "/ValidSegment", false, 10, false, false, false)
            });
            Mock.Arrange(() => _configuration.TransactionNameWhitelistRules).Returns(new Dictionary<string, IEnumerable<string>>
            {
                {"WebTransaction/Uri", new[]{"ValidSegment"}}
            });

            var originalMetricName = new TransactionMetricName("WebTransaction", "Uri/InvalidSegment/OtherInvalidSegment");
            var newMetricName = _metricNameService.RenameTransaction(originalMetricName);

            // The URL rule should transform it into "WebTransaction/Uri/ValidSegment/OtherInvalidSegment",
            // then the transaction segment should transform it into "WebTransaction/Uri/ValidSegment/*"
            Assert.NotNull(newMetricName);
            Assert.AreEqual("WebTransaction/Uri/ValidSegment/*", newMetricName.PrefixedName);
        }

        [TestCaseSource(typeof(MetricNameServiceTest), "CrossAgentWhitelistRuleTestCases")]
        public void RenameTransaction_PassesAllCrossAgentTransactionSegmentTests(WhitelistRuleTestCase testCase)
        {
            if (testCase.TestName == "transaction_name_with_single_segment")
            {
                // NOTE: we intentionally ignore this particular test case because it makes no sense in the .NET agent. Our agent does not allow for transaction metric names to only have a single segments -- that would be an invalid name.
                return;
            }

            var whitelistRUles = DefaultConfiguration.GetWhitelistRules(testCase.Rules);
            Mock.Arrange(() => _configuration.TransactionNameWhitelistRules).Returns(whitelistRUles);

            foreach (var test in testCase.Tests)
            {
                if (test == null)
                    continue;

                var originalName = test.Input;
                var originalMetricName = AsTransactionMetricName(originalName);

                var actualOutput = _metricNameService.RenameTransaction(originalMetricName);

                if (test.Expected == null)
                {
                    Assert.IsTrue(actualOutput.ShouldIgnore);
                }
                else
                {
                    Assert.IsFalse(actualOutput.ShouldIgnore);
                    Assert.AreEqual(test.Expected, actualOutput.PrefixedName);
                }
            }
        }

        #endregion RenameTransaction

        #region RenameMetric

        [Test]
        [TestCase(null, null, Description = "Null in, null out")]
        [TestCase("/apple", "/APPLE", Description = "Simple rule")]
        [TestCase("/banana", "/banana", Description = "Rule that doesn't match")]
        [TestCase("/banana/pie", "/BANANA/*", Description = "Rule with regex that matches")]
        [TestCase("/apple/banana/pie", "/APPLE/BANANA/*", Description = "Two matching rules that don't conflict")]
        [TestCase("/banana/apple/pie", "/BANANA/*", Description = "Two matching rules that conflict -- evaluation order matters!")]
        public void RenameMetric_RenamesMetricAccordingToRules_IfRuleIsNotIgnore(string input, string expectedOutput)
        {
            Mock.Arrange(() => _configuration.MetricNameRegexRules).Returns(new List<RegexRule>
            {
                new RegexRule("/apple", "/APPLE", false, 1, false, false, false),
                new RegexRule("/banana/.*", "/BANANA/*", false, 2, false, false, false)
            });

            var actualOutput = _metricNameService.RenameMetric(input);

            Assert.AreEqual(expectedOutput, actualOutput);
        }

        [Test]
        [TestCase("/apple", null)]
        [TestCase("/banana", "/banana")]
        public void RenameMetric_ReturnsNull_IfRuleIsIgnore(string input, string expectedOutput)
        {
            Mock.Arrange(() => _configuration.MetricNameRegexRules).Returns(new List<RegexRule>
            {
                new RegexRule("/apple", "ThisWillNeverHappen/apple", false, 10, false, false, false),
                new RegexRule("/apple", null, true, 10, false, false, false)
            });

            var actualOutput = _metricNameService.RenameMetric(input);

            Assert.AreEqual(expectedOutput, actualOutput);
        }

        [TestCaseSource(typeof(MetricNameServiceTest), "CrossAgentRegexRuleTestCases")]
        public void RenameMetric_PassesAllCrossAgentUrlTests(RegexRuleTestCase testCase)
        {
            var regexRules = DefaultConfiguration.GetRegexRules(testCase.Rules);
            Mock.Arrange(() => _configuration.MetricNameRegexRules).Returns(regexRules);

            foreach (var test in testCase.Tests)
            {
                if (test == null)
                    continue;

                var actualOutput = _metricNameService.RenameMetric(test.Input);

                Assert.AreEqual(test.Expected, actualOutput);
            }
        }

        #endregion RenameMetric

        #region Cross-agent test data

        public class WhitelistRuleTestCase
        {
            [JsonProperty(PropertyName = "testname")]
            public string TestName { get; set; }
            [JsonProperty(PropertyName = "transaction_segment_terms")]
            public IEnumerable<ServerConfiguration.WhitelistRule> Rules { get; set; }
            [JsonProperty(PropertyName = "tests")]
            public IEnumerable<TestCase> Tests { get; set; }
        }

        public class RegexRuleTestCase
        {
            [JsonProperty(PropertyName = "testname")]
            public string TestName { get; set; }
            [JsonProperty(PropertyName = "rules")]
            public IEnumerable<ServerConfiguration.RegexRule> Rules { get; set; }
            [JsonProperty(PropertyName = "tests")]
            public IEnumerable<TestCase> Tests { get; set; }
        }

        public class TestCase
        {
            [JsonProperty(PropertyName = "input")]
            public string Input { get; set; }
            [JsonProperty(PropertyName = "expected")]
            public string Expected { get; set; }
        }

        private static IEnumerable<RegexRuleTestCase[]> CrossAgentRegexRuleTestCases
        {
            get
            {
                #region JSON

                const string json = @"
[
  {
    ""testname"":""replace first"",
    ""rules"":[{""match_expression"":""(psi)"", ""replacement"":""gamma"", ""ignore"":false, ""eval_order"":0}],
    ""tests"":
    [
      {""input"":""/alpha/psi/beta"", ""expected"":""/alpha/gamma/beta""},
      {""input"":""/psi/beta"", ""expected"":""/gamma/beta""},
      {""input"":""/alpha/psi"", ""expected"":""/alpha/gamma""}
    ]
  },
  {
    ""testname"":""resource normalization rule"",
    ""rules"":[{""match_expression"":""(.*)/[^/]*.(bmp|css|gif|ico|jpg|jpeg|js|png)$"", ""replacement"":""\\1/*.\\2"", ""ignore"":false, ""eval_order"":1}],
    ""tests"":
    [
      {""input"":""/test/dude/flower.jpg"", ""expected"":""/test/dude/*.jpg""},
      {""input"":""/DUDE.ICO"", ""expected"":""/*.ICO""}
    ]
  },
  {
    ""testname"":""replace first"",
    ""rules"":[{""match_expression"":""^/userid/.*/folderid"", ""replacement"":""/userid/*/folderid/*"", ""ignore"":false, ""eval_order"":1},
             {""match_expression"":""/need_not_be_first_segment/.*"", ""replacement"":""*/need_not_be_first_segment/*"", ""ignore"":false, ""eval_order"":2}],
    ""tests"":
    [
      {""input"":""/userid/123abc/folderid/qwerty8855"", ""expected"":""/userid/*/folderid/*/qwerty8855""},
      {""input"":""/first/need_not_be_first_segment/uiop"", ""expected"":""/first*/need_not_be_first_segment/*""}
    ]
  },
  {
    ""testname"":""ignore rule"",
    ""rules"":[{""match_expression"":""^/artists/az/(.*)/(.*)$"", ""replacement"":""/artists/az/*/\\2"", ""ignore"":true, ""eval_order"":11}],
    ""tests"":
    [
      {""input"":""/artists/az/veritas/truth.jhtml"", ""expected"":null}
    ]
  },
  {
    ""testname"":""hexadecimal each segment rule"",
    ""rules"":[{""match_expression"":""^[0-9a-f]*[0-9][0-9a-f]*$"", ""replacement"":""*"", ""ignore"":false, ""eval_order"":1, ""each_segment"":true}],
    ""tests"":
    [
      {""input"":""/test/1axxx/4babe/cafe222/bad/a1b2c3d3e4f5/ABC123/x999/111"", ""expected"":""/test/1axxx/*/*/bad/*/*/x999/*""},
      {""input"":""/test/4/dude"", ""expected"":""/test/*/dude""},
      {""input"":""/test/babe4/999x"", ""expected"":""/test/*/999x""},
      {""input"":""/glass/resource/vase/images/9ae1283"", ""expected"":""/glass/resource/vase/images/*""},
      {""input"":""/test/4/dude.jsp"", ""expected"":""/test/*/dude.jsp""},
      {""input"":""/glass/resource/vase/images/add"", ""expected"":""/glass/resource/vase/images/add""}
    ]
  },
  {
    ""testname"":""url encoded segments rule"",
    ""rules"":[{""match_expression"":""(.*)%(.*)"", ""replacement"":""*"", ""ignore"":false, ""eval_order"":1, ""each_segment"":true, ""terminate_chain"":false, ""replace_all"":false}],
    ""tests"":
    [
      {""input"":""/test/%%%/bad%%/a1b2%c3%d3e4f5/x999/111%"", ""expected"":""/test/*/*/*/x999/*""},
      {""input"":""/add-resource/vmqoiearks%1B%3R"", ""expected"":""/add-resource/*""}
    ]
  },
  {
    ""testname"":""remove all ticks"",
    ""rules"":[{""match_expression"":""([^']*)'+"", ""replacement"":""\\1"", ""ignore"":false, ""eval_order"":1, ""each_segment"":false, ""replace_all"":true}],
    ""tests"":
    [
      {""input"":""/test/'''/b'a''d''/a1b2'c3'd3e4f5/x999/111'"", ""expected"":""/test//bad/a1b2c3d3e4f5/x999/111""}
    ]
  },
  {
    ""testname"":""number rule"",
    ""rules"":[{""match_expression"":""\\d+"", ""replacement"":""*"", ""ignore"":false, ""eval_order"":1, ""each_segment"":false, ""replace_all"":true}],
    ""tests"":
    [
      {""input"":""/solr/shard03/select"", ""expected"":""/solr/shard*/select""},
      {""input"":""/hey/r2d2"", ""expected"":""/hey/r*d*""}
    ]
  },
  {
    ""testname"":""custom rules"",
    ""rules"":
    [
      {""match_expression"":""^/([^/]*=[^/]*&?)+"", ""replacement"":""/all_params"", ""ignore"":false, ""eval_order"":0, ""each_segment"":false, ""terminate_chain"":true},
      {""match_expression"":""^/.*/PARAMS/(article|legacy_article|post|product)/.*"", ""replacement"":""/*/PARAMS/\\1/*"", ""ignore"":false, ""eval_order"":14, ""each_segment"":false, ""terminate_chain"":true},
      {""match_expression"":""^/test/(.*)"", ""replacement"":""/dude"", ""ignore"":false, ""eval_order"":1, ""each_segment"":false, ""terminate_chain"":true},
      {""match_expression"":""^/blah/(.*)"", ""replacement"":""/\\1"", ""ignore"":false, ""eval_order"":2, ""each_segment"":false, ""terminate_chain"":true},
      {""match_expression"":""/.*(dude|man)"", ""replacement"":""/*.\\1"", ""ignore"":false, ""eval_order"":3, ""each_segment"":false, ""terminate_chain"":true},
      {""match_expression"":""^/(bob)"", ""replacement"":""/\\1ert/\\1/\\1ertson"", ""ignore"":false, ""eval_order"":4, ""each_segment"":false, ""terminate_chain"":true},
      {""match_expression"":""/foo(.*)"", ""ignore"":true, ""eval_order"":5, ""each_segment"":false, ""terminate_chain"":true}
    ],
    ""tests"":
    [
      {""input"":""/xs=zs&fly=*&row=swim&id=*&"", ""expected"":""/all_params""},
      {""input"":""/zip-zap/PARAMS/article/*"", ""expected"":""/*/PARAMS/article/*""},
      {""input"":""/bob"", ""expected"":""/bobert/bob/bobertson""},
      {""input"":""/test/foobar"", ""expected"":""/dude""},
      {""input"":""/bar/test"", ""expected"":""/bar/test""},
      {""input"":""/blah/test/man"", ""expected"":""/test/man""},
      {""input"":""/oh/hey.dude"", ""expected"":""/*.dude""},
      {""input"":""/oh/hey/what/up.man"", ""expected"":""/*.man""},
      {""input"":""/foo"", ""expected"":null},
      {""input"":""/foo/foobar"", ""expected"":null}
    ]
  },
  {
    ""testname"":""chained rules"",
    ""rules"":
    [
      {""match_expression"":""^[0-9a-f]*[0-9][0-9a-f]*$"", ""replacement"":""*"", ""ignore"":false, ""eval_order"":1, ""each_segment"":true, ""terminate_chain"":false},
      {""match_expression"":""(.*)/fritz/(.*)"", ""replacement"":""\\1/karl/\\2"", ""ignore"":false, ""eval_order"":11, ""each_segment"":false, ""terminate_chain"":true}
    ],
    ""tests"":
    [
      {""input"":""/test/1axxx/4babe/fritz/x999/111"", ""expected"":""/test/1axxx/*/karl/x999/*""}
    ]
  },
  {
    ""testname"":""rule ordering (two rules match, but only one is applied due to ordering)"",
    ""rules"":
    [
      {""match_expression"":""/test/(.*)"", ""replacement"":""/el_duderino"", ""ignore"":false, ""eval_order"":37},
      {""match_expression"":""/test/(.*)"", ""replacement"":""/dude"", ""ignore"":false, ""eval_order"":1},
      {""match_expression"":""/blah/(.*)"", ""replacement"":""/$1"", ""ignore"":false, ""eval_order"":2},
      {""match_expression"":""/foo(.*)"", ""ignore"":true, ""eval_order"":3}
    ],
    ""tests"":
    [
      {""input"":""/test/foobar"", ""expected"":""/dude""}
    ]
  },
  {
    ""testname"":""stable rule sorting"",
    ""rules"":
    [
      {""match_expression"":""/test/(.*)"", ""replacement"":""/you_first"", ""ignore"":false, ""eval_order"":0},
      {""match_expression"":""/test/(.*)"", ""replacement"":""/no_you"", ""ignore"":false, ""eval_order"":0},
      {""match_expression"":""/test/(.*)"", ""replacement"":""/please_after_you"", ""ignore"":false, ""eval_order"":0}
    ],
    ""tests"":
    [
      {""input"":""/test/polite_seattle_drivers"", ""expected"":""/you_first""}
    ]
  },
  {
    ""testname"":""custom rule chaining"",
    ""rules"":
    [
      {""match_expression"":""(.*)/robertson(.*)"", ""replacement"":""\\1/LAST_NAME\\2"", ""ignore"":false, ""eval_order"":0, ""terminate_chain"":false},
      {""match_expression"":""^/robert(.*)"", ""replacement"":""/bob\\1"", ""ignore"":false, ""eval_order"":1, ""terminate_chain"":true},
      {""match_expression"":""/LAST_NAME"", ""replacement"":""/fail"", ""ignore"":false, ""eval_order"":2, ""terminate_chain"":true}
    ],
    ""tests"":
    [
      {""input"":""/robert/robertson"", ""expected"":""/bob/LAST_NAME""}
    ]
  },
  {
    ""testname"":""sacksman's test"",
    ""rules"":[{""match_expression"":""^(?!account|application).*"", ""replacement"":""*"", ""ignore"":false, ""eval_order"":0, ""each_segment"":true}],
    ""tests"":
    [
      {""input"":""/account/myacc/application/test"", ""expected"":""/account/*/application/*""},
      {""input"":""/oh/dude/account/myacc/application"", ""expected"":""/*/*/account/*/application""}
    ]
  }
]
";

                #endregion JSON

                var testCases = JsonConvert.DeserializeObject<IEnumerable<RegexRuleTestCase>>(json);
                Assert.NotNull(testCases);
                return testCases
                    .Where(testCase => testCase != null)
                    .Select(testCase => new[] { testCase });
            }
        }

        private static IEnumerable<WhitelistRuleTestCase[]> CrossAgentWhitelistRuleTestCases
        {
            get
            {
                #region JSON

                const string json = @"
[
  {
    ""testname"": ""basic"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/Custom"",
        ""terms"": [""one"", ""two"", ""three""]
      },
      {
        ""prefix"": ""WebTransaction/Uri"",
        ""terms"": [""seven"", ""eight"", ""nine""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Uri/one/two/seven/user/nine/account"",
        ""expected"": ""WebTransaction/Uri/*/seven/*/nine/*""
      },
      {
        ""input"": ""WebTransaction/Custom/one/two/seven/user/nine/account"",
        ""expected"": ""WebTransaction/Custom/one/two/*""
      },
      {
        ""input"": ""WebTransaction/Other/one/two/foo/bar"",
        ""expected"": ""WebTransaction/Other/one/two/foo/bar""
      }
    ]
  },
  {
    ""testname"": ""prefix_with_trailing_slash"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/Custom/"",
        ""terms"": [""a"", ""b""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Custom/a/b/c"",
        ""expected"": ""WebTransaction/Custom/a/b/*""
      },
      {
        ""input"": ""WebTransaction/Other/a/b/c"",
        ""expected"": ""WebTransaction/Other/a/b/c""
      }
    ]
  },
  {
    ""testname"": ""prefix_with_trailing_spaces_and_then_slash"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/Custom    /"",
        ""terms"": [""a"", ""b""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Custom    /a/b/c"",
        ""expected"": ""WebTransaction/Custom    /a/b/*""
      },
      {
        ""input"": ""WebTransaction/Custom  /a/b/c"",
        ""expected"": ""WebTransaction/Custom  /a/b/c""
      },
      {
        ""input"": ""WebTransaction/Custom/a/b/c"",
        ""expected"": ""WebTransaction/Custom/a/b/c""
      }
    ]
  },
  {
    ""testname"": ""prefix_with_trailing_spaces"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/Custom    "",
        ""terms"": [""a"", ""b""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Custom    /a/b/c"",
        ""expected"": ""WebTransaction/Custom    /a/b/*""
      },
      {
        ""input"": ""WebTransaction/Custom  /a/b/c"",
        ""expected"": ""WebTransaction/Custom  /a/b/c""
      },
      {
        ""input"": ""WebTransaction/Custom/a/b/c"",
        ""expected"": ""WebTransaction/Custom/a/b/c""
      }
    ]
  },
  {
    ""testname"": ""overlapping_prefix_last_one_only_applied"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/Foo"",
        ""terms"": [""one"", ""two"", ""three""]
      },
      {
        ""prefix"": ""WebTransaction/Foo"",
        ""terms"": [""one"", ""two"", ""zero""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Foo/zero/one/two/three/four"",
        ""expected"": ""WebTransaction/Foo/zero/one/two/*""
      }
    ]
  },
  {
    ""testname"": ""terms_are_order_independent"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/Foo"",
        ""terms"": [""one"", ""two"", ""three""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Foo/bar/one/three/two"",
        ""expected"": ""WebTransaction/Foo/*/one/three/two""
      },
      {
        ""input"": ""WebTransaction/Foo/three/one/one/two/three"",
        ""expected"": ""WebTransaction/Foo/three/one/one/two/three""
      }
    ]
  },
  {
    ""testname"": ""invalid_rule_not_enough_prefix_segments"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction"",
        ""terms"": [""one"", ""two""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Foo/bar/one/three/two"",
        ""expected"": ""WebTransaction/Foo/bar/one/three/two""
      },
      {
        ""input"": ""WebTransaction/Foo/three/one/one/two/three"",
        ""expected"": ""WebTransaction/Foo/three/one/one/two/three""
      }
    ]
  },
  {
    ""testname"": ""invalid_rule_not_enough_prefix_segments_ending_in_slash"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/"",
        ""terms"": [""one"", ""two""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Foo/bar/one/three/two"",
        ""expected"": ""WebTransaction/Foo/bar/one/three/two""
      },
      {
        ""input"": ""WebTransaction/Foo/three/one/one/two/three"",
        ""expected"": ""WebTransaction/Foo/three/one/one/two/three""
      }
    ]
  },
  {
    ""testname"": ""invalid_rule_too_many_prefix_segments"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/Foo/bar"",
        ""terms"": [""one"", ""two""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Foo/bar/one/three/two"",
        ""expected"": ""WebTransaction/Foo/bar/one/three/two""
      },
      {
        ""input"": ""WebTransaction/Foo/three/one/one/two/three"",
        ""expected"": ""WebTransaction/Foo/three/one/one/two/three""
      }
    ]
  },
  {
    ""testname"": ""invalid_rule_prefix_with_trailing_slash_and_then_space"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/Custom/    "",
        ""terms"": [""a"", ""b""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Custom/a/b/c"",
        ""expected"": ""WebTransaction/Custom/a/b/c""
      }
    ]
  },
  {
    ""testname"": ""invalid_rule_prefix_with_multiple_trailing_slashes"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/Custom////"",
        ""terms"": [""a"", ""b""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Custom/a/b/c"",
        ""expected"": ""WebTransaction/Custom/a/b/c""
      }
    ]
  },
  {
    ""testname"": ""invalid_rule_null_prefix"",
    ""transaction_segment_terms"": [
      {
        ""terms"": [""one"", ""two"", ""three""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Custom/one/two/seven/user/nine/account"",
        ""expected"": ""WebTransaction/Custom/one/two/seven/user/nine/account""
      }
    ]
  },
  {
    ""testname"": ""invalid_rule_null_terms"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/Custom""
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Custom/one/two/seven/user/nine/account"",
        ""expected"": ""WebTransaction/Custom/one/two/seven/user/nine/account""
      }
    ]
  },
  {
    ""testname"": ""empty_terms"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/Custom"",
        ""terms"": []
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Custom/one/two/seven/user/nine/account"",
        ""expected"": ""WebTransaction/Custom/*""
      },
      {
        ""input"": ""WebTransaction/Custom/"",
        ""expected"": ""WebTransaction/Custom/""
      },
      {
        ""input"": ""WebTransaction/Custom"",
        ""expected"": ""WebTransaction/Custom""
      }
    ]
  },
  {
    ""testname"": ""two_segment_transaction_name"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/Foo"",
        ""terms"": [""a"", ""b"", ""c""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Foo"",
        ""expected"": ""WebTransaction/Foo""
      }
    ]
  },
  {
    ""testname"": ""two_segment_transaction_name_with_trailing_slash"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/Foo"",
        ""terms"": [""a"", ""b"", ""c""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Foo/"",
        ""expected"": ""WebTransaction/Foo/""
      }
    ]
  },
  {
    ""testname"": ""transaction_segment_with_adjacent_slashes"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/Foo"",
        ""terms"": [""a"", ""b"", ""c""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Foo///a/b///c/d/"",
        ""expected"": ""WebTransaction/Foo/*/a/b/*/c/*""
      },
      {
        ""input"": ""WebTransaction/Foo///a/b///c///"",
        ""expected"": ""WebTransaction/Foo/*/a/b/*/c/*""
      }
    ]
  },
  {
    ""testname"": ""transaction_name_with_single_segment"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/Foo"",
        ""terms"": [""a"", ""b"", ""c""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction"",
        ""expected"": ""WebTransaction""
      }
    ]
  },
  {
    ""testname"": ""prefix_must_match_first_two_segments"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/Zip"",
        ""terms"": [""a"", ""b""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Zip/a/b/c"",
        ""expected"": ""WebTransaction/Zip/a/b/*""
      },
      {
        ""input"": ""WebTransaction/ZipZap/a/b/c"",
        ""expected"": ""WebTransaction/ZipZap/a/b/c""
      }
    ]
  },
  {
    ""testname"": ""one_bad_rule_does_not_scrap_all_rules"",
    ""transaction_segment_terms"": [
      {
        ""prefix"": ""WebTransaction/MissingTerms""
      },
      {
        ""prefix"": ""WebTransaction/Uri"",
        ""terms"": [""seven"", ""eight"", ""nine""]
      }
    ],
    ""tests"": [
      {
        ""input"": ""WebTransaction/Uri/one/two/seven/user/nine/account"",
        ""expected"": ""WebTransaction/Uri/*/seven/*/nine/*""
      }
    ]
  }
]
";

                #endregion JSON

                var testCases = JsonConvert.DeserializeObject<IEnumerable<WhitelistRuleTestCase>>(json);
                Assert.NotNull(testCases);
                return testCases
                    .Where(testCase => testCase != null)
                    .Select(testCase => new[] { testCase });
            }
        }

        #endregion Cross-agent test data
    }
}
