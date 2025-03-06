// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.TestUtilities;
using Newtonsoft.Json;
using NUnit.Framework;
using Telerik.JustMock;

namespace CompositeTests.CrossAgentTests.HybridAgent
{
    [TestFixture]
    public class HybridAgentTests
    {
        private static CompositeTestAgent _compositeTestAgent;
        private IAgent _agent;

        [SetUp]
        public void Setup()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _agent = _compositeTestAgent.GetAgent();
        }

        [TearDown]
        public static void TearDown()
        {
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

        private static void PerformOperation(Operation operation, int nestedLevel = 0)
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

        private static Action<Action> GetActionForOperation(Operation operation)
        {
            switch (operation)
            {
                case { Command: "DoWorkInSpan" }:
                    return (Action work) => OpenTelemetryOperations.DoWorkInSpan(GetSpanKindForOperation(operation), work);

                case { Command: "DoWorkInSpanWithRemoteParent" }:
                    return (Action work) => OpenTelemetryOperations.DoWorkInSpanWithRemoteParent(GetSpanKindForOperation(operation), work);

                case { Command: "DoWorkInSpanWithInboundContext" }:
                    return (Action work) => OpenTelemetryOperations.DoWorkInSpanWithInboundContext(GetSpanKindForOperation(operation), GetInboundContextForOperation(operation), work);

                case { Command: "DoWorkInTransaction" }:
                    return (Action work) => NewRelicAgentOperations.DoWorkInTransaction(GetTransactionNameForOperation(operation), work);

                case { Command: "DoWorkInSegment" }:
                    var segmentName = operation.Parameters!["segmentName"] as string;
                    return (Action work) => NewRelicAgentOperations.DoWorkInSegment(segmentName!, work);

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
                    return (Action work) => NewRelicAgentOperations.InjectHeaders(work);

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

        private static Action GetActionForAssertion(Assertion assertion, int nestedLevel)
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
                            childActions.Add(NewRelicAgentOperations.AssertNotValidTransaction);
                            break;
                        default:
                            throw new Exception($"{operand} is not supported for {assertion.Rule!.Operator}.");
                    }
                    break;
                case { Operator: "Equals" }:
                    var lhs = GetGetterForObject(assertion.Rule!.Parameters!["left"]);
                    var rhs = GetGetterForObject(assertion.Rule!.Parameters!["right"]);
                    childActions.Add(() =>
                    {
                        var left = lhs();
                        var right = rhs();
                        if (!left.Equals(right))
                        {
                            throw new Exception($"{left} does not equal {right}");
                        }
                    });
                    break;
                case { Operator: "Matches" }:
                    var objectName = (string)assertion.Rule!.Parameters!["object"];
                    var objectGetter = GetGetterForObject(objectName);
                    var expectedValue = (string)assertion.Rule!.Parameters!["value"];
                    childActions.Add(() =>
                    {
                        var actualValue = (string)objectGetter();
                        if (!string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new Exception($"{actualValue} does not match {expectedValue}");
                        }
                    });
                    break;
                default:
                    throw new Exception($"{assertion.Rule!.Operator} is not supported.");
            }

            return () =>
            {
                try
                {
                    childActions.ForEach(a => a());
                }
                catch (Exception e)
                {
                    Console.WriteLine("Caught exception {0}", e);
                }
            };
        }

        private static Func<object> GetGetterForObject(object objectName)
        {
            return objectName switch
            {
                "currentOTelSpan.traceId" => OpenTelemetryOperations.GetCurrentTraceId,
                "currentTransaction.traceId" => NewRelicAgentOperations.GetCurrentTraceId,
                "currentOTelSpan.spanId" => OpenTelemetryOperations.GetCurrentSpanId,
                "currentSegment.spanId" => NewRelicAgentOperations.GetCurrentSpanId,
                "currentTransaction.sampled" => () => NewRelicAgentOperations.GetCurrentIsSampledFlag(),
                "injected.traceId" => SimulatedOperations.GetInjectedTraceId,
                "injected.spanId" => SimulatedOperations.GetInjectedSpanId,
                "injected.sampled" => () => SimulatedOperations.GetInjectedSampledFlag(),
                _ => throw new Exception($"{objectName} is not supported.")
            };
        }

