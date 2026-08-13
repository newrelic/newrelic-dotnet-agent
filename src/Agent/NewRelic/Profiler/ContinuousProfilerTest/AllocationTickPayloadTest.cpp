// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include "CppUnitTest.h"
#include <limits>
#include "../ContinuousProfiler/AllocationSampler.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace NewRelic::Profiler::ContinuousProfiler;

// Exercises AllocationSampler's AllocationTick v4 payload parser. This is the one piece of the
// allocation path that is pure, CLR-free logic -- and the one most easily got wrong, since the layout is
// a byte blob whose last field must be read from the END of the buffer -- so it is unit tested directly.
// Including this header here also keeps AllocationSampler.h under continuous compilation before the
// profiler itself references it.
namespace
{
    constexpr size_t PointerSize = sizeof(void*);
    // AllocationAmount(4) + AllocationKind(4) + InstanceId(2) + AllocationAmount64(8) + TypeId(ptr)
    constexpr size_t TypeNameOffset = 4 + 4 + 2 + 8 + PointerSize;
    // ...plus HeapIndex(4) + Address(ptr) + AllocatedSize(8)
    constexpr size_t FixedFieldBytes = TypeNameOffset + 4 + PointerSize + 8;

    // Build a well-formed v4 payload for `typeName` (NUL-terminated in the buffer, as the runtime emits
    // it) and `allocatedSize`, filling every field we do not consume with recognizable junk so a
    // mis-offset read shows up as a wrong value rather than an accidental pass.
    std::vector<uint8_t> BuildPayload(const std::wstring& typeName, uint64_t allocatedSize)
    {
        const size_t nameBytes = (typeName.size() + 1) * sizeof(wchar_t);
        std::vector<uint8_t> payload(FixedFieldBytes + nameBytes, 0xAB);

        std::memcpy(payload.data() + TypeNameOffset, typeName.c_str(), nameBytes);
        std::memcpy(payload.data() + payload.size() - sizeof(uint64_t), &allocatedSize, sizeof(uint64_t));
        return payload;
    }
}

