// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include "stdafx.h"
#include "CppUnitTest.h"
#include "ConfigurationTestTemplates.h"
#include "../Configuration/InstrumentationConfiguration.h"
#include "../sicily/SicilyTest/RealisticTokenizer.h"
#include "../MethodRewriterTest/MockFunction.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace Configuration { namespace Test
{
    class MockTokenResolver : public SignatureParser::ITokenResolver
    {
    public:
        MockTokenResolver(const std::wstring& typeString = L"MyNamespace.MyClass") : _typeString(typeString) {}

        virtual std::wstring GetTypeStringsFromTypeDefOrRefOrSpecToken(uint32_t /*typeDefOrRefOrSPecToken*/) override
        {
            return _typeString;
        }

        uint32_t _typeGenericArgumentCount;
        virtual uint32_t GetTypeGenericArgumentCount(uint32_t /*typeDefOrMethodDefToken*/)
        {
            return _typeGenericArgumentCount;
        }

    private:
        std::wstring _typeString;
    };

    TEST_CLASS(InstrumentationConfigurationTest)
    {
    public:
        TEST_METHOD(increment_invalid_file)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"<?xml version=\"1.0\" encoding=\"utf-8\"?><blah");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            Assert::AreEqual(1, int(instrumentation.GetInvalidFileCount()));
        }
        
        TEST_METHOD(no_matches_with_empty_xml)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::IsTrue(instrumentationPoint == nullptr);
            Assert::AreEqual(0, int(instrumentation.GetInvalidFileCount()));
        }

        TEST_METHOD(basic_match)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::IsFalse(instrumentationPoint == nullptr);
        }

        TEST_METHOD(basic_match_with_version)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\" minVersion=\"1.0.0\" maxVersion=\"2.0.0\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"1.2.3.4"));
            Assert::IsFalse(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"1.0.0.0"));
            Assert::IsFalse(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"1.9"));
            Assert::IsFalse(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"1.9.9.9"));
            Assert::IsFalse(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"1.0.5.2"));
            Assert::IsFalse(instrumentationPoint == nullptr);
        }

        TEST_METHOD(basic_mismatch_with_version)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\" minVersion=\"1.0.0\" maxVersion=\"2.0.0\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"3.0.0.0"));
            Assert::IsTrue(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"0.1.0.0"));
            Assert::IsTrue(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"2.0.3.0"));
            Assert::IsTrue(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"9.0.0.0"));
            Assert::IsTrue(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"0.9.9.9"));
            Assert::IsTrue(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"2.0.0.0"));
            Assert::IsTrue(instrumentationPoint == nullptr);
        }

        TEST_METHOD(basic_match_with_minversion)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\" minVersion=\"1.0.0\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"1.0"));
            Assert::IsFalse(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"1.2.3.4"));
            Assert::IsFalse(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"3.0.0.0"));
            Assert::IsFalse(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"5.0.0.0"));
            Assert::IsFalse(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"7.4.12.0"));
            Assert::IsFalse(instrumentationPoint == nullptr);
        }

        TEST_METHOD(basic_match_with_maxversion)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\" maxVersion=\"2.5.0\"> \
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"2.2"));
            Assert::IsFalse(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"2.0.0.0"));
            Assert::IsFalse(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"2.4.9.9"));
            Assert::IsFalse(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"1.2"));
            Assert::IsFalse(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"0.2.9"));
            Assert::IsFalse(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"1.0.0.2"));
            Assert::IsFalse(instrumentationPoint == nullptr);
        }

        TEST_METHOD(basic_mismatch_with_minversion)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\" minVersion=\"4.0.0.0\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"1.2.0.0"));
            Assert::IsTrue(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"1.9.9.9"));
            Assert::IsTrue(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"0.1.0.0"));
            Assert::IsTrue(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"3.5.0.66"));
            Assert::IsTrue(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"2.22.333.6"));
            Assert::IsTrue(instrumentationPoint == nullptr);
        }

        TEST_METHOD(basic_mismatch_with_maxversion)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\" maxVersion=\"3.0.0\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"3.0.0.0"));
            Assert::IsTrue(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"3.1"));
            Assert::IsTrue(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"3.0.0.1"));
            Assert::IsTrue(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"4.5.78.1"));
            Assert::IsTrue(instrumentationPoint == nullptr);

            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>(false, L"3"));
            Assert::IsTrue(instrumentationPoint == nullptr);
        }


        TEST_METHOD(deprecated_instrumentation_xml_is_ignored)
        {
            wchar_t* wrapperNames[3] =
            {
                L"NewRelic.Providers.Wrapper.Logging.Instrumentation.xml",
                L"NewRelic.Providers.Wrapper.CastleMonoRail2.Instrumentation.xml",
                L"NewRelic.Providers.Wrapper.Asp35.Instrumentation.xml"
            };

            int size = sizeof(wrapperNames) / sizeof(wchar_t*);

            for (int i = 0; i < size; i++)
            {
                InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
                xmlSet->emplace(wrapperNames[i], L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
                InstrumentationConfiguration instrumentation(xmlSet, nullptr);
                auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
                Assert::IsTrue(instrumentationPoint == nullptr);
            }
        }

        TEST_METHOD(no_assembly_match)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyOtherAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::IsTrue(instrumentationPoint == nullptr);
        }

        TEST_METHOD(no_class_match)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyOtherClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::IsTrue(instrumentationPoint == nullptr);
        }

        TEST_METHOD(no_method_match)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyOtherMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::IsTrue(instrumentationPoint == nullptr);
        }

        TEST_METHOD(multiple_xml_files)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"a", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            xmlSet->emplace(L"b", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyOtherMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto function = std::make_shared<MethodRewriter::Test::MockFunction>();
            
            // see if a match for the method in the first file is found
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsFalse(instrumentationPoint == nullptr);

            // see if a match for the method in the second file is found
            function->_functionName = L"MyOtherMethod";
            auto instrumentationPoint2 = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsFalse(instrumentationPoint2 == nullptr);
        }

        TEST_METHOD(multiple_matchers)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                                <exactMethodMatcher methodName=\"MyOtherMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto function = std::make_shared<MethodRewriter::Test::MockFunction>();
            
            // see if a match for the first method is found
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsFalse(instrumentationPoint == nullptr);

            // see if a match for the second method is found
            function->_functionName = L"MyOtherMethod";
            auto instrumentationPoint2 = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsFalse(instrumentationPoint2 == nullptr);
        }

        TEST_METHOD(multiple_matches)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                            <match assemblyName=\"MyOtherAssembly\" className=\"MyOtherClass\">\
                                <exactMethodMatcher methodName=\"MyOtherMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto function = std::make_shared<MethodRewriter::Test::MockFunction>();
            
            // see if a match for the first method is found
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsFalse(instrumentationPoint == nullptr);

            // see if a match for the second method is found
            function->_assemblyName = L"MyOtherAssembly";
            function->_typeName = L"MyOtherClass";
            function->_functionName = L"MyOtherMethod";
            auto instrumentationPoint2 = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsFalse(instrumentationPoint2 == nullptr);
        }

        TEST_METHOD(one_match_one_nomatch)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                            <match assemblyName=\"MyOtherAssembly\" className=\"MyOtherClass\">\
                                <exactMethodMatcher methodName=\"MyUnmatchedMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto function = std::make_shared<MethodRewriter::Test::MockFunction>();
            
            // see if a match for the first method is found
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsFalse(instrumentationPoint == nullptr);

            // see if a match for the second method is found
            function->_assemblyName = L"MyOtherAssembly";
            function->_typeName = L"MyOtherClass";
            function->_functionName = L"MyOtherMethod";
            auto instrumentationPoint2 = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsTrue(instrumentationPoint2 == nullptr);
        }

        TEST_METHOD(with_matching_parameters)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\" parameters=\"MyNamespace.MyTypeName\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::IsFalse(instrumentationPoint == nullptr);
        }

        TEST_METHOD(with_nonmatching_parameters)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\" parameters=\"MyOtherNamespace.MyTypeName\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::IsTrue(instrumentationPoint == nullptr);
        }

        TEST_METHOD(tracer_factory_name)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory name=\"Foo.Bar.Factory\">\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::AreEqual(std::wstring(L"Foo.Bar.Factory"), instrumentationPoint->TracerFactoryName);
        }

        TEST_METHOD(metric_name)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory metricName=\"My/Metric/Name\">\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::AreEqual(std::wstring(L"My/Metric/Name"), instrumentationPoint->MetricName);
        }

        TEST_METHOD(transaction_naming_priority)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory transactionNamingPriority=\"2\">\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::AreEqual(uint32_t(0x2), uint32_t(instrumentationPoint->TracerFactoryArgs >> 24) & 0x7);
        }

        TEST_METHOD(tracer_factory_level)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory level=\"2\">\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::AreEqual(uint32_t(0x2), uint32_t(instrumentationPoint->TracerFactoryArgs >> 16) & 0x7);
        }

        TEST_METHOD(metric_name_instance)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory metricName=\"instance\">\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::IsTrue((instrumentationPoint->TracerFactoryArgs & TracerFlags::UseInvocationTargetClassName) != 0);
        }

        TEST_METHOD(tracer_args_default)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory metricName=\"instance\">\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::IsTrue((instrumentationPoint->TracerFactoryArgs & (TracerFlags::GenerateScopedMetric | TracerFlags::SuppressRecursiveCalls | TracerFlags::TransactionTracerSegment)) != 0);
        }

        TEST_METHOD(multiple_class_matcher)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory metricName=\"instance\">\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass,MyNamespace.MyOtherClass,MyNamespace.MyThirdClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto function = std::make_shared<MethodRewriter::Test::MockFunction>();

            function->_typeName = L"MyNamespace.MyClass";
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsNotNull(instrumentationPoint.get());

            function->_typeName = L"MyNamespace.MyOtherClass";
            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsNotNull(instrumentationPoint.get());

            function->_typeName = L"MyNamespace.MyThirdClass";
            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsNotNull(instrumentationPoint.get());

            function->_typeName = L"MyNamespace.MyFourthClass";
            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsNull(instrumentationPoint.get());
        }

        TEST_METHOD(multiple_generic_class_matcher)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory metricName=\"instance\">\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass[A,B],MyNamespace.MyOtherClass<A<B,C>,D>,MyNamespace.MyThirdClass<A,B,C[D[E[F],G],H],I>\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto function = std::make_shared<MethodRewriter::Test::MockFunction>();

            function->_typeName = L"MyNamespace.MyClass[A,B]";
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsNotNull(instrumentationPoint.get());

            function->_typeName = L"MyNamespace.MyOtherClass<A<B,C>,D>";
            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsNotNull(instrumentationPoint.get());

            function->_typeName = L"MyNamespace.MyThirdClass<A,B,C[D[E[F],G],H],I>";
            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsNotNull(instrumentationPoint.get());

            function->_typeName = L"MyNamespace.MyFourthClass";
            instrumentationPoint = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsNull(instrumentationPoint.get());
        }

        TEST_METHOD(when_matcher_has_no_parameters_then_all_overloads_are_matched)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::IsFalse(instrumentationPoint == nullptr);
        }

        TEST_METHOD(when_matcher_has_empty_parameter_string_then_parameterized_overload_does_not_match)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\" parameters=\"\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::IsTrue(instrumentationPoint == nullptr);
        }

        TEST_METHOD(when_matcher_has_void_parameter_string_then_parameterized_overload_does_not_match)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\" parameters=\"void\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::IsTrue(instrumentationPoint == nullptr);
        }

        TEST_METHOD(when_matcher_has_no_parameter_string_then_unparameterized_overload_matches)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto function = std::make_shared<MethodRewriter::Test::MockFunction>();
            BYTEVECTOR(signatureBytes,
                0x00, // default calling convention
                0x00, // 0 parameters
                0x01, // void return
                );
            function->_signature = std::make_shared<ByteVector>(signatureBytes);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsFalse(instrumentationPoint == nullptr);
        }

        TEST_METHOD(when_matcher_has_empty_parameter_string_then_unparameterized_overload_matches)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\" parameters=\"\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto function = std::make_shared<MethodRewriter::Test::MockFunction>();
            BYTEVECTOR(signatureBytes,
                0x00, // default calling convention
                0x00, // 0 parameters
                0x01, // void return
                );
            function->_signature = std::make_shared<ByteVector>(signatureBytes);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsFalse(instrumentationPoint == nullptr);
        }

        TEST_METHOD(when_matcher_has_void_parameter_string_then_unparameterized_overload_matches)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\" parameters=\"void\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto function = std::make_shared<MethodRewriter::Test::MockFunction>();
            BYTEVECTOR(signatureBytes,
                0x00, // default calling convention
                0x00, // 0 parameters
                0x01, // void return
                );
            function->_signature = std::make_shared<ByteVector>(signatureBytes);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsFalse(instrumentationPoint == nullptr);
        }

        TEST_METHOD(when_matcher_has_string_parameter_string_then_unparameterized_overload_does_not_match)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\" parameters=\"System.String\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto function = std::make_shared<MethodRewriter::Test::MockFunction>();
            BYTEVECTOR(signatureBytes,
                0x00, // default calling convention
                0x00, // 0 parameters
                0x01, // void return
                );
            function->_signature = std::make_shared<ByteVector>(signatureBytes);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsTrue(instrumentationPoint == nullptr);
        }

        TEST_METHOD(when_matcher_has_multiple_parameter_string_then_parameterized_overload_matches)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\" parameters=\"System.String, System.Int32\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");
            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            auto function = std::make_shared<MethodRewriter::Test::MockFunction>();
            BYTEVECTOR(signatureBytes,
                0x00, // default calling convention
                0x02, // 2 parameters
                0x01, // void return
                0x0E, // 1st parameter type System.String
                0x08, // 2nd parameter type System.Int32
                );
            function->_signature = std::make_shared<ByteVector>(signatureBytes);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(function);
            Assert::IsFalse(instrumentationPoint == nullptr);
        }

        TEST_METHOD(ignored_instrumentation_should_not_have_instrumentation_points)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"\
                <?xml version=\"1.0\" encoding=\"utf-8\"?>\
                <extension>\
                    <instrumentation>\
                        <tracerFactory>\
                            <match assemblyName=\"MyAssembly\" className=\"MyNamespace.MyClass\">\
                                <exactMethodMatcher methodName=\"MyMethod\"/>\
                            </match>\
                        </tracerFactory>\
                    </instrumentation>\
                </extension>\
                ");

            IgnoreInstrumentationListPtr ignoreList(new IgnoreInstrumentationList());
            ignoreList->push_back(std::make_shared<IgnoreInstrumentation>(L"MyAssembly", L"MyNamespace.MyClass"));

            InstrumentationConfiguration instrumentation(xmlSet, ignoreList);
            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::IsTrue(instrumentationPoint == nullptr);
        }

        TEST_METHOD(set_lambda_instrumentation_point_success)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"<?xml version=\"1.0\" encoding=\"utf-8\"?>");

            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            instrumentation.AddInstrumentationPointToCollectionFromEnvironment(_X("MyAssembly::MyNamespace.MyClass::MyMethod"));

            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::IsFalse(instrumentationPoint == nullptr);
        }

        TEST_METHOD(set_lambda_instrumentation_point_failure)
        {
            InstrumentationXmlSetPtr xmlSet(new InstrumentationXmlSet());
            xmlSet->emplace(L"filename", L"<?xml version=\"1.0\" encoding=\"utf-8\"?>");

            InstrumentationConfiguration instrumentation(xmlSet, nullptr);
            instrumentation.AddInstrumentationPointToCollectionFromEnvironment(_X("MyAssembly::MyNamespace.MyClass::"));
            instrumentation.AddInstrumentationPointToCollectionFromEnvironment(_X("MyAssembly::MyNamespace.MyClass::WrongMethod"));
            instrumentation.AddInstrumentationPointToCollectionFromEnvironment(_X("MyAssembly::MyMethod"));
            instrumentation.AddInstrumentationPointToCollectionFromEnvironment(_X("MyNamespace.MyClass:MyMethod"));
            instrumentation.AddInstrumentationPointToCollectionFromEnvironment(_X("MyMethod"));
            instrumentation.AddInstrumentationPointToCollectionFromEnvironment(_X(":::MyMethod"));
            instrumentation.AddInstrumentationPointToCollectionFromEnvironment(_X("::::::MyMethod"));
            instrumentation.AddInstrumentationPointToCollectionFromEnvironment(_X(":::MyMethod::"));
            instrumentation.AddInstrumentationPointToCollectionFromEnvironment(_X("MyAssembly:MyNamespace.MyClass:MyMethod"));
            instrumentation.AddInstrumentationPointToCollectionFromEnvironment(_X("MyAssembly/MyNamespace.MyClass/MyMethod"));
            instrumentation.AddInstrumentationPointToCollectionFromEnvironment(_X("MyAssembly_MyNamespace.MyClass_MyMethod"));
            instrumentation.AddInstrumentationPointToCollectionFromEnvironment(_X("MyAssembly MyNamespace.MyClass MyMethod"));
            instrumentation.AddInstrumentationPointToCollectionFromEnvironment(_X(" MyAssembly::MyNamespace.MyClass:MyMethod"));
            instrumentation.AddInstrumentationPointToCollectionFromEnvironment(_X("MyAssembly::MyNamespace.MyClass::MyMethod"));
            instrumentation.AddInstrumentationPointToCollectionFromEnvironment(_X("MyAssembly::MyNamespace .MyClass:: MyMethod"));

            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            Assert::IsFalse(instrumentationPoint == nullptr);
        }
    };
}}}}
