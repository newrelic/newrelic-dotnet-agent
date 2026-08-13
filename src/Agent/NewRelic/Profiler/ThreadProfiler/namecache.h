/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <algorithm>
#include <array>
#include <cstddef>
#include <cstdint>
#include <list>
#include <memory>
#include <unordered_map>
#include <utility>
#include <cor.h>
#include <corprof.h>
#include "../Common/xplat.h"

namespace NewRelic {
    namespace Profiler {
        namespace ThreadProfiler
        {
            static constexpr std::size_t MAX_TYPE_NAME_LENGTH = 1023;
            static constexpr std::size_t MAX_METHOD_NAME_LENGTH = 1023;
            using PreallocTypeName = std::pair<std::array<xchar_t, MAX_TYPE_NAME_LENGTH>, ULONG>;
            using PreallocMethodName = std::pair<std::array<xchar_t, MAX_METHOD_NAME_LENGTH>, ULONG>;

            //holds a reference to the type name that is in the type-name cache and the actual string for the method name
            class TypeAndMethodNames
            {
            public:
                TypeAndMethodNames & operator=(const TypeAndMethodNames& other) = delete;

                TypeAndMethodNames(std::shared_ptr<xstring_t> typeName, xstring_t methodName) noexcept : _typeName(std::move(typeName)), _methodName(std::move(methodName))
                {}

                // Declared explicitly: a user-declared copy-assignment suppresses the implicit move
                // constructor and deprecates the implicit copy constructor, and the cache stores these
                // by value.
                TypeAndMethodNames(const TypeAndMethodNames&) = default;
                TypeAndMethodNames(TypeAndMethodNames&&) = default;

                static std::shared_ptr<xstring_t> GetUnknownTypeName()
                {
                    static const std::shared_ptr<xstring_t> UnknownTypeName = std::make_shared<xstring_t>(_X("UnknownClass"));
                    return UnknownTypeName;
                }

                static const TypeAndMethodNames& GetUnknownTypeAndMethodNames()
                {
                    static const TypeAndMethodNames UnknownTypeAndMethod{ GetUnknownTypeName(), _X("UnknownMethod(error)") };
                    return UnknownTypeAndMethod;
                }

                const xchar_t * TypeName() const noexcept
                {
                    return _typeName->c_str();
                }

                const xchar_t * MethodName() const noexcept
                {
                    return _methodName.c_str();
                }
            private:
                std::shared_ptr<xstring_t> _typeName;
                xstring_t _methodName;
            };

            // Hash helper for CLR ids. A FunctionID is a MethodDesc address and a ModuleID is a Module
            // address, so both are aligned and their low bits are always zero. std::hash of an integer is
            // the identity in the libraries we build against, and MSVC's unordered_map selects a bucket by
            // masking the LOW hash bits -- an identity hash of an aligned pointer would therefore collapse
            // most keys onto a fraction of the buckets. Mix the high bits down first.
            inline std::size_t MixId(std::uint64_t id) noexcept
            {
                std::uint64_t hash = id * 0x9E3779B97F4A7C15ULL;
                hash ^= hash >> 32;
                return static_cast<std::size_t>(hash);
            }

            struct FunctionIdHash
            {
                std::size_t operator()(FunctionID functionId) const noexcept
                {
                    return MixId(static_cast<std::uint64_t>(functionId));
                }
            };

            // Cache key for a type name. mdTypeDef tokens are small, sequential, PER-MODULE integers, so
            // nearly every loaded module defines a type at the same low token value -- the token on its own
            // is not a usable key and collides across modules within minutes of a real process starting.
            // No default member initializers: the Linux build is C++11, where those would stop this from
            // being an aggregate and break the TypeKey{ moduleId, typeDef } initializations below.
            struct TypeKey
            {
                ModuleID moduleId;
                mdTypeDef typeDef;

                bool operator==(const TypeKey& other) const noexcept
                {
                    return moduleId == other.moduleId && typeDef == other.typeDef;
                }
            };

            struct TypeKeyHash
            {
                std::size_t operator()(const TypeKey& key) const noexcept
                {
                    return MixId(static_cast<std::uint64_t>(key.moduleId) ^ (static_cast<std::uint64_t>(key.typeDef) * 0xC2B2AE3D27D4EB4FULL));
                }
            };

            // Bounded least-recently-used cache: hashed O(1) lookup and O(1) eviction of the
            // least-recently-used entry once Capacity entries are live.
            //
            // NOT thread safe, and note that even Get mutates (it reorders the LRU list). Both users --
            // the thread profiler's worker thread and the continuous profiler's sampling thread -- own
            // their own instance and touch it from one thread at a time.
            template <typename TKey, typename TValue, typename THash, std::size_t Capacity>
            class BoundedLruCache
            {
                struct Entry
                {
                    TValue value;
                    typename std::list<TKey>::iterator order;
                };
                using EntryMap = std::unordered_map<TKey, Entry, THash>;

