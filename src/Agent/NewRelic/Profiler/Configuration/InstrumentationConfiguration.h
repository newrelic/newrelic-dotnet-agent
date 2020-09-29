/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <memory>
#include <string>
#include <map>
#include "../Logging/Logger.h"
#include "InstrumentationPoint.h"
#include "TracerFlags.h"
#include "../MethodRewriter/IFunction.h"
#include "../SignatureParser/SignatureParser.h"
#include "../RapidXML/rapidxml.hpp"

namespace NewRelic { namespace Profiler { namespace Configuration
{
    // a map of file name to the contents of the xml file
    typedef std::map<xstring_t, xstring_t> InstrumentationXmlSet;
    typedef std::shared_ptr<InstrumentationXmlSet> InstrumentationXmlSetPtr;

    class InstrumentationConfiguration
    {
    public:
        InstrumentationConfiguration(InstrumentationXmlSetPtr instrumentationXmls) :
            _instrumentationPoints(new InstrumentationPointSet()),
            _invalidFileCount(0)
        {
            // pull instrumentation points from every xml string
            for (auto instrumentationXml : *instrumentationXmls)
            {
                try
                {
                    LogDebug(L"Parsing instrumentation file '", instrumentationXml.first);
                    GetInstrumentationPoints(instrumentationXml.second);
                }
                catch (...)
                {
                    _invalidFileCount++;
                    // if an exception is thrown while parsing a file just move on to the next one
                    LogWarn(L"Exception thrown while attempting to parse instrumentation file '", instrumentationXml.first, L"'. Please validate your instrumentation files against extensions/extension.xsd or contact New Relic support.");
                    continue;
                }
            }
        }

        InstrumentationConfiguration(InstrumentationPointSetPtr instrumentationPoints) :
            _instrumentationPoints(instrumentationPoints),
            _invalidFileCount(0)
        {}

        InstrumentationPointSetPtr GetInstrumentationPoints() {
            return _instrumentationPoints;
        }

        uint16_t GetInvalidFileCount()
        {
            return _invalidFileCount;
        }

        InstrumentationPointPtr TryGetInstrumentationPoint(const MethodRewriter::IFunctionPtr function) const
        {
            SignatureParser::MethodSignaturePtr methodSignature = SignatureParser::SignatureParser::ParseMethodSignature(function->GetSignature()->begin(), function->GetSignature()->end());
            InstrumentationPointPtr ipToFind(new InstrumentationPoint());
            ipToFind->AssemblyName = function->GetAssemblyName();
            ipToFind->ClassName = function->GetTypeName();
            ipToFind->MethodName = function->GetFunctionName();
            ipToFind->Parameters = std::unique_ptr<xstring_t>(new xstring_t(methodSignature->ToString(function->GetTokenResolver())));

            if (Strings::AreEqualCaseInsensitive(function->GetAssemblyName(), _X("System.Net.Http"))
                && ((function->GetAssemblyProps().usMajorVersion >= 5 && Strings::AreEqualCaseInsensitive(function->GetTypeName(), _X("System.Net.Http.HttpClient"))) || (function->GetAssemblyProps().usMajorVersion < 5 && Strings::AreEqualCaseInsensitive(function->GetTypeName(), _X("System.Net.Http.SocketsHttpHandler")))))
            {
                return nullptr;
            }

            auto instPoint = TryGetInstrumentationPoint(ipToFind);
            return instPoint;
        }

    private:

        InstrumentationPointPtr TryGetInstrumentationPoint(const InstrumentationPointPtr ipToFind) const
        {
            for (auto current : *_instrumentationPoints)
            {
                if (current == ipToFind)
                {
                    return current;
                }
            }

            return nullptr;
        }

