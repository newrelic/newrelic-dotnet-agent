// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.OpenTelemetryBridge;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.TestUtilities;
using Newtonsoft.Json;
using NUnit.Framework;

namespace CompositeTests.CrossAgentTests.HybridAgent
{
    [TestFixture]
    public class HybridAgentTests
    {
        private static CompositeTestAgent _compositeTestAgent;
        private IAgent _agent;
        private NewRelicAgentOperations _newRelicAgentOperations;
        private ActivityBridge _activityBridge;

        [SetUp]
        public void Setup()
        {
            _compositeTestAgent = new CompositeTestAgent();
            // Used for the DT tests to identify the correct tracestate header component
            _compositeTestAgent.ServerConfiguration.TrustedAccountKey = "1";


            // enable the OTel bridge
            _compositeTestAgent.LocalConfiguration.openTelemetryBridge.enabled = true;
            // configure the activity source we want to listen to
            _compositeTestAgent.LocalConfiguration.appSettings.Add(new configurationAdd() { key = "OpenTelemetry.ActivitySource.Include", value = "TestApp activity source" });
            // update configuration
            _compositeTestAgent.PushConfiguration();

            _agent = _compositeTestAgent.GetAgent();
            _newRelicAgentOperations = new NewRelicAgentOperations(_agent);

            Console.WriteLine("OTel activity source is ready", OpenTelemetryOperations.TestAppActivitySource.Name);

            _activityBridge = new ActivityBridge(_agent, _compositeTestAgent.Container.Resolve<IErrorService>());
            _activityBridge.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _newRelicAgentOperations = null;
            _activityBridge.Dispose();
            _compositeTestAgent.Dispose();
        }

        [TestCaseSource(nameof(GetHybridAgentTestData))]
        public void HybridAgent_CrossAgentTests(HybridAgentTestCase test)
        {
            foreach (var operation in test.Operations ?? Enumerable.Empty<Operation>())
            {
                PerformOperation(operation);
            }

            TriggerOrWaitForHarvestCycle();

            ValidateTelemetryFromHarvest(test.Telemetry);
        }

        private void PerformOperation(Operation operation, int nestedLevel = 0)
        {
            List<Action> childActions = new() {
                () => Console.WriteLine($"{new string(' ', nestedLevel * 2)}Performing operation: {operation.Command}")
            };

            foreach (var childOperation in operation.ChildOperations ?? Enumerable.Empty<Operation>())
            {
                childActions.Add(() => PerformOperation(childOperation, nestedLevel + 1));
            }

            foreach (var assertion in operation.Assertions ?? Enumerable.Empty<Assertion>())
            {
                childActions.Add(GetActionForAssertion(assertion, nestedLevel));
            }

            var action = GetActionForOperation(operation);
            action(() => childActions.ForEach(childAction => childAction()));
        }

        private Action<Action> GetActionForOperation(Operation operation)
        {
            switch (operation)
            {
                case { Command: "DoWorkInSpan" }:
                    return (Action work) => OpenTelemetryOperations.DoWorkInSpan(GetSpanNameForOperation(operation), GetSpanKindForOperation(operation), work);

                case { Command: "DoWorkInSpanWithRemoteParent" }:
                    return (Action work) => OpenTelemetryOperations.DoWorkInSpanWithRemoteParent(GetSpanNameForOperation(operation), GetSpanKindForOperation(operation), work);

                case { Command: "DoWorkInSpanWithInboundContext" }:
                    return (Action work) => OpenTelemetryOperations.DoWorkInSpanWithInboundContext(GetSpanNameForOperation(operation), GetSpanKindForOperation(operation), GetInboundContextForOperation(operation), work);

                case { Command: "DoWorkInTransaction" }:
                    return (Action work) => _newRelicAgentOperations.DoWorkInTransaction(GetTransactionNameForOperation(operation), work);

                case { Command: "DoWorkInSegment" }:
                    var segmentName = operation.Parameters!["segmentName"] as string;
                    return (Action work) => _newRelicAgentOperations.DoWorkInSegment(segmentName!, work);

                case { Command: "AddOTelAttribute" }:
                    var name = operation.Parameters!["name"] as string;
                    var value = operation.Parameters!["value"];
                    return (Action work) => OpenTelemetryOperations.AddAttributeToCurrentSpan(name!, value, work);

                case { Command: "RecordExceptionOnSpan" }:
                    var errorMessage = operation.Parameters!["errorMessage"] as string;
                    return (Action work) => OpenTelemetryOperations.RecordExceptionOnSpan(errorMessage!, work);

                case { Command: "SimulateExternalCall" }:
                    var url = operation.Parameters!["url"] as string;
                    return (Action work) => SimulatedOperations.ExternalCall(url!, work);

                case { Command: "OTelInjectHeaders" }:
                    return (Action work) => OpenTelemetryOperations.InjectHeaders(work);

                case { Command: "NRInjectHeaders" }:
                    return (Action work) => _newRelicAgentOperations.InjectHeaders(work);

                default:
                    throw new Exception($"{operation.Command} is not supported.");
            }
        }

