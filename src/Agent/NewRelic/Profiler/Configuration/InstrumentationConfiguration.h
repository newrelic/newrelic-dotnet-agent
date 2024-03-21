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
#include "../Common/AssemblyVersion.h"
#include "IgnoreInstrumentation.h"
#include "../Configuration/Strings.h"
#include "../Logging/DefaultFileLogLocation.h"

namespace NewRelic { namespace Profiler { namespace Configuration
{
    // a map of file name to the contents of the xml file
    typedef std::map<xstring_t, xstring_t> InstrumentationXmlSet;
    typedef std::shared_ptr<InstrumentationXmlSet> InstrumentationXmlSetPtr;

    class InstrumentationConfiguration
    {
    public:
        InstrumentationConfiguration(InstrumentationXmlSetPtr instrumentationXmls, IgnoreInstrumentationListPtr ignoreList, std::shared_ptr<NewRelic::Profiler::Logger::IFileDestinationSystemCalls> systemCalls = nullptr) :
            _instrumentationPointsSet(new InstrumentationPointSet())
            , _ignoreList(ignoreList)
            , _systemCalls(systemCalls)
            , _foundServerlessInstrumentationPoint(false)
        {
            // pull instrumentation points from every xml string
            for (auto instrumentationXml : *instrumentationXmls)
            {
                try
                {
                    if (InstrumentationXmlIsDeprecated(instrumentationXml.first))
                    {
                        LogWarn("Deprecated instrumentation file being ignored: ", instrumentationXml.first);
                    }
                    else
                    {
                        LogDebug(L"Parsing instrumentation file '", instrumentationXml.first);
                        GetInstrumentationPoints(instrumentationXml.second);
                    }                    
                }
                catch (...)
                {
                    _invalidFileCount++;
                    // if an exception is thrown while parsing a file just move on to the next one
                    LogWarn(L"Exception thrown while attempting to parse instrumentation file '", instrumentationXml.first, L"'. Please validate your instrumentation files against extensions/extension.xsd or contact New Relic support.");
                    continue;
                }
            }
            LogInfo("Identified ", _instrumentationPointsSet->size(), " Instrumentation points (not ignored) in .xml files");
        }

        InstrumentationConfiguration(InstrumentationPointSetPtr instrumentationPoints, IgnoreInstrumentationListPtr ignoreList) :
            _instrumentationPointsSet(new InstrumentationPointSet())
            , _ignoreList(ignoreList)
            , _systemCalls(nullptr)
            , _foundServerlessInstrumentationPoint(false)
        {
            for (auto instrumentationPoint : *instrumentationPoints)
            {
                AddInstrumentationPointToCollectionsIfNotIgnored(instrumentationPoint);
            }
        }

        uint16_t GetInvalidFileCount()
        {
            return _invalidFileCount;
        }

        InstrumentationPointSetPtr GetInstrumentationPoints() const
        {
            return _instrumentationPointsSet;
        }

        IgnoreInstrumentationListPtr GetIgnoreList() const
        {
            return _ignoreList;
        }

        InstrumentationPointPtr TryGetInstrumentationPoint(const MethodRewriter::IFunctionPtr function) const
        {
            const auto methodSignature = SignatureParser::SignatureParser::ParseMethodSignature(function->GetSignature()->begin(), function->GetSignature()->end());
            const auto params = methodSignature->ToString(function->GetTokenResolver());
            const auto instPoints = TryGetInstrumentationPoints(function->GetAssemblyName(), function->GetTypeName(), function->GetFunctionName(), params);

            if (instPoints.empty())
            {
                // No instrumentation points were found so there is nothing else to check
                return nullptr;
            }

            // We may have multiple matching instrumentation points that target different assembly versions. See if we can find one that meets
            // the version requirements
            AssemblyVersion foundVersion(function->GetAssemblyProps());
            for (auto instPoint : instPoints)
            {
                if ((instPoint->MinVersion != nullptr) && (foundVersion < *instPoint->MinVersion))
                {
                    LogDebug(function->GetAssemblyName(), L" version ", foundVersion.ToString(), L" does not meet minimum version ", instPoint->MinVersion->ToString());
                    continue;
                }

                if ((instPoint->MaxVersion != nullptr) && (foundVersion >= *instPoint->MaxVersion))
                {
                    LogDebug(function->GetAssemblyName(), L" version ", foundVersion.ToString(), L" exceeds maximum version ", instPoint->MaxVersion->ToString());
                    continue;
                }
                // As soon as we find one that passes, return it
                return instPoint;
            }
            return nullptr;
        }

