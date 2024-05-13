// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include "stdafx.h"
#include "CppUnitTest.h"
#include "ConfigurationTestTemplates.h"
#include "../Configuration/Configuration.h"
#include "../Configuration/InstrumentationConfiguration.h"
#include "../sicily/SicilyTest/RealisticTokenizer.h"
#include "../MethodRewriterTest/MockFunction.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace Configuration { namespace Test
{
    TEST_CLASS(IgnoreInstrumentationTest)
    {
    public:
        TEST_METHOD(ignore_assembly_and_class)
        {
            std::wstring configurationXml(L"\
                <?xml version=\"1.0\"?>\
                <configuration>\
                    <instrumentation>\
                        <rules>\
                            <ignore assemblyName='MyAssembly' className='MyNamespace.MyClass'/>\
                        </rules>\
                    </instrumentation>\
                </configuration>\
                ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            auto ignoreList = configuration.GetIgnoreInstrumentationList();
            Assert::AreEqual((size_t)1, ignoreList->size());
            Assert::IsTrue(IgnoreInstrumentation::Matches(ignoreList, _X("MyAssembly"), _X("MyNamespace.MyClass")));
            Assert::IsTrue(IgnoreInstrumentation::Matches(ignoreList, _X("myassembly"), _X("mynamespace.myclass")));
            Assert::IsFalse(IgnoreInstrumentation::Matches(ignoreList, _X("my"), _X("MyNamespace.MyClass")));
            Assert::IsFalse(IgnoreInstrumentation::Matches(ignoreList, _X("myassembly"), _X("mynamespacemyclass")));
            Assert::IsFalse(IgnoreInstrumentation::Matches(ignoreList, _X(""), _X("MyNamespace.MyClass")));
            Assert::IsFalse(IgnoreInstrumentation::Matches(ignoreList, _X("anotherassembly"), _X("MyNamespace.MyClass")));
            Assert::IsFalse(IgnoreInstrumentation::Matches(ignoreList, _X(""), _X("")));
            Assert::IsFalse(IgnoreInstrumentation::Matches(ignoreList, _X("myassembly"), _X("foo")));
            Assert::IsFalse(IgnoreInstrumentation::Matches(ignoreList, _X("myassembly"), _X("M")));

            auto xmlSet = GetInstrumentationXmlSet();
            InstrumentationConfiguration instrumentation(xmlSet, ignoreList, nullptr);

            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            // Should fail to find the instrumentation because it's in the ignore list
            Assert::IsTrue(instrumentationPoint == nullptr);
        }

        TEST_METHOD(ignore_assembly)
        {
            std::wstring configurationXml(L"\
                <?xml version=\"1.0\"?>\
                <configuration>\
                    <instrumentation>\
                        <rules>\
                            <ignore assemblyName='MyAssembly' />\
                        </rules>\
                    </instrumentation>\
                </configuration>\
                ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            auto ignoreList = configuration.GetIgnoreInstrumentationList();
            Assert::AreEqual((size_t)1, ignoreList->size());
            Assert::IsTrue(IgnoreInstrumentation::Matches(ignoreList, _X("MyAssembly"), _X("bar")));
            Assert::IsTrue(IgnoreInstrumentation::Matches(ignoreList, _X("MyAssembly"), _X("")));
            Assert::IsTrue(IgnoreInstrumentation::Matches(ignoreList, _X("MyAssembly"), _X("foo")));
            Assert::IsTrue(IgnoreInstrumentation::Matches(ignoreList, _X("MyAssembly"), _X("someclass")));
            Assert::IsFalse(IgnoreInstrumentation::Matches(ignoreList, _X("alpha"), _X("bar")));
            Assert::IsFalse(IgnoreInstrumentation::Matches(ignoreList, _X(""), _X("")));

            auto xmlSet = GetInstrumentationXmlSet();
            InstrumentationConfiguration instrumentation(xmlSet, ignoreList, nullptr);

            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            // Should fail to find the instrumentation because it's in the ignore list
            Assert::IsTrue(instrumentationPoint == nullptr);
        }

        TEST_METHOD(ignore_class)
        {
            // Matching by just class name isn't allowed, so these should all fail
            std::wstring configurationXml(L"\
                <?xml version=\"1.0\"?>\
                <configuration>\
                    <instrumentation>\
                        <rules>\
                            <ignore className='MyNamespace.MyClass' />\
                        </rules>\
                    </instrumentation>\
                </configuration>\
                ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            auto ignoreList = configuration.GetIgnoreInstrumentationList();
            Assert::AreEqual((size_t)0, ignoreList->size());
            Assert::IsFalse(IgnoreInstrumentation::Matches(ignoreList, _X("foo"), _X("MyNamespace.MyClass")));
            Assert::IsFalse(IgnoreInstrumentation::Matches(ignoreList, _X("alpha"), _X("MyNamespace")));
            Assert::IsFalse(IgnoreInstrumentation::Matches(ignoreList, _X(""), _X("MyNamespace.MyClass")));

            auto xmlSet = GetInstrumentationXmlSet();
            InstrumentationConfiguration instrumentation(xmlSet, ignoreList, nullptr);

            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            // Should find the instrumentation because the ignore list is invalid
            Assert::IsTrue(instrumentationPoint != nullptr);
        }

        TEST_METHOD(ignore_multiple)
        {
            std::wstring configurationXml(L"\
                <?xml version=\"1.0\"?>\
                <configuration>\
                    <instrumentation>\
                        <rules>\
                            <ignore assemblyName='MyAssembly' className='MyNamespace.MyClass'/>\
                            <ignore assemblyName='alpha' />\
                            <ignore assemblyName='foo' className='bar'/>\
                        </rules>\
                    </instrumentation>\
                </configuration>\
                ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            auto ignoreList = configuration.GetIgnoreInstrumentationList();
            Assert::AreEqual((size_t)3, ignoreList->size());
            Assert::IsTrue(IgnoreInstrumentation::Matches(ignoreList, _X("foo"), _X("bar")));
            Assert::IsTrue(IgnoreInstrumentation::Matches(ignoreList, _X("myassembly"), _X("mynamespace.myclass")));
            Assert::IsTrue(IgnoreInstrumentation::Matches(ignoreList, _X("alpha"), _X("")));
            Assert::IsTrue(IgnoreInstrumentation::Matches(ignoreList, _X("alpha"), _X("something")));
            Assert::IsFalse(IgnoreInstrumentation::Matches(ignoreList, _X("foo"), _X("")));
            Assert::IsFalse(IgnoreInstrumentation::Matches(ignoreList, _X("myassembly"), _X("")));
            Assert::IsFalse(IgnoreInstrumentation::Matches(ignoreList, _X("foo"), _X("MyNamespace.MyClass")));
            Assert::IsFalse(IgnoreInstrumentation::Matches(ignoreList, _X("different"), _X("MyNamespace.MyClass")));

            auto xmlSet = GetInstrumentationXmlSet();
            InstrumentationConfiguration instrumentation(xmlSet, ignoreList, nullptr);

            auto instrumentationPoint = instrumentation.TryGetInstrumentationPoint(std::make_shared<MethodRewriter::Test::MockFunction>());
            // Should fail to find the instrumentation because it's in the ignore list
            Assert::IsTrue(instrumentationPoint == nullptr);
        }

    private:
        const std::pair<xstring_t, bool> _missingAgentEnabledConfigPair = std::make_pair(L"<?xml version=\"1.0\"?><configuration/>", false);

        InstrumentationXmlSetPtr GetInstrumentationXmlSet()
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
            return xmlSet;
        }
    };
} } } }