        private static ActivityKind GetSpanKindForOperation(Operation operation)
        {
            return operation.Parameters!["spanKind"] switch
            {
                "Internal" => ActivityKind.Internal,
                "Client" => ActivityKind.Client,
                "Server" => ActivityKind.Server,
                _ => throw new NotImplementedException(),
            };
        }

        private static string GetSpanNameForOperation(Operation operation)
        {
            return (string)operation.Parameters!["spanName"];
        }

        private static InboundContext GetInboundContextForOperation(Operation operation)
        {
            return new InboundContext
            {
                TraceId = (string)operation.Parameters!["traceIdInHeader"],
                SpanId = (string)operation.Parameters!["spanIdInHeader"],
                Sampled = operation.Parameters!["sampledFlagInHeader"] switch
                {
                    "0" => false,
                    "1" => true,
                    string s => bool.Parse(s),
                    _ => throw new NotImplementedException(),
                },
            };
        }

        private static string GetTransactionNameForOperation(Operation operation)
        {
            return (string)operation.Parameters!["transactionName"];
        }

        private Action GetActionForAssertion(Assertion assertion, int nestedLevel)
        {
            List<Action> childActions = new() {
                () => Console.WriteLine($"{new string(' ', nestedLevel * 2)}Performing assertion: {assertion.Description}")
            };

            switch (assertion.Rule)
            {
                case { Operator: "NotValid" }:
                    var operand = assertion.Rule!.Parameters!["object"] as string;
                    switch (operand)
                    {
                        case "currentOTelSpan":
                            childActions.Add(OpenTelemetryOperations.AssertNotValidSpan);
                            break;
                        case "currentTransaction":
                            childActions.Add(_newRelicAgentOperations.AssertNotValidTransaction);
                            break;
                        default:
                            throw new Exception($"{operand} is not supported for {assertion.Rule!.Operator}.");
                    }
                    break;
                case { Operator: "Equals" }:
                    var lhsName = (string)assertion.Rule!.Parameters!["left"];
                    var rhsName = (string)assertion.Rule!.Parameters!["right"];
                    var lhs = GetGetterForObject(lhsName);
                    var rhs = GetGetterForObject(rhsName);
                    childActions.Add(() =>
                    {
                        var left = lhs();
                        var right = rhs();

                        Assert.That(left, Is.EqualTo(right), $"{lhsName}({left}) does not equal {rhsName}({right})");
                    });
                    break;
                case { Operator: "Matches" }:
                    var objectName = (string)assertion.Rule!.Parameters!["object"];
                    var objectGetter = GetGetterForObject(objectName);
                    var expectedValue = (string)assertion.Rule!.Parameters!["value"];
                    childActions.Add(() =>
                    {
                        var actualValue = (string)objectGetter();
                        Assert.That(actualValue, Is.EqualTo(expectedValue).IgnoreCase, $"{objectName}({actualValue}) does not match {expectedValue}");
                    });
                    break;
                default:
                    throw new Exception($"{assertion.Rule!.Operator} is not supported.");
            }

            return () =>
            {
                childActions.ForEach(a => a());
            };
        }

        private Func<object> GetGetterForObject(object objectName)
        {
            return objectName switch
            {
                "currentOTelSpan.traceId" => OpenTelemetryOperations.GetCurrentTraceId,
                "currentTransaction.traceId" => _newRelicAgentOperations.GetCurrentTraceId,
                "currentOTelSpan.spanId" => OpenTelemetryOperations.GetCurrentSpanId,
                "currentSegment.spanId" => _newRelicAgentOperations.GetCurrentSpanId,
                "currentTransaction.sampled" => () => _newRelicAgentOperations.GetCurrentIsSampledFlag(),
                "injected.traceId" => SimulatedOperations.GetInjectedTraceId,
                "injected.spanId" => SimulatedOperations.GetInjectedSpanId,
                "injected.sampled" => () => SimulatedOperations.GetInjectedSampledFlag(),
                _ => throw new Exception($"{objectName} is not supported.")
            };
        }

        private void TriggerOrWaitForHarvestCycle()
        {
            _compositeTestAgent.Harvest();
        }

        private void ValidateTelemetryFromHarvest(AgentOutput telemetry)
        {
            if (telemetry == null)
            {
                return;
            }

            if (telemetry.Transactions != null)
            {
                ValidateTransactions(telemetry.Transactions);
            }

            if (telemetry.Spans != null)
            {
                ValidateSpans(telemetry.Spans);
            }
        }