        void CheckForEnvironmentInstrumentationPoint(void)
        {
            if (_foundServerlessInstrumentationPoint || (_systemCalls == nullptr))
            {
                return;
            }

            auto lambdaInstPoint = _systemCalls->TryGetEnvironmentVariable(_X("_HANDLER"));
            if (lambdaInstPoint != nullptr)
            {
                AddInstrumentationPointToCollectionFromEnvironment(*lambdaInstPoint);
                _foundServerlessInstrumentationPoint = true;
                return;
            }

            lambdaInstPoint = _systemCalls->TryGetEnvironmentVariable(_X("NEW_RELIC_LAMBDA_FUNCTION_HANDLER"));
            if (lambdaInstPoint != nullptr)
            {
                AddInstrumentationPointToCollectionFromEnvironment(*lambdaInstPoint);
                _foundServerlessInstrumentationPoint = true;
            }
        }

        void AddInstrumentationPointToCollectionFromEnvironment(xstring_t text)
        {
            auto segments = Strings::Split(text, _X("::"));
            if (segments.size() != 3)
            {
                LogWarn(text, L" is not a valid method descriptor. It must be in the format 'assembly::class::method'");
                return;
            }
            LogInfo(L"Serverless mode detected. Assembly: ", segments[0], L" Class: ", segments[1], L" Method: ", segments[2]);

            InstrumentationPointPtr instrumentationPoint(new InstrumentationPoint());
            // Note that this must exactly match the wrapper name in the managed Agent
            instrumentationPoint->TracerFactoryName = _X("NewRelic.Providers.Wrapper.AwsLambda.HandlerMethod");
            instrumentationPoint->MetricName = _X("");
            instrumentationPoint->MetricType = _X("");
            instrumentationPoint->AssemblyName = segments[0];
            instrumentationPoint->MinVersion = nullptr;
            instrumentationPoint->MaxVersion = nullptr;
            instrumentationPoint->ClassName = segments[1];
            instrumentationPoint->MethodName = segments[2];
            instrumentationPoint->Parameters = nullptr;
            instrumentationPoint->TracerFactoryArgs = 0;

            (*_instrumentationPointsMap)[instrumentationPoint->GetMatchKey()].insert(instrumentationPoint);
            _instrumentationPointsSet->insert(instrumentationPoint);
        }

    private:
        static bool InstrumentationXmlIsDeprecated(xstring_t instrumentationXmlFilePath)
        {
            bool returnValue = false;
            if (NewRelic::Profiler::Strings::ContainsCaseInsensitive(instrumentationXmlFilePath, _X("NewRelic.Providers.Wrapper.Logging.Instrumentation.xml")))
            {
                returnValue = true;
            }
            else if (NewRelic::Profiler::Strings::ContainsCaseInsensitive(instrumentationXmlFilePath, _X("NewRelic.Providers.Wrapper.CastleMonoRail2.Instrumentation.xml")))
            {
                returnValue = true;
            }
            else if (NewRelic::Profiler::Strings::ContainsCaseInsensitive(instrumentationXmlFilePath, _X("NewRelic.Providers.Wrapper.Asp35.Instrumentation.xml")))
            {
                returnValue = true;
            }

            return returnValue;
        }