        void GetInstrumentationPoints(xstring_t instrumentationXml)
        {
            rapidxml::xml_document<xchar_t> document;
            document.parse<rapidxml::parse_trim_whitespace | rapidxml::parse_normalize_whitespace>(const_cast<xchar_t*>(instrumentationXml.c_str()));
            auto extensionNode = document.first_node(_X("extension"), 0, false);
            if (extensionNode == nullptr)
            {
                LogWarn(L"extension node not found in instrumentation file. Please validate your instrumentation files against extensions/extension.xsd or contact New Relic support.");
                return;
            }

            auto instrumentationNode = extensionNode->first_node(_X("instrumentation"), 0, false);
            if (instrumentationNode == nullptr)
            {
                LogWarn(L"instrumentation node not found in instrumentation file. Please validate your instrumentation files against extensions/extension.xsd or contact New Relic support.");
                return;
            }
            
            for (auto tracerFactoryNode = instrumentationNode->first_node(_X("tracerFactory"), 0, false); tracerFactoryNode; tracerFactoryNode = tracerFactoryNode->next_sibling(_X("tracerFactory"), 0, false))
            {
                GetInstrumentationPointsForTracer(tracerFactoryNode);
            }
        }

        void GetInstrumentationPointsForTracer(rapidxml::xml_node<xchar_t>* tracerFactoryNode)
        {
            // if this tracer factory isn't enabled then bail
            auto enabled = GetAttributeOrEmptyString(tracerFactoryNode, _X("enabled"));
            if (Strings::AreEqualCaseInsensitive(enabled, _X("false")))
            {
                return;
            }

            // get the instrumentation points for every match node in this tracer factory
            for (auto matchNode = tracerFactoryNode->first_node(_X("match"), 0, false); matchNode; matchNode = matchNode->next_sibling(_X("match"), 0, false))
            {
                GetInstrumentationPointsForMatch(matchNode);
            }
        }

        void GetInstrumentationPointsForMatch(rapidxml::xml_node<xchar_t>* matchNode)
        {
            // get the instrumentation points for every matcher node in this tracer factory
            for (auto matcherNode = matchNode->first_node(_X("exactMethodMatcher"), 0, false); matcherNode; matcherNode = matcherNode->next_sibling(_X("exactMethodMatcher"), 0, false))
            {
                GetInstrumentationPointForMatcher(matcherNode);
            }
        }

