/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <algorithm>
#include <array>
#include <cstdint>
#include <cstring>
#include <memory>
#include <vector>

#include <cor.h>
#include <corprof.h>

#include "../ThreadProfiler/namecache.h"
#include "../SignatureParser/SignatureParser.h"
#include "../SignatureParser/SignatureFormatting.h"
#include "../Profiler/CorTokenResolver.h"

// FrameNameResolver turns a captured FunctionID into the fully-qualified frame name the managed side
// consumes ("Namespace.Type.Method(System.Int32)"), caching type/method names in a caller-owned
// NameCache. It was extracted verbatim from ContinuousProfiler so BOTH samplers -- the timer-driven
// thread sampler and the event-driven AllocationSampler -- share one implementation instead of two
// copies of the same ~120 lines of metadata/signature handling.
//
// EVERYTHING HERE IS POST-RESUME ONLY. Every method allocates, takes metadata locks and/or makes
// ICorProfilerInfo calls, so none of it may run while the runtime (or any app thread) is suspended --
// that is the whole reason ContinuousProfiler defers name resolution until after ResumeRuntime.
//
// NOT THREAD SAFE, deliberately. It owns a ~4 KB reusable scratch frame and writes into a NameCache
// that is itself documented as single-threaded (namecache.h: "even Get mutates"). Each sampler
// therefore owns its OWN FrameNameResolver + NameCache and touches it from one thread at a time; they
// are NOT shared across the sampling thread and app threads. Sharing one instance would mean two
// threads mutating the same unordered_map/LRU list concurrently -- heap corruption, not just a stale
// name -- and the alternative (a mutex) would let an app thread block behind the sampler thread
// resolving a hundred threads' stacks. Duplicated cache entries are bounded (namecache.h caps each
// map at 5000 LRU entries), which is a far cheaper price than either of those.
namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    class FrameNameResolver
    {
    public:
        // Reuse the ThreadProfiler's preallocated name-cache machinery verbatim (same suspend-safe
        // constraints apply): NameCache, the prealloc name buffers, and the type/method name holder.
        using NameCache = NewRelic::Profiler::ThreadProfiler::NameCache;
        using TypeAndMethodNames = NewRelic::Profiler::ThreadProfiler::TypeAndMethodNames;
        using PreallocTypeName = NewRelic::Profiler::ThreadProfiler::PreallocTypeName;
        using PreallocMethodName = NewRelic::Profiler::ThreadProfiler::PreallocMethodName;

        // Upper bound on a captured method-signature blob. Signatures larger than this fall back to a
        // name-only frame (no parameter list) rather than allocating in the snapshot callback.
        static constexpr size_t MaxSigBlobBytes = 256;

        // Defensive bound on the nested-type enclosing-chain walk in QualifyNestedTypeName. Real nesting is
        // shallow (a handful of levels at most); this only stops a pathological or corrupt-metadata loop.
        static constexpr size_t MaxTypeNestingDepth = 16;

        // One preallocated stack frame. All name storage is preallocated so the snapshot callback never
        // allocates. Mirrors ThreadProfiler::StackFrame (ThreadProfiler.h:290-302).
        struct StackFrame
        {
            FunctionID functionId{};
            // Defining module of functionId. Half of the type-name cache key: an mdTypeDef token is only
            // unique within its own module (see namecache.h).
            ModuleID moduleId{};
            mdTypeDef typeDef{};
            PreallocTypeName typeName{};
            PreallocMethodName methodName{};

            // Raw COR method-signature blob captured under suspend (zero-alloc memcpy); parsed + formatted
            // into the method name during the post-walk fold. sigBlobLength == 0 means "no signature".
            std::array<uint8_t, MaxSigBlobBytes> sigBlob{};
            uint32_t sigBlobLength{};

            StackFrame() = default;
            StackFrame(const StackFrame&) = delete;
            StackFrame(StackFrame&&) = delete;
            StackFrame& operator=(const StackFrame&) = delete;
            StackFrame& operator=(StackFrame&&) = delete;
        };

        // `nameCache` is borrowed, NOT owned -- it must outlive this resolver (both samplers declare it
        // as a member ahead of the resolver, so destruction order guarantees that).
        FrameNameResolver(NameCache& nameCache, ICorProfilerInfo4* corProfilerInfo)
            : _nameCache(nameCache), _corProfilerInfo(corProfilerInfo)
        {
        }

        // POST-RESUME: resolve (and cache) one FunctionID's fully-qualified frame name. The two-step
        // resolve-then-assemble split is an implementation detail: a cache miss populates the cache, then
        // the name is assembled from it. Never throws for a resolution failure -- an unresolvable function
        // yields "Native.Function Call" / "UnknownClass.UnknownMethod(<id>)" instead.
        xstring_t ResolveFrameName(FunctionID functionId)
        {
            ResolveIntoCache(functionId);
            return AssembleFrameName(functionId);
        }

        FrameNameResolver(const FrameNameResolver&) = delete;
        FrameNameResolver(FrameNameResolver&&) = delete;
        FrameNameResolver& operator=(const FrameNameResolver&) = delete;
        FrameNameResolver& operator=(FrameNameResolver&&) = delete;

    private:
        // POST-RESUME: resolve one FunctionID's type + method name (+ signature) into the name cache, if not
        // already cached. Mirrors what the snapshot callback used to do under suspend, moved here so all
        // metadata calls + allocation happen after ResumeRuntime. functionId==0 and unresolvable functions
        // are left uncached (AssembleFrameName then emits "Native.Function Call" / "UnknownMethod(<id>)").
        // Never throws.
        void ResolveIntoCache(FunctionID functionId) noexcept
        {
            if (functionId == 0 || _nameCache.has_fid(functionId))
                return;

            try
            {
                CComPtr<IMetaDataImport2> metaData;
                mdToken methodToken{};
                if (FAILED(_corProfilerInfo->GetTokenAndMetaDataFromFunction(functionId, IID_IMetaDataImport2, (IUnknown**)&metaData, &methodToken)) || metaData == nullptr)
                    return;

                auto& scratch = _resolveScratch;
                scratch.functionId = functionId;
                scratch.moduleId = 0;
                scratch.typeDef = 0;
                scratch.sigBlobLength = 0;

                auto& methodName = scratch.methodName;
                PCCOR_SIGNATURE pSigBlob = nullptr;
                ULONG sigBlobLength = 0;
                if (FAILED(metaData->GetMethodProps(methodToken, &scratch.typeDef,
                    &methodName.first.front(), (ULONG)methodName.first.size(), &methodName.second,
                    nullptr, &pSigBlob, &sigBlobLength, nullptr, nullptr)))
                    return;

                if (scratch.typeDef == 0)
                    return; // no owning type -> leave uncached (AssembleFrameName emits UnknownMethod(<id>))

                // The defining module completes the type-name cache key -- an mdTypeDef token alone is only
                // unique within its own module. One extra call per cache-missing function, not per frame.
                ClassID classId = 0;
                mdToken functionToken = 0;
                if (FAILED(_corProfilerInfo->GetFunctionInfo(functionId, &classId, &scratch.moduleId, &functionToken)) || scratch.moduleId == 0)
                    return;

                if (pSigBlob != nullptr && sigBlobLength > 0 && sigBlobLength <= MaxSigBlobBytes)
                {
                    std::memcpy(scratch.sigBlob.data(), pSigBlob, sigBlobLength);
                    scratch.sigBlobLength = sigBlobLength;
                }

                auto& typeName = scratch.typeName;
                const auto cachedTypeName = _nameCache.typename_for(scratch.moduleId, scratch.typeDef);
                if (cachedTypeName == TypeAndMethodNames::GetUnknownTypeName())
                {
                    DWORD typeFlags = 0;
                    // Bail on failure rather than caching: typeName still holds the PREVIOUS resolve's name
                    // (only functionId/moduleId/typeDef/sigBlobLength are reset per call), which would
                    // otherwise be cached under this type's key. Uncached -> UnknownMethod(<id>).
                    if (FAILED(metaData->GetTypeDefProps(scratch.typeDef, &typeName.first.front(), static_cast<ULONG>(typeName.first.size()), &typeName.second, &typeFlags, nullptr)))
                        return;

                    // GetTypeDefProps returns only the innermost name for a NESTED type (e.g. the compiler
                    // closure "<>c"), dropping the declaring type -- unusable on its own since every type's
                    // closures share that name. Walk the enclosing chain and rebuild "Outer+...+Inner" so the
                    // frame is attributable. Cached per typeDef (below), so this runs once per type.
                    if (IsTdNested(typeFlags))
                    {
                        QualifyNestedTypeName(metaData, scratch.typeDef, typeFlags, typeName);
                    }
                }
                else
                {
                    wcscpy_s(typeName.first.data(), static_cast<ULONG>(typeName.first.size()), cachedTypeName->c_str());
                }

                AppendSignature(scratch); // fold the parameter list into the method name
                _nameCache.insert(scratch.moduleId, scratch.functionId, scratch.typeDef, scratch.typeName, scratch.methodName);
            }
            catch (...)
            {
                // Leave uncached -> name-only / UnknownMethod(<id>). Never crash the sampler.
            }
        }

        // POST-RESUME: rewrite a nested type's prealloc name from the bare innermost name GetTypeDefProps
        // returns (e.g. the compiler closure "<>c") to the fully-qualified "Outer+...+Inner", walking the
        // enclosing chain via GetNestedClassProps and prepending each encloser with '+' (the CLR nested-type
        // separator, matching Function.h). Uses IsTdNested -- ALL nested visibilities -- so tdNestedPrivate/
        // tdNestedAssembly compiler closures are qualified too (a tdNestedPublic|tdNestedFamily mask misses
        // them). Bounded and never throws; on any failure the bare innermost name is left as-is.
        void QualifyNestedTypeName(IMetaDataImport2* metaData, mdTypeDef typeDef, DWORD typeFlags, PreallocTypeName& out) noexcept
        {
            try
            {
                xstring_t qualified(out.first.data()); // innermost name GetTypeDefProps just wrote
                mdTypeDef current = typeDef;
                DWORD flags = typeFlags;

                for (size_t depth = 0; IsTdNested(flags) && depth < MaxTypeNestingDepth; ++depth)
                {
                    mdTypeDef enclosing = 0;
                    if (FAILED(metaData->GetNestedClassProps(current, &enclosing)) || enclosing == 0)
                        break;

                    ULONG nameLen = 0;
                    metaData->GetTypeDefProps(enclosing, nullptr, 0, &nameLen, nullptr, nullptr);
                    if (nameLen == 0)
                        break;

                    std::vector<xchar_t> buffer(nameLen);
                    DWORD enclosingFlags = 0;
                    if (FAILED(metaData->GetTypeDefProps(enclosing, buffer.data(), nameLen, &nameLen, &enclosingFlags, nullptr)))
                        break;

                    qualified = xstring_t(buffer.data()) + _X("+") + qualified;
                    current = enclosing;
                    flags = enclosingFlags;
                }

                // Copy back into the prealloc buffer, truncating to capacity. PreallocTypeName.second is the
                // length INCLUDING the null terminator (NameCache::insert stores .second - 1 chars).
                const size_t maxChars = out.first.size() - 1;
                const size_t n = qualified.size() < maxChars ? qualified.size() : maxChars;
                std::copy_n(qualified.c_str(), n, out.first.data());
                out.first[n] = 0;
                out.second = static_cast<ULONG>(n + 1);
            }
            catch (...)
            {
                // Leave the bare innermost name as-is; never crash the sampler.
            }
        }

        // POST-RESUME: assemble one frame's fully-qualified name from the (now-populated) cache, mirroring
        // the thread profiler's three-case handling: functionId==0 -> "Native.Function Call"; resolved ->
        // "Type.Method(params)"; real-but-unresolvable -> "UnknownClass.UnknownMethod(<id>)".
        xstring_t AssembleFrameName(FunctionID functionId)
        {
            if (functionId == 0)
            {
                // NOTE: the managed PprofProfileBuilder.NativeFrameName constant MUST match this exact
                // string -- it keys profile.frame.type = "native" off it. Change both together.
                return _X("Native.Function Call");
            }
            if (!_nameCache.has_fid(functionId))
            {
                xstring_t frameName(_X("UnknownClass.UnknownMethod("));
                frameName.append(to_xstring((unsigned long)functionId));
                frameName.append(_X(")"));
                return frameName;
            }
            const auto& names = _nameCache[functionId];
            xstring_t frameName(names.TypeName());
            frameName.append(_X("."));
            frameName.append(names.MethodName());
            return frameName;
        }

        // Format the frame's captured method signature and append its parameter list to the method name,
        // turning "Type.Method" into "Type.Method(System.Object, System.Int32)" -- OTel-shaped, so a
        // customer migrating OTel->NR sees identical frames and overloads are distinguishable. Runs during
        // post-resume resolution (ResolveIntoCache), once per newly-resolved functionId. Any failure (parse
        // error, unresolvable token, would-overflow the name buffer) leaves the name-only method name --
        // never throws, never crashes the sampler.
        void AppendSignature(StackFrame& frame) noexcept
        {
            if (frame.sigBlobLength == 0)
                return;

            try
            {
                // Re-fetch the defining module's metadata reader so signature type tokens resolve in the
                // correct scope. Cheap: this runs only for frames being inserted fresh into the cache.
                CComPtr<IMetaDataImport2> metaData;
                mdToken methodToken{};
                if (FAILED(_corProfilerInfo->GetTokenAndMetaDataFromFunction(frame.functionId, IID_IMetaDataImport2, (IUnknown**)&metaData, &methodToken)) || metaData == nullptr)
                    return;

                ByteVector bytes(frame.sigBlob.begin(), frame.sigBlob.begin() + frame.sigBlobLength);
                auto iterator = bytes.cbegin();
                auto methodSignature = SignatureParser::SignatureParser::ParseMethodSignature(iterator, bytes.cend());
                auto resolver = std::make_shared<CorTokenResolver>(metaData);
                const auto params = SignatureParser::FormatParameterList(methodSignature, resolver); // "(...)"

                // methodName.second is the current length INCLUDING the null terminator (NameCache convention).
                auto& buffer = frame.methodName.first;
                const size_t nameLength = frame.methodName.second == 0 ? 0 : frame.methodName.second - 1;
                if (nameLength + params.size() + 1 <= buffer.size())
                {
                    std::copy(params.begin(), params.end(), buffer.begin() + nameLength);
                    buffer[nameLength + params.size()] = _X('\0');
                    frame.methodName.second = static_cast<ULONG>(nameLength + params.size() + 1);
                }
            }
            catch (...)
            {
                // Keep the name-only method name.
            }
        }

        // Type/method name cache, owned by the SAMPLER (not this resolver) and reused across samples.
        NameCache& _nameCache;

        // Interface to the CLR metadata services. Provided during profiler Initialize.
        CComPtr<ICorProfilerInfo4> _corProfilerInfo;

        // Reusable scratch frame for post-resume name/signature resolution (prealloc name + sig buffers),
        // so ResolveIntoCache does not allocate ~4 KB per resolved function. Touched only by the owning
        // sampler's thread (see the not-thread-safe note on this class).
        StackFrame _resolveScratch;
    };
}}}