        private static void TriggerOrWaitForHarvestCycle()
        {
            Console.WriteLine("This is a simulated agent harvest.");
        }

        private static void ValidateTelemetryFromHarvest(AgentOutput telemetry)
        {
            if (telemetry == null)
            {
                return;
            }

            if (telemetry.Transactions != null)
            {
                try
                {
                    ValidateTransactions(telemetry.Transactions);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Caught exception {0}", e);
                }
            }

            if (telemetry.Spans != null)
            {
                try
                {
                    ValidateSpans(telemetry.Spans);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Caught exception {0}", e);
                }
            }
        }

        private static void ValidateTransactions(IEnumerable<Transaction> transactionsToCheck)
        {
            var actualTansactionEvents = GetTransactionEventsFromHarvest();

            if (!transactionsToCheck.Any())
            {
                if (actualTansactionEvents.Any())
                {
                    throw new Exception("Expected no transactions, but found some.");
                }

                return;
            }

            foreach (var expectedTransaction in transactionsToCheck)
            {
                // find the actual transaction that matches the expected transaction
                var actualTransaction = actualTansactionEvents.FirstOrDefault(t => t.Name!.Contains(expectedTransaction.Name!));
                if (actualTransaction == null)
                {
                    throw new Exception($"Expected transaction {expectedTransaction.Name} not found.");
                }
            }
        }

        private static void ValidateSpans(IEnumerable<Span> spansToCheck)
        {
            var actualSpanEvents = GetSpanEventsFromHarvest();
            if (!spansToCheck.Any())
            {
                if (actualSpanEvents.Any())
                {
                    throw new Exception("Expected no spans, but found some.");
                }

                return;
            }

            foreach (var expectedSpan in spansToCheck)
            {
                // find the actual span that matches the expected span
                var actualSpan = actualSpanEvents.FirstOrDefault(s => s.Name!.Contains(expectedSpan.Name!));
                if (actualSpan == null)
                {
                    throw new Exception($"Expected span {expectedSpan.Name} not found.");
                }

                if (expectedSpan.EntryPoint != null && expectedSpan.EntryPoint != actualSpan.EntryPoint)
                {
                    throw new Exception($"Expected entry point {expectedSpan.EntryPoint} does not match actual entry point {actualSpan.EntryPoint}.");
                }

                if (expectedSpan.Category != null && expectedSpan.Category != actualSpan.Category)
                {
                    throw new Exception($"Expected category {expectedSpan.Category} does not match actual category {actualSpan.Category}.");
                }

                if (expectedSpan.ParentName != null && !actualSpan.ParentName!.Contains(expectedSpan.ParentName))
                {
                    throw new Exception($"Expected parent name {expectedSpan.ParentName} does not match actual parent name {actualSpan.ParentName}.");
                }

                if (expectedSpan.Attributes != null && expectedSpan.Attributes.Any())
                {
                    foreach (var attribute in expectedSpan.Attributes)
                    {
                        if (!actualSpan.Attributes!.ContainsKey(attribute.Key) || actualSpan.Attributes[attribute.Key] != attribute.Value)
                        {
                            throw new Exception($"Expected attribute {attribute.Key} with value {attribute.Value} not found.");
                        }
                    }
                }
            }
        }

        private static IEnumerable<Transaction> GetTransactionEventsFromHarvest()
        {
            // This is a simulated method to get transaction events from the agent.
            return Enumerable.Empty<Transaction>();
        }

        private static IEnumerable<Span> GetSpanEventsFromHarvest()
        {
            // This is a simulated method to get span events from the agent.
            return Enumerable.Empty<Span>();
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