        InstrumentationPointSet TryGetInstrumentationPoints(
            const xstring_t& assemblyName,
            const xstring_t& className,
            const xstring_t& methodName,
            const xstring_t& parameters) const
        {

            auto matchKey = InstrumentationPoint::GetMatchKey(assemblyName, className, methodName, parameters);
            auto matchInstrumentation = TryGetInstrumentationPoints(matchKey);

            if (!matchInstrumentation.empty())
            {
                return matchInstrumentation;
            }

            matchKey = InstrumentationPoint::GetMatchKey(assemblyName, className, methodName);
            return TryGetInstrumentationPoints(matchKey);
        }

        InstrumentationPointSet TryGetInstrumentationPoints(const InstrumentationPointPtr ipToFind) const
        {
            auto const key = ipToFind->GetMatchKey();

            return TryGetInstrumentationPoints(key);
        }

        InstrumentationPointSet TryGetInstrumentationPoints(const xstring_t& key) const
        {
            auto matches = _instrumentationPointsMap->find(key);
            if (matches == _instrumentationPointsMap->end())
            {
                return InstrumentationPointSet();
            }

            return matches->second;
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
            instrumentationPoint->MinVersion = std::unique_ptr<AssemblyVersion>(AssemblyVersion::Create(GetAttributeOrEmptyString(matchNode, _X("minVersion"))));
            instrumentationPoint->MaxVersion = std::unique_ptr<AssemblyVersion>(AssemblyVersion::Create(GetAttributeOrEmptyString(matchNode, _X("maxVersion"))));
            instrumentationPoint->ClassName = GetAttributeOrEmptyString(matchNode, _X("className"));
            instrumentationPoint->MethodName = GetAttributeOrEmptyString(matcherNode, _X("methodName"));
            instrumentationPoint->Parameters = NormalizeParameters(TryGetAttribute(matcherNode, _X("parameters")));

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

                // finally add the new instrumentation point(s) to our set of instrumentation points
                // Note that there may be "duplicated" instrumentation points that target different assembly versions
                AddInstrumentationPointToCollectionsIfNotIgnored(iPoint);
            }
        }

        void AddInstrumentationPointToCollectionsIfNotIgnored(InstrumentationPointPtr instrumentationPoint)
        {
            if (!IgnoreInstrumentation::Matches(_ignoreList, instrumentationPoint->AssemblyName, instrumentationPoint->ClassName))
            {
                (*_instrumentationPointsMap)[instrumentationPoint->GetMatchKey()].insert(instrumentationPoint);
                _instrumentationPointsSet->insert(instrumentationPoint);
            }
            else
            {
                LogDebug(L"Instrumentation for ", instrumentationPoint->GetMatchKey(), L" is in the ignore list in the newrelic.config file and will be ignored.");
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

        static std::unique_ptr<xstring_t> NormalizeParameters(std::unique_ptr<xstring_t> rawParams)
        {
            if (rawParams == nullptr)
            {
                return nullptr;
            }

            // void as the parameters means parameterless method call (not all overloads)
            if (Strings::AreEqualCaseInsensitive(*(rawParams), _X("void")))
            {
                return std::unique_ptr<xstring_t>(new xstring_t());
            }

            // remove whitespace chars from param list
            rawParams->erase(std::remove_if(rawParams->begin(), rawParams->end(), ::isspace), rawParams->end());

            return rawParams;
        }

    private:
        InstrumentationPointMapPtr _instrumentationPointsMap = InstrumentationPointMapPtr(new InstrumentationPointMap());
        InstrumentationPointSetPtr _instrumentationPointsSet;
        uint16_t _invalidFileCount = 0;
        IgnoreInstrumentationListPtr _ignoreList;
        std::shared_ptr<NewRelic::Profiler::Logger::IFileDestinationSystemCalls> _systemCalls;
        bool _foundServerlessInstrumentationPoint;
    };
    typedef std::shared_ptr<InstrumentationConfiguration> InstrumentationConfigurationPtr;
}}}