        void GetInstrumentationPointForMatcher(rapidxml::xml_node<xchar_t>* matcherNode)
        {
            InstrumentationPointPtr instrumentationPoint(new InstrumentationPoint());

            auto matchNode = matcherNode->parent();
            auto tracerNode = matchNode->parent();

            // get all the attributes of interest from the XML
            instrumentationPoint->TracerFactoryName = GetAttributeOrEmptyString(tracerNode, _X("name"));
            instrumentationPoint->MetricName = GetAttributeOrEmptyString(tracerNode, _X("metricName"));
            instrumentationPoint->MetricType = GetAttributeOrEmptyString(tracerNode, _X("metric"));
            auto levelString = GetAttributeOrEmptyString(tracerNode, _X("level"));
            auto suppressRecursiveCallsString = GetAttributeOrEmptyString(tracerNode, _X("level"));
            auto transactionTraceSegmentString = GetAttributeOrEmptyString(tracerNode, _X("transactionTraceSegment"));
            auto transactionNamingPriorityString = GetAttributeOrEmptyString(tracerNode, _X("transactionNamingPriority"));
            instrumentationPoint->AssemblyName = GetAttributeOrEmptyString(matchNode, _X("assemblyName"));
            instrumentationPoint->ClassName = GetAttributeOrEmptyString(matchNode, _X("className"));
            instrumentationPoint->MethodName = GetAttributeOrEmptyString(matcherNode, _X("methodName"));
            instrumentationPoint->Parameters = TryGetAttribute(matcherNode, _X("parameters"));

            // sdaubin : I'm sure we could allow some mscorlib methods to be instrumented because we're able to 
            // append methods onto an mscorlib exception class.  But we'd need to do something like we do for those
            // exception helper methods and remove the `mscorlib` lookups.  Right now we try to find a reference
            // to the mscorlib library as we build class tokens, and mscorlib does not reference itself.
            // But the safest thing to do is disallow mscorlib instrumentation and make that very clear to users.
            if (instrumentationPoint->AssemblyName == _X("mscorlib"))
            {
                LogWarn(L"Skipping instrumentation targeted at the mscorlib assembly for class ", instrumentationPoint->ClassName);
                return;
            }

            // default the tracer factory name if one was not found
            if (instrumentationPoint->TracerFactoryName.empty())
            {
                instrumentationPoint->TracerFactoryName = _X("NewRelic.Agent.Core.Tracer.Factories.DefaultTracerFactory");
            }

            // populate the TracerFactoryArgs
            instrumentationPoint->TracerFactoryArgs = 0;

            // metric type flags
            if (!instrumentationPoint->MetricType.empty())
            {
                if (Strings::AreEqualCaseInsensitive(instrumentationPoint->MetricType, _X("both")))
                {
                    instrumentationPoint->TracerFactoryArgs |= TracerFlags::GenerateScopedMetric | TracerFlags::GenerateUnscopedMetric;
                }
                else if (Strings::AreEqualCaseInsensitive(instrumentationPoint->MetricType, _X("scoped")))
                {
                    instrumentationPoint->TracerFactoryArgs |= TracerFlags::GenerateScopedMetric;
                }
                else if (Strings::AreEqualCaseInsensitive(instrumentationPoint->MetricType, _X("unscoped")))
                {
                    instrumentationPoint->TracerFactoryArgs |= TracerFlags::GenerateUnscopedMetric;
                }
            }
            else
            {
                // default if no MetricType supplied
                instrumentationPoint->TracerFactoryArgs |= TracerFlags::GenerateScopedMetric;
            }

            // metric name flags
            if (!instrumentationPoint->MetricName.empty())
            {
                if (Strings::AreEqualCaseInsensitive(instrumentationPoint->MetricName, _X("instance"))) 
                {
                    instrumentationPoint->TracerFactoryArgs |= TracerFlags::UseInvocationTargetClassName;
                }

                instrumentationPoint->TracerFactoryArgs |= TracerFlags::CustomMetricName;
            }

            // level
            if (!levelString.empty())
            {
                // bits 18..16 hold a 3-bit instrumentation level for this instrumenter
                int level = xstoi(levelString);
                level &= 0x7;
                instrumentationPoint->TracerFactoryArgs |= (level << 16);
            }

            // suppress recusive call flag
            if (!suppressRecursiveCallsString.empty())
            {
                if (Strings::AreEqualCaseInsensitive(suppressRecursiveCallsString, _X("true"))) 
                {
                    instrumentationPoint->TracerFactoryArgs |= TracerFlags::SuppressRecursiveCalls;
                }
            }
            else
            {
                instrumentationPoint->TracerFactoryArgs |= TracerFlags::SuppressRecursiveCalls;
            }

            // transaction trace segment flag
            if (!transactionTraceSegmentString.empty())
            {
                if (Strings::AreEqualCaseInsensitive(transactionTraceSegmentString, _X("true")))
                {
                    instrumentationPoint->TracerFactoryArgs |= TracerFlags::TransactionTracerSegment;
                }
            }
            else
            {
                instrumentationPoint->TracerFactoryArgs |= TracerFlags::TransactionTracerSegment;
            }

            // transaction naming priority
            if (!transactionNamingPriorityString.empty())
            {
                // bits 24..26 hold a 3-bit naming priority for this instrumenter
                int transactionNamingPriority = xstoi(transactionNamingPriorityString);
                transactionNamingPriority &= 0x7;
                instrumentationPoint->TracerFactoryArgs |= (transactionNamingPriority << 24);
            }

            // if the ClassName includes multiple classes, we have to split this into multiple instrumentation points
            auto instrumentationPoints = SplitInstrumentationPointsOnClassNames(instrumentationPoint);

            for (auto iPoint : instrumentationPoints) {
                // check if this method has already been instrumented -- if so, log a warning
                auto existingInstrumentation = TryGetInstrumentationPoint(iPoint);
                if (existingInstrumentation != nullptr)
                {
                    LogInfo(L"Duplicate instrumentation for ", existingInstrumentation->ToString(), " was found and will be ignored");
                    continue;
                }

                // finally add the new instrumentation point(s) to our set of instrumentation points
                _instrumentationPoints->insert(iPoint);
            }
        }