            public:
                // Returns nullptr when the key is absent. On a hit the entry becomes most-recently-used.
                const TValue* Get(const TKey& key) const
                {
                    const auto itr = _entries.find(key);
                    if (itr == std::end(_entries))
                    {
                        return nullptr;
                    }

                    _order.splice(std::begin(_order), _order, itr->second.order);
                    return &itr->second.value;
                }

                // Inserts only when the key is absent; an already-present entry is kept (it was resolved
                // from the same metadata, so it is already correct) and merely becomes most-recently-used.
                // Returns the stored value -- never nullptr.
                const TValue* Put(const TKey& key, TValue value)
                {
                    if (const auto* existing = Get(key))
                    {
                        return existing;
                    }

                    if (_entries.size() >= Capacity)
                    {
                        _entries.erase(_order.back());
                        _order.pop_back();
                    }

                    _order.push_front(key);
                    try
                    {
                        return &_entries.emplace(key, Entry{ std::move(value), std::begin(_order) }).first->second.value;
                    }
                    catch (...)
                    {
                        _order.pop_front();
                        throw;
                    }
                }

                void clear() noexcept
                {
                    _entries.clear();
                    _order.clear();
                }

            private:
                // mutable: Get is logically a read but has to move the hit to the front of the LRU list.
                mutable std::list<TKey> _order; // most-recently-used first
                mutable EntryMap _entries;
            };

            class NameCache
            {
                // Upper bound on live entries in each map. Sized to hold every method a real application is
                // actually sampled in (OpenTelemetry's equivalent long-lived name cache uses the same 5000)
                // while keeping the worst case bounded -- both maps previously grew for the life of the
                // process and were scanned linearly on every lookup.
                static constexpr std::size_t MaxCachedNames = 5000;

                using FunctionNameCache = BoundedLruCache<FunctionID, TypeAndMethodNames, FunctionIdHash, MaxCachedNames>;
                using TypeNameCache = BoundedLruCache<TypeKey, std::shared_ptr<xstring_t>, TypeKeyHash, MaxCachedNames>;

            public:

                bool has_fid(FunctionID fid) const
                {
                    return _functionNames.Get(fid) != nullptr;
                }

                const TypeAndMethodNames& operator[](FunctionID fid) const
                {
                    const auto* names = _functionNames.Get(fid);
                    return names != nullptr ? *names : TypeAndMethodNames::GetUnknownTypeAndMethodNames();
                }

                // Returns the shared UnknownTypeName singleton when the type is not cached; callers detect a
                // miss by comparing the result against GetUnknownTypeName().
                std::shared_ptr<xstring_t> typename_for(ModuleID moduleId, mdTypeDef typeDef) const
                {
                    const auto* typeName = _typeNames.Get(TypeKey{ moduleId, typeDef });
                    return typeName != nullptr ? *typeName : TypeAndMethodNames::GetUnknownTypeName();
                }

                void clear() noexcept
                {
                    _functionNames.clear();
                    _typeNames.clear();
                }

                // Callers must only reach here with names they actually resolved for THIS function: the
                // prealloc name buffers are reused across frames, so after a failed metadata call they still
                // hold the previous frame's names, which would then be cached under this frame's keys.
                void insert(ModuleID moduleId, FunctionID functionId, mdTypeDef typeDef, const PreallocTypeName& typeName, const PreallocMethodName& methodName)
                {
                    const TypeKey typeKey{ moduleId, typeDef };
                    const auto* cachedTypeName = _typeNames.Get(typeKey);
                    if (cachedTypeName == nullptr)
                    {
                        cachedTypeName = _typeNames.Put(typeKey, std::make_shared<xstring_t>(to_xstring_t(typeName)));
                    }

                    _functionNames.Put(functionId, TypeAndMethodNames(*cachedTypeName, to_xstring_t(methodName)));
                }

            private:

                // PreallocTypeName/PreallocMethodName .second is the string length INCLUDING the null
                // terminator; clamp it to the buffer so an out-of-range length can never read past the end.
                template <std::size_t N>
                static xstring_t to_xstring_t(const std::pair<std::array<xchar_t, N>, ULONG>& prealloc)
                {
                    const std::size_t length = prealloc.second == 0 ? 0 : std::min<std::size_t>(prealloc.second - 1, N - 1);
                    return xstring_t(prealloc.first.data(), length);
                }

                FunctionNameCache _functionNames;
                TypeNameCache _typeNames;
            };
        } // namespace ThreadProfiler
    } // namespace Profiler
} // namespace NewRelic
