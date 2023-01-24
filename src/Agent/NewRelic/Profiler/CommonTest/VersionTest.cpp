// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include "stdafx.h"
#include "CppUnitTest.h"
// AssemblyVersion does some logging, but we don't care about it for these tests
#define LOGGER_DEFINE_STDLOG
#include "../Common/AssemblyVersion.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic {
    namespace Profiler {
        namespace Common
        {
            TEST_CLASS(VersionTest)
            {
            private:
                // Allows the test to create version objects for comparison without needing to manage the pointer
                AssemblyVersion CreateVersion(xstring_t versionString)
                {
                    auto pVersion = std::unique_ptr<AssemblyVersion>(AssemblyVersion::Create(versionString));
                    if (pVersion == nullptr)
                    {
                        return AssemblyVersion(0, 0, 0, 0);
                    }
                    return AssemblyVersion(*pVersion);
                }

            public:
                TEST_METHOD(version_validity_test)
                {
                    // Valid
                    Assert::IsNotNull(AssemblyVersion::Create(_X("1")));
                    Assert::IsNotNull(AssemblyVersion::Create(_X("1.2")));
                    Assert::IsNotNull(AssemblyVersion::Create(_X("1.2.3")));
                    Assert::IsNotNull(AssemblyVersion::Create(_X("1.2.3.4")));
                    Assert::IsNotNull(AssemblyVersion::Create(_X("12")));
                    Assert::IsNotNull(AssemblyVersion::Create(_X("12.345")));
                    Assert::IsNotNull(AssemblyVersion::Create(_X("12.345.6789")));
                    Assert::IsNotNull(AssemblyVersion::Create(_X("12.345.6789.65535")));
                    Assert::IsNotNull(AssemblyVersion::Create(_X("5.01.340")));
                    Assert::IsNotNull(AssemblyVersion::Create(_X("1")));
                    Assert::IsNotNull(AssemblyVersion::Create(_X("  1.2.3")));
                    Assert::IsNotNull(AssemblyVersion::Create(_X("1.  2.3")));
                    Assert::IsNotNull(AssemblyVersion::Create(_X("1.2.3   ")));
                    Assert::IsNotNull(AssemblyVersion::Create(_X("  1  .2.3")));

                    // Note that these are technically allowed by the string conversion function. It's probably not worth the extra
                    // effort to exclude them
                    Assert::IsNotNull(AssemblyVersion::Create(_X("1x2x3")));                    // resolves to 1
                    Assert::IsNotNull(AssemblyVersion::Create(_X("1.0xff")));                   // resolves to 1.255
                    Assert::IsNotNull(AssemblyVersion::Create(_X("1.+2.+3")));                  // resolves to 1.2.3
                    Assert::IsNotNull(AssemblyVersion::Create(_X("12abcd.35wjkg.88sldkjg")));   // resolves to 12.35.88
                    Assert::IsNotNull(AssemblyVersion::Create(_X("01")));                       // resolves to 1
                }

                TEST_METHOD(version_invalidity_test)
                {
                    Assert::IsNull(AssemblyVersion::Create(_X("")));
                    Assert::IsNull(AssemblyVersion::Create(_X("    ")));
                    Assert::IsNull(AssemblyVersion::Create(_X("1.2.3.a")));
                    Assert::IsNull(AssemblyVersion::Create(_X("zzz1.2.3.a")));
                    Assert::IsNull(AssemblyVersion::Create(_X("abc")));
                    Assert::IsNull(AssemblyVersion::Create(_X("1.a.3")));
                    Assert::IsNull(AssemblyVersion::Create(_X("1.2.3.4.5")));                           // Too many segments
                    Assert::IsNull(AssemblyVersion::Create(_X("12.345.6789.101112")));                  // larger than a max unsigned short
                    Assert::IsNull(AssemblyVersion::Create(_X("99999999999999999999999999999999")));    // larger than an int
                    Assert::IsNull(AssemblyVersion::Create(_X("-1")));
                    Assert::IsNull(AssemblyVersion::Create(_X("1.2.-3.4")));
                }

                TEST_METHOD(version_comparison_test)
                {
                    Assert::IsTrue(CreateVersion(_X("1")) < CreateVersion(_X("2")));
                    Assert::IsTrue(CreateVersion(_X("1.2")) < CreateVersion(_X("2")));
                    Assert::IsTrue(CreateVersion(_X("1.2")) < CreateVersion(_X("2.0")));
                    Assert::IsTrue(CreateVersion(_X("1.2")) < CreateVersion(_X("2.3")));
                    Assert::IsTrue(CreateVersion(_X("1.2")) < CreateVersion(_X("1.2.1")));
                    Assert::IsTrue(CreateVersion(_X("1.2")) < CreateVersion(_X("2.0.0")));
                    Assert::IsTrue(CreateVersion(_X("1.2.3")) < CreateVersion(_X("1.3")));
                    Assert::IsTrue(CreateVersion(_X("1.2.3")) < CreateVersion(_X("1.2.9")));
                    Assert::IsTrue(CreateVersion(_X("1.2.3.4")) < CreateVersion(_X("2")));
                    Assert::IsTrue(CreateVersion(_X("1.2.3.4")) < CreateVersion(_X("1.2.3.5")));

                    Assert::IsFalse(CreateVersion(_X("2")) < CreateVersion(_X("1")));
                    Assert::IsFalse(CreateVersion(_X("2")) < CreateVersion(_X("1.2")));
                    Assert::IsFalse(CreateVersion(_X("2.0")) < CreateVersion(_X("1.2")));
                    Assert::IsFalse(CreateVersion(_X("2.3")) < CreateVersion(_X("1.2")));
                    Assert::IsFalse(CreateVersion(_X("1.2.1")) < CreateVersion(_X("1.2")));
                    Assert::IsFalse(CreateVersion(_X("2.0.0")) < CreateVersion(_X("1.2")));
                    Assert::IsFalse(CreateVersion(_X("1.3")) < CreateVersion(_X("1.2.3")));
                    Assert::IsFalse(CreateVersion(_X("1.2.9")) < CreateVersion(_X("1.2.3")));
                    Assert::IsFalse(CreateVersion(_X("2")) < CreateVersion(_X("1.2.3.4")));
                    Assert::IsFalse(CreateVersion(_X("1.2.3.5")) < CreateVersion(_X("1.2.3.4")));

                    Assert::IsTrue(CreateVersion(_X("1")) <= CreateVersion(_X("2")));
                    Assert::IsTrue(CreateVersion(_X("1.2")) <= CreateVersion(_X("2")));
                    Assert::IsTrue(CreateVersion(_X("1.2")) <= CreateVersion(_X("2.0")));
                    Assert::IsTrue(CreateVersion(_X("1.2")) <= CreateVersion(_X("2.3")));
                    Assert::IsTrue(CreateVersion(_X("1.2")) <= CreateVersion(_X("1.2.1")));
                    Assert::IsTrue(CreateVersion(_X("1.2")) <= CreateVersion(_X("2.0.0")));
                    Assert::IsTrue(CreateVersion(_X("1.2.3")) <= CreateVersion(_X("1.3")));
                    Assert::IsTrue(CreateVersion(_X("1.2.3")) <= CreateVersion(_X("1.2.9")));
                    Assert::IsTrue(CreateVersion(_X("1.2.3.4")) <= CreateVersion(_X("2")));
                    Assert::IsTrue(CreateVersion(_X("1.2.3.4")) <= CreateVersion(_X("1.2.3.5")));

                    Assert::IsTrue(CreateVersion(_X("1")) <= CreateVersion(_X("1")));
                    Assert::IsTrue(CreateVersion(_X("1.2")) <= CreateVersion(_X("1.2")));
                    Assert::IsTrue(CreateVersion(_X("1.2.3")) <= CreateVersion(_X("1.2.3")));
                    Assert::IsTrue(CreateVersion(_X("1.2.3.4")) <= CreateVersion(_X("1.2.3.4")));

                    Assert::IsTrue(CreateVersion(_X("2")) > CreateVersion(_X("1")));
                    Assert::IsTrue(CreateVersion(_X("2")) > CreateVersion(_X("1.2")));
                    Assert::IsTrue(CreateVersion(_X("2.0")) > CreateVersion(_X("1.2")));
                    Assert::IsTrue(CreateVersion(_X("2.3")) > CreateVersion(_X("1.2")));
                    Assert::IsTrue(CreateVersion(_X("1.2.1")) > CreateVersion(_X("1.2")));
                    Assert::IsTrue(CreateVersion(_X("2.0.0")) > CreateVersion(_X("1.2")));
                    Assert::IsTrue(CreateVersion(_X("1.3")) > CreateVersion(_X("1.2.3")));
                    Assert::IsTrue(CreateVersion(_X("1.2.9")) > CreateVersion(_X("1.2.3")));
                    Assert::IsTrue(CreateVersion(_X("2")) > CreateVersion(_X("1.2.3.4")));
                    Assert::IsTrue(CreateVersion(_X("1.2.3.5")) > CreateVersion(_X("1.2.3.4")));

                    Assert::IsFalse(CreateVersion(_X("1")) > CreateVersion(_X("2")));
                    Assert::IsFalse(CreateVersion(_X("1.2")) > CreateVersion(_X("2")));
                    Assert::IsFalse(CreateVersion(_X("1.2")) > CreateVersion(_X("2.0")));
                    Assert::IsFalse(CreateVersion(_X("1.2")) > CreateVersion(_X("2.3")));
                    Assert::IsFalse(CreateVersion(_X("1.2")) > CreateVersion(_X("1.2.1")));
                    Assert::IsFalse(CreateVersion(_X("1.2")) > CreateVersion(_X("2.0.0")));
                    Assert::IsFalse(CreateVersion(_X("1.2.3")) > CreateVersion(_X("1.3")));
                    Assert::IsFalse(CreateVersion(_X("1.2.3")) > CreateVersion(_X("1.2.9")));
                    Assert::IsFalse(CreateVersion(_X("1.2.3.4")) > CreateVersion(_X("2")));
                    Assert::IsFalse(CreateVersion(_X("1.2.3.4")) > CreateVersion(_X("1.2.3.5")));

                    Assert::IsFalse(CreateVersion(_X("1")) >= CreateVersion(_X("2")));
                    Assert::IsFalse(CreateVersion(_X("1.2")) >= CreateVersion(_X("2")));
                    Assert::IsFalse(CreateVersion(_X("1.2")) >= CreateVersion(_X("2.0")));
                    Assert::IsFalse(CreateVersion(_X("1.2")) >= CreateVersion(_X("2.3")));
                    Assert::IsFalse(CreateVersion(_X("1.2")) >= CreateVersion(_X("1.2.1")));
                    Assert::IsFalse(CreateVersion(_X("1.2")) >= CreateVersion(_X("2.0.0")));
                    Assert::IsFalse(CreateVersion(_X("1.2.3")) >= CreateVersion(_X("1.3")));
                    Assert::IsFalse(CreateVersion(_X("1.2.3")) >= CreateVersion(_X("1.2.9")));
                    Assert::IsFalse(CreateVersion(_X("1.2.3.4")) >= CreateVersion(_X("2")));
                    Assert::IsFalse(CreateVersion(_X("1.2.3.4")) >= CreateVersion(_X("1.2.3.5")));

                    Assert::IsTrue(CreateVersion(_X("1")) >= CreateVersion(_X("1")));
                    Assert::IsTrue(CreateVersion(_X("1.2")) >= CreateVersion(_X("1.2")));
                    Assert::IsTrue(CreateVersion(_X("1.2.3")) >= CreateVersion(_X("1.2.3")));
                    Assert::IsTrue(CreateVersion(_X("1.2.3.4")) >= CreateVersion(_X("1.2.3.4")));
                }

            };
        }
    }
}