        private void ValidateTransactions(IEnumerable<Transaction> transactionsToCheck)
        {
            var actualTransactionEvents = GetTransactionEventsFromHarvest();

            if (!transactionsToCheck.Any())
            {
                Assert.That(actualTransactionEvents, Is.Empty, "Expected no transactions but found some.");

                return;
            }

            foreach (var expectedTransaction in transactionsToCheck)
            {
                // find the actual transaction that matches the expected transaction
                Assert.That(actualTransactionEvents, Has.One.Matches<TransactionEventWireModel>(t => TransactionEventHasName(t, expectedTransaction.Name!)), $"Expected transaction {expectedTransaction.Name} not found in {string.Join(", ", actualTransactionEvents.Select(GetTransactionNameFromTransactionEvent))}.");
            }
        }

        private static bool TransactionEventHasName(TransactionEventWireModel transactionEvent, string name)
        {
            var transactionName = GetTransactionNameFromTransactionEvent(transactionEvent);
            return transactionName.Contains(name);
        }

        private static string GetTransactionNameFromTransactionEvent(TransactionEventWireModel transactionEvent)
        {
            return (string)transactionEvent.IntrinsicAttributes()["name"];
        }

        private void ValidateSpans(IEnumerable<Span> spansToCheck)
        {
            var actualSpanEvents = GetSpanEventsFromHarvest().ToList();
            if (!spansToCheck.Any())
            {
                Assert.That(actualSpanEvents, Is.Empty, "Expected no spans but found some.");

                return;
            }

            foreach (var expectedSpan in spansToCheck)
            {
                // find the actual span that matches the expected span
                Assert.That(actualSpanEvents, Has.One.Matches<ISpanEventWireModel>(s => SpanEventHasName(s, expectedSpan.Name!)), $"Expected span {expectedSpan.Name} not found in {string.Join(", ", actualSpanEvents.Select(s => (string)s.IntrinsicAttributes()["name"]))}.");

                var actualSpan = actualSpanEvents.FirstOrDefault(s => SpanEventHasName(s, expectedSpan.Name!));

                if (expectedSpan.EntryPoint != null)
                {
                    var entryPoint = actualSpan.IntrinsicAttributes()["nr.entryPoint"];
                    Assert.That(entryPoint, Is.EqualTo(expectedSpan.EntryPoint), $"Expected entry point {expectedSpan.EntryPoint} does not match actual entry point {entryPoint}.");
                }

                if (expectedSpan.Category != null)
                {
                    var category = actualSpan.IntrinsicAttributes()["category"];
                    Assert.That(category, Is.EqualTo(expectedSpan.Category), $"Expected category {expectedSpan.Category} does not match actual category {category}.");
                }

                if (expectedSpan.ParentName != null)
                {
                    var parentId = (string)actualSpan.IntrinsicAttributes()["parentId"];
                    var parent = actualSpanEvents.Single(s => (string)s.IntrinsicAttributes()["guid"] == parentId);
                    var parentName = (string)parent.IntrinsicAttributes()["name"];

                    Assert.That(parentName, Does.EndWith(expectedSpan.ParentName), $"Expected parent name {expectedSpan.ParentName} does not match actual parent name {parentName}.");
                }

                if (expectedSpan.Attributes != null && expectedSpan.Attributes.Any())
                {
                    foreach (var attribute in expectedSpan.Attributes)
                    {
                        var allAttributes = actualSpan.GetAllAttributeValuesDic();

                        Assert.That(allAttributes, Contains.Key(attribute.Key), $"Expected attribute {attribute.Key} not found.");
                        Assert.That(allAttributes[attribute.Key], Is.EqualTo(attribute.Value), $"Expected attribute {attribute.Key} with value {attribute.Value} does not match actual value {allAttributes[attribute.Key]}.");
                    }
                }
            }
        }

        private static bool SpanEventHasName(ISpanEventWireModel spanEvent, string name)
        {
            var spanName = (string)spanEvent.IntrinsicAttributes()["name"];
            return spanName.Contains(name);
        }

        private IEnumerable<TransactionEventWireModel> GetTransactionEventsFromHarvest()
        {
            return _compositeTestAgent.TransactionEvents;
        }

        private IEnumerable<ISpanEventWireModel> GetSpanEventsFromHarvest()
        {
            return _compositeTestAgent.SpanEvents;
        }

        private static List<TestCaseData> GetHybridAgentTestData()
        {
            var testCaseDatas = new List<TestCaseData>();

            string location = Assembly.GetExecutingAssembly().GetLocation();
            var dllPath = Path.GetDirectoryName(new Uri(location).LocalPath);
            var jsonPath = Path.Combine(dllPath, "CrossAgentTests", "HybridAgent", "HybridAgentTestCaseDefinitions.json");
            var jsonString = File.ReadAllText(jsonPath);
            var testList = JsonConvert.DeserializeObject<List<HybridAgentTestCase>>(jsonString);

            foreach (var testData in testList)
            {
                var testCase = new TestCaseData([testData]);
                testCase.SetName("HybridAgentCrossAgentTests: " + testData.TestDescription);
                testCaseDatas.Add(testCase);
            }

            return testCaseDatas;
        }
    }
}
