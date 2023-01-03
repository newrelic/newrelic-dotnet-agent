/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include "../Configuration/Strings.h"
#include <vector>
#include <sstream>
#include <cor.h>
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
            auto identifiers = Configuration::Strings::Split(version, '.');
            unsigned short major = 0;
            unsigned short minor = 0;
            unsigned short build = 0;
            unsigned short revision = 0;

            try
            {
                if (identifiers.size() > 0) major = (unsigned short)xstoi(identifiers[0]);
                if (identifiers.size() > 1) minor = (unsigned short)xstoi(identifiers[1]);
                if (identifiers.size() > 2) build = (unsigned short)xstoi(identifiers[2]);
                if (identifiers.size() > 3) revision = (unsigned short)xstoi(identifiers[3]);
            }
            catch (...)
            {
                // stoi throws if it can't parse the string
                LogWarn(version, L" is not a valid version");
                return nullptr;
            }

            if ((major == 0) && (minor == 0) && (build == 0) && (revision == 0))
            {
                return nullptr;
            }

            return new AssemblyVersion(major, minor, build, revision);
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
