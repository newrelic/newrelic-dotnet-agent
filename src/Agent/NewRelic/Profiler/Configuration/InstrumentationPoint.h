/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <string>
#include <vector>
#include <memory>
#include <stdint.h>
#include <set>
#include <map>
#include "Strings.h"
#include "../Logging/Logger.h"
#include "../Common/AssemblyVersion.h"

namespace NewRelic { namespace Profiler { namespace Configuration
{
    class InstrumentationPoint
    {
    public:
        xstring_t TracerFactoryName;                    // represented by 'name' on the <tracerFactory> node
        xstring_t AssemblyName;                         // on the <match> node
        xstring_t ClassName;                            // on the <match> node
        xstring_t MethodName;                           // on the <exactMethodMatcher> node
        std::unique_ptr<xstring_t> Parameters;          // on the <exactMethodMatcher> node
        xstring_t MetricType;                           // represented by 'metric' on the <tracerFactory> node
        xstring_t MetricName;                           // on the <tracerFactory> node
        uint32_t TracerFactoryArgs;
        std::unique_ptr<AssemblyVersion> MinVersion;    // on the <match> node
        std::unique_ptr<AssemblyVersion> MaxVersion;    // on the <match> node

        InstrumentationPoint():
            TracerFactoryArgs(0) { }

        InstrumentationPoint(const InstrumentationPoint& other) :
            TracerFactoryName(other.TracerFactoryName),
            AssemblyName(other.AssemblyName),
            MinVersion((other.MinVersion == nullptr) ? nullptr : new AssemblyVersion(*other.MinVersion)),
            MaxVersion((other.MaxVersion == nullptr) ? nullptr : new AssemblyVersion(*other.MaxVersion)),
            ClassName(other.ClassName),
            MethodName(other.MethodName),
            Parameters((other.Parameters == nullptr) ? nullptr : new xstring_t(*other.Parameters)),
            MetricType(other.MetricType),
            MetricName(other.MetricName),
            TracerFactoryArgs(other.TracerFactoryArgs) { }

        bool operator==(const InstrumentationPoint& other)
        {
            return Strings::AreEqualCaseInsensitive(this->AssemblyName, other.AssemblyName) &&
                Strings::AreEqualCaseInsensitive(this->ClassName, other.ClassName) &&
                Strings::AreEqualCaseInsensitive(this->MethodName, other.MethodName) &&
                ParametersMatch(other);
        }

        xstring_t ToString()
        {
            if (Parameters == nullptr)
            {
                return xstring_t(_X("[")) + AssemblyName + _X("]") + ClassName + _X(".") + MethodName + _X("()");
            } else {
                return xstring_t(_X("[")) + AssemblyName + _X("]") + ClassName + _X(".") + MethodName + _X("(")
                    + *Parameters + _X(")");
            }
        }

        xstring_t GetMatchKey()
        {
            return Parameters == nullptr
                ? GetMatchKey(AssemblyName, ClassName, MethodName)
                : GetMatchKey(AssemblyName, ClassName, MethodName, *Parameters);
        }

        static xstring_t GetMatchKey(const xstring_t& assemblyName, const xstring_t& className, const xstring_t& methodName)
        {
            return xstring_t(_X("[")) + assemblyName + _X("]") + className + _X(".") + methodName;
        }

        static xstring_t GetMatchKey(const xstring_t& assemblyName, const xstring_t& className, const xstring_t& methodName, const xstring_t& parameters)
        {
            return GetMatchKey(assemblyName, className, methodName) + _X("(") + parameters + _X(")");
        }


    private:
        bool ParametersMatch(const InstrumentationPoint& other)
        {
            // nullptr means no parameters attribute was supplied in configuration, suggesting that we should instrument all overloads
            if (this->Parameters == nullptr)
                return true;
            if (other.Parameters == nullptr)
                return true;

            // check for a direct match
            if (Strings::AreEqualCaseInsensitive(*this->Parameters, *other.Parameters))
                return true;

            // if one is 'void' and the other is an empty string then match them (backwards compatability)
            if ((*this->Parameters).empty() && Strings::AreEqualCaseInsensitive(*other.Parameters, _X("void")))
                return true;
            if ((*other.Parameters).empty() && Strings::AreEqualCaseInsensitive(*this->Parameters, _X("void")))
                return true;

            return false;
        }
    };

    typedef std::shared_ptr<InstrumentationPoint> InstrumentationPointPtr;
    typedef std::set<InstrumentationPointPtr> InstrumentationPointSet;
    typedef std::shared_ptr<InstrumentationPointSet> InstrumentationPointSetPtr;

    typedef std::map<xstring_t, InstrumentationPointSet> InstrumentationPointMap;
    typedef std::shared_ptr<InstrumentationPointMap> InstrumentationPointMapPtr;

    inline bool operator==(std::nullptr_t /*leftSide*/, InstrumentationPointPtr rightSide)
    {
        return (rightSide.get() == nullptr);
    }

    inline bool operator==(InstrumentationPointPtr leftSide, std::nullptr_t /*rightSide*/)
    {
        return (leftSide.get() == nullptr);
    }

    inline bool operator==(InstrumentationPointPtr leftSide, InstrumentationPointPtr rightSide)
    {
        return ((*leftSide.get()) == (*rightSide.get()));
    }
}}}
