/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

#pragma once

#include "Strings.h"
#include <memory>
#include <list>

namespace NewRelic { namespace Profiler { namespace Configuration
{
    class IgnoreInstrumentation;

    typedef std::shared_ptr<IgnoreInstrumentation> IgnoreInstrumentationPtr;
    typedef std::list<IgnoreInstrumentationPtr> IgnoreInstrumentationList;
    typedef std::shared_ptr<IgnoreInstrumentationList> IgnoreInstrumentationListPtr;

    class IgnoreInstrumentation
    {
        public:
            xstring_t AssemblyName;
            xstring_t ClassName;

            IgnoreInstrumentation() {}
            IgnoreInstrumentation(xstring_t assembly)
            {
                AssemblyName = assembly;
            }
            IgnoreInstrumentation(xstring_t assembly, xstring_t className)
            {
                AssemblyName = assembly;
                ClassName = className;
            }
            static bool Matches(IgnoreInstrumentationListPtr list, xstring_t assembly, xstring_t className)
            {
                if (list == nullptr)
                {
                    return false;
                }
                for (IgnoreInstrumentationPtr item : *list)
                {
                    if (item->Matches(assembly, className))
                    {
                        return true;
                    }
                }
                return false;
            }

        private:
            bool Matches(xstring_t assembly, xstring_t className)
            {
                if (!Strings::AreEqualCaseInsensitive(AssemblyName, assembly))
                {
                    return false;
                }
                if (ClassName.empty())
                {
                    return true;
                }
                return (Strings::AreEqualCaseInsensitive(ClassName, className));
            }
    };

} } }