        // the class name field of an instrumentation point may have multiple clasess listed (comma separated), we have to build instrumentation points for each
        static std::set<InstrumentationPointPtr> SplitInstrumentationPointsOnClassNames(InstrumentationPointPtr instrumentationPoint)
        {
            std::set<InstrumentationPointPtr> instrumentationPoints;

            // look for commas outside matching brackets and split on them
            uint32_t bracketLevel = 0;
            auto classNameBeginPosition = instrumentationPoint->ClassName.cbegin();
            for (auto character = instrumentationPoint->ClassName.cbegin(); character != instrumentationPoint->ClassName.cend(); ++character)
            {
                if (*character == L'<' || *character == L'[')
                {
                    ++bracketLevel;
                }

                if (*character == L'>' || *character == L']')
                {
                    --bracketLevel;
                }

                if (bracketLevel == 0 && *character == L',')
                {
                    // comma was found outside of brackets, spin off a new instrumentation point from the previous class name
                    auto newInstrumentationPoint = GetInstrumentationPointFromClassSplitIterators(instrumentationPoint, classNameBeginPosition, character);
                    instrumentationPoints.insert(newInstrumentationPoint);
                    classNameBeginPosition = character + 1;
                }
            }

            // add one last instrumentation point for the last split section
            auto newInstrumentationPoint = GetInstrumentationPointFromClassSplitIterators(instrumentationPoint, classNameBeginPosition, instrumentationPoint->ClassName.end());
            instrumentationPoints.emplace(newInstrumentationPoint);

            return instrumentationPoints;
        }

        // given two instrumentationPoint->ClassName iterators (begin and end), return a new copy of insrtumentationPoint containing a ClassName of [begin, end)
        static InstrumentationPointPtr GetInstrumentationPointFromClassSplitIterators(InstrumentationPointPtr instrumentationPoint, xstring_t::const_iterator begin, xstring_t::const_iterator end)
        {
            // construct a new class name string from this section of the old class name split
            xstring_t newClassName(begin, end);

            // construct a new instrumentation point from the old
            auto newInstrumentationPoint = std::make_shared<InstrumentationPoint>(*instrumentationPoint);

            // set the split class name as the class name of the new instrumentation point
            newInstrumentationPoint->ClassName = newClassName;

            return newInstrumentationPoint;
        }

        static xstring_t GetAttributeOrEmptyString(rapidxml::xml_node<xchar_t>* node, const xchar_t* attributeName)
        {
            auto attributeValue = TryGetAttribute(node, attributeName);
            if (attributeValue == nullptr)
            {
                return xstring_t();
            }

            return *attributeValue;
        }

        static std::unique_ptr<xstring_t> TryGetAttribute(rapidxml::xml_node<xchar_t>* node, const xchar_t* attributeName)
        {
            auto attribute = node->first_attribute(attributeName, 0, false);
            if (attribute == nullptr)
                return nullptr;

            return std::unique_ptr<xstring_t>(new xstring_t(attribute->value())); 
        }

    private:
        InstrumentationPointSetPtr _instrumentationPoints;
        uint16_t _invalidFileCount;
    };
    typedef std::shared_ptr<InstrumentationConfiguration> InstrumentationConfigurationPtr;
}}}