TEST_CLASS(AllocationTickPayloadTest)
{
public:
    TEST_METHOD(Parse_WellFormedPayload_ReadsSizeAndTypeName)
    {
        const auto payload = BuildPayload(L"System.Collections.Generic.List`1[System.String]", 123456789ULL);

        uint64_t allocatedSize = 0;
        std::wstring typeName;
        Assert::IsTrue(AllocationSampler::ParseAllocationTickPayload(
            static_cast<ULONG>(payload.size()), payload.data(), allocatedSize, typeName));

        Assert::AreEqual(123456789ULL, allocatedSize);
        Assert::AreEqual(std::wstring(L"System.Collections.Generic.List`1[System.String]"), typeName);
    }

    TEST_METHOD(Parse_EmptyTypeName_SucceedsWithEmptyString)
    {
        // The shortest legal payload: TypeName is just its NUL terminator.
        const auto payload = BuildPayload(L"", 4096ULL);
        Assert::AreEqual(FixedFieldBytes + sizeof(wchar_t), payload.size());

        uint64_t allocatedSize = 0;
        std::wstring typeName;
        Assert::IsTrue(AllocationSampler::ParseAllocationTickPayload(
            static_cast<ULONG>(payload.size()), payload.data(), allocatedSize, typeName));

        Assert::AreEqual(4096ULL, allocatedSize);
        Assert::IsTrue(typeName.empty());
    }

    TEST_METHOD(Parse_NullData_Rejected)
    {
        uint64_t allocatedSize = 99;
        std::wstring typeName(L"stale");
        Assert::IsFalse(AllocationSampler::ParseAllocationTickPayload(1024, nullptr, allocatedSize, typeName));
    }

    TEST_METHOD(Parse_TooShortPayload_RejectedWithoutReading)
    {
        // One byte short of "all fixed fields + a NUL terminator" -- the under-read guard must reject it.
        std::vector<uint8_t> payload(FixedFieldBytes + sizeof(wchar_t) - 1, 0xAB);

        uint64_t allocatedSize = 99;
        std::wstring typeName(L"stale");
        Assert::IsFalse(AllocationSampler::ParseAllocationTickPayload(
            static_cast<ULONG>(payload.size()), payload.data(), allocatedSize, typeName));

        // Out-params are cleared even on rejection, so a caller that ignores the result cannot publish a
        // stale size/name from a previous sample.
        Assert::AreEqual(0ULL, allocatedSize);
        Assert::IsTrue(typeName.empty());
    }

    TEST_METHOD(Parse_OddTypeNameByteCount_Rejected)
    {
        // A trailing half code unit cannot be a UTF-16 string; the modulo guard must reject it.
        auto payload = BuildPayload(L"Foo", 1ULL);
        payload.push_back(0x00);

        uint64_t allocatedSize = 0;
        std::wstring typeName;
        Assert::IsFalse(AllocationSampler::ParseAllocationTickPayload(
            static_cast<ULONG>(payload.size()), payload.data(), allocatedSize, typeName));
    }

    TEST_METHOD(Parse_TypeNameIsNotReadPastItsLength)
    {
        // The parser must use the computed length, not scan for a NUL: overwrite the terminator and the
        // returned string must still stop at the right place (and must not include the terminator itself).
        auto payload = BuildPayload(L"Foo", 8ULL);
        std::memcpy(payload.data() + TypeNameOffset + (3 * sizeof(wchar_t)), L"X", sizeof(wchar_t));

        uint64_t allocatedSize = 0;
        std::wstring typeName;
        Assert::IsTrue(AllocationSampler::ParseAllocationTickPayload(
            static_cast<ULONG>(payload.size()), payload.data(), allocatedSize, typeName));

        Assert::AreEqual(static_cast<size_t>(3), typeName.size());
        Assert::AreEqual(std::wstring(L"Foo"), typeName);
    }

    TEST_METHOD(Parse_AllocatedSizeComesFromTheEndOfTheBuffer)
    {
        // Two payloads whose ONLY difference is the type name's length must both report the same size --
        // proving the size is read relative to the end, not from a fixed offset.
        const auto shortName = BuildPayload(L"A", 0xDEADBEEFCAFEULL);
        const auto longName = BuildPayload(L"A.Much.Longer.Type.Name.Here", 0xDEADBEEFCAFEULL);

        uint64_t shortSize = 0, longSize = 0;
        std::wstring ignored;
        Assert::IsTrue(AllocationSampler::ParseAllocationTickPayload(
            static_cast<ULONG>(shortName.size()), shortName.data(), shortSize, ignored));
        Assert::IsTrue(AllocationSampler::ParseAllocationTickPayload(
            static_cast<ULONG>(longName.size()), longName.data(), longSize, ignored));

        Assert::AreEqual(0xDEADBEEFCAFEULL, shortSize);
        Assert::AreEqual(0xDEADBEEFCAFEULL, longSize);
    }

    TEST_METHOD(IsAllocationTickEvent_AcceptsOnlyEventTenVersionFour)
    {
        Assert::IsTrue(AllocationSampler::IsAllocationTickEvent(10, 4));
        Assert::IsFalse(AllocationSampler::IsAllocationTickEvent(10, 3));
        Assert::IsFalse(AllocationSampler::IsAllocationTickEvent(10, 5));
        Assert::IsFalse(AllocationSampler::IsAllocationTickEvent(9, 4));
    }

    // The budget arrives from managed code as a SIGNED int. A non-positive value must not be started at
    // all: casting it would produce a huge unsigned target that AllocationSubSampler does not clamp,
    // which degrades into sampling every single AllocationTick on application threads.
    TEST_METHOD(TryNormalizeMaxSamplesPerMinute_RejectsNonPositiveInsteadOfWrappingToHuge)
    {
        uint32_t budget = 0xFFFFFFFFu;
        Assert::IsFalse(AllocationSampler::TryNormalizeMaxSamplesPerMinute(-1, budget));
        Assert::AreEqual(0u, budget, L"a rejected budget must not leave a usable value behind");

        budget = 0xFFFFFFFFu;
        Assert::IsFalse(AllocationSampler::TryNormalizeMaxSamplesPerMinute(0, budget));
        Assert::AreEqual(0u, budget);

        budget = 0xFFFFFFFFu;
        Assert::IsFalse(AllocationSampler::TryNormalizeMaxSamplesPerMinute(
            (std::numeric_limits<int32_t>::min)(), budget));
        Assert::AreEqual(0u, budget);
    }

    TEST_METHOD(TryNormalizeMaxSamplesPerMinute_PassesSaneValuesThrough)
    {
        uint32_t budget = 0;
        Assert::IsTrue(AllocationSampler::TryNormalizeMaxSamplesPerMinute(1, budget));
        Assert::AreEqual(1u, budget);

        Assert::IsTrue(AllocationSampler::TryNormalizeMaxSamplesPerMinute(200, budget));
        Assert::AreEqual(200u, budget, L"the shipped default must survive unmodified");
    }

    TEST_METHOD(TryNormalizeMaxSamplesPerMinute_ClampsAboveTheCeiling)
    {
        // Copied to a local: the ceiling is a static constexpr member, and passing it straight to
        // Assert::AreEqual (which takes a const reference) would odr-use it.
        const int32_t ceiling = AllocationSampler::MaxSupportedSamplesPerMinute;

        uint32_t budget = 0;
        Assert::IsTrue(AllocationSampler::TryNormalizeMaxSamplesPerMinute(ceiling, budget));
        Assert::AreEqual(static_cast<uint32_t>(ceiling), budget, L"the ceiling itself is accepted as-is");

        Assert::IsTrue(AllocationSampler::TryNormalizeMaxSamplesPerMinute(ceiling + 1, budget));
        Assert::AreEqual(static_cast<uint32_t>(ceiling), budget);

        Assert::IsTrue(AllocationSampler::TryNormalizeMaxSamplesPerMinute(
            (std::numeric_limits<int32_t>::max)(), budget));
        Assert::AreEqual(static_cast<uint32_t>(ceiling), budget);
    }
};
