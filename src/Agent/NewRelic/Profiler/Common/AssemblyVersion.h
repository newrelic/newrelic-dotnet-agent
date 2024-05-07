/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include "../Configuration/Strings.h"
#include <vector>
#include <sstream>
#include <cor.h>
#include <limits>
#include "../Logging/Logger.h"

namespace NewRelic { namespace Profiler
{
    /// <summary>
    /// Class to represent and compare assembly versions
    /// </summary>
    class AssemblyVersion
    {
    public:
        unsigned short Major;
        unsigned short Minor;
        unsigned short Build;
        unsigned short Revision;

        /// <summary>
        /// Constructor
        /// </summary>
        AssemblyVersion(unsigned short major,
            unsigned short minor = 0,
            unsigned short build = 0,
            unsigned short rev = 0)
        {
            Major = major;
            Minor = minor;
            Build = build;
            Revision = rev;
        }

        /// <summary>
        /// Constructor given an existing assembly
        /// </summary>
        /// <param name="metadata">Assembly metadata</param>
        AssemblyVersion(ASSEMBLYMETADATA metadata)
        {
            Major = metadata.usMajorVersion;
            Minor = metadata.usMinorVersion;
            Build = metadata.usBuildNumber;
            Revision = metadata.usRevisionNumber;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other"></param>
        AssemblyVersion(const AssemblyVersion& other)
        {
            Major = other.Major;
            Minor = other.Minor;
            Build = other.Build;
            Revision = other.Revision;
        }

        /// <summary>
        /// Helper function to create an AssemblyVersion from a version string. If the
        /// string is invalid it returns null
        /// </summary>
        /// <param name="version">Version string. Only digits and periods are allowed.
        /// Partial versions (e.g. "2.3" or even just "1") are fine, but prerelease,
        /// beta, etc. identifiers are not valid</param>
        /// <returns>An AssemblyVersion or null</returns>
        static AssemblyVersion* Create(xstring_t version)
        {
            if (version.size() == 0)
            {
                return nullptr;
            }
            auto identifiers = Configuration::Strings::Split(version, _X("."));
            int major = 0;
            int minor = 0;
            int build = 0;
            int revision = 0;

            // We could just take the first four numbers but safer to treat as an error
            if (identifiers.size() > 4)
            {
                LogWarn(version, L" is not a valid version");
                return nullptr;
            }

            try
            {
                if (identifiers.size() > 0) major = xstoi(identifiers[0]);
                if (identifiers.size() > 1) minor = xstoi(identifiers[1]);
                if (identifiers.size() > 2) build = xstoi(identifiers[2]);
                if (identifiers.size() > 3) revision = xstoi(identifiers[3]);
            }
            catch (...)
            {
                // stoi throws if it can't parse the string
                LogWarn(version, L" is not a valid version");
                return nullptr;
            }

            // Negative numbers aren't valid
            if ((major < 0) || (minor < 0) || (build < 0) || (revision < 0))
            {
                LogWarn(version, L" is not a valid version");
                return nullptr;
            }

            // Nor is anything over 65535
            constexpr int maxValid = (std::numeric_limits<unsigned short>::max)();
            if ((major > maxValid) || (minor > maxValid) || (build > maxValid) || (revision > maxValid))
            {
                LogWarn(version, L" is not a valid version");
                return nullptr;
            }

            // Or 0.0.0.0
            if ((major == 0) && (minor == 0) && (build == 0) && (revision == 0))
            {
                return nullptr;
            }

            return new AssemblyVersion((unsigned short)major, (unsigned short)minor, (unsigned short)build, (unsigned short)revision);
        }

        /// <summary>
        /// Comparison operators
        /// </summary>

        bool operator==(const AssemblyVersion& other) const
        {
            return (this->Major == other.Major) &&
                (this->Minor == other.Minor) &&
                (this->Build == other.Build) &&
                (this->Revision == other.Revision);
        }

        bool operator<(const AssemblyVersion& other) const
        {
            if (this->Major < other.Major) return true;
            if (this->Major > other.Major) return false;

            if (this->Minor < other.Minor) return true;
            if (this->Minor > other.Minor) return false;

            if (this->Build < other.Build) return true;
            if (this->Build > other.Build) return false;

            if (this->Revision < other.Revision) return true;
            if (this->Revision > other.Revision) return false;

            // All equal
            return false;
        }

        bool operator>(const AssemblyVersion& other) const
        {
            if (this->Major > other.Major) return true;
            if (this->Major < other.Major) return false;

            if (this->Minor > other.Minor) return true;
            if (this->Minor < other.Minor) return false;

            if (this->Build > other.Build) return true;
            if (this->Build < other.Build) return false;

            if (this->Revision > other.Revision) return true;
            if (this->Revision < other.Revision) return false;

            // All equal
            return false;
        }

        bool operator<=(const AssemblyVersion& other) const
        {
            return (*this < other) || (*this == other);
        }

        bool operator>=(const AssemblyVersion& other) const
        {
            return (*this > other) || (*this == other);
        }

        xstring_t ToString()
        {
            return to_xstring(Major) + _X(".") + to_xstring(Minor) + _X(".") + to_xstring(Build) + _X(".") + to_xstring(Revision);
        }
    };
}}
