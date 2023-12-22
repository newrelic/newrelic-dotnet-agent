#pragma once

#include "stdafx.h"
#include <atomic>

#include "CorProfilerCallbackImpl.h"

namespace NewRelic { namespace Profiler {

    const IID IID_IUnknown      = { 0x00000000, 0x0000, 0x0000, { 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46 } };

    const IID IID_IClassFactory = { 0x00000001, 0x0000, 0x0000, { 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46 } };

    class ClassFactory : public IClassFactory
    {
    public:
        ClassFactory() : _referenceCount(1)
        {
        }
        ~ClassFactory()
        {
        }

        HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void **ppvObject) override
        {
            if (riid == IID_IUnknown || riid == IID_IClassFactory)
            {
                *ppvObject = this;
                this->AddRef();

                return S_OK;
            }

            *ppvObject = nullptr;
            return E_NOINTERFACE;
        }

        virtual ULONG STDMETHODCALLTYPE AddRef() override
        {
            return std::atomic_fetch_add(&this->_referenceCount, 1) + 1;
        }

        virtual ULONG STDMETHODCALLTYPE Release() override
        {
            int count = std::atomic_fetch_sub(&this->_referenceCount, 1) - 1;

            if (count <= 0)
            {
                delete this;
            }

            return count;
        }

        virtual HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject) override
        {
            if (pUnkOuter != nullptr) {
                *ppvObject = nullptr;
                return CLASS_E_NOAGGREGATION;
            }

            auto profiler = std::make_unique<NewRelic::Profiler::CorProfilerCallbackImpl>();

            if (!profiler) {
                return E_FAIL;
            }

            return profiler.release()->QueryInterface(riid, ppvObject);
        }

        virtual HRESULT STDMETHODCALLTYPE LockServer(BOOL fLock) override
        {
            (void)fLock;
            return S_OK;
        }

    private:
        std::atomic<int> _referenceCount;
    };

    class CInstrumentationMethodClassFactory : public IClassFactory
    {
    public:
        CInstrumentationMethodClassFactory() : _referenceCount(1)
        {
        }
        ~CInstrumentationMethodClassFactory()
        {
        }

        HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void **ppvObject) override
        {
            if (riid == IID_IUnknown || riid == IID_IClassFactory)
            {
                *ppvObject = this;
                this->AddRef();

                return S_OK;
            }

            *ppvObject = nullptr;
            return E_NOINTERFACE;
        }

        virtual ULONG STDMETHODCALLTYPE AddRef() override
        {
            return std::atomic_fetch_add(&this->_referenceCount, 1) + 1;
        }

        virtual ULONG STDMETHODCALLTYPE Release() override
        {
            int count = std::atomic_fetch_sub(&this->_referenceCount, 1) - 1;

            if (count <= 0)
            {
                delete this;
            }

            return count;
        }

        virtual HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject) override
        {
            if (pUnkOuter != nullptr) {
                *ppvObject = nullptr;
                return CLASS_E_NOAGGREGATION;
            }

            auto profiler = std::make_unique<NewRelic::Profiler::CInstrumentationMethod>();

            if (!profiler) {
                return E_FAIL;
            }

            return profiler.release()->QueryInterface(riid, ppvObject);
        }

        virtual HRESULT STDMETHODCALLTYPE LockServer(BOOL fLock) override
        {
            (void)fLock;
            return S_OK;
        }

    private:
        std::atomic<int> _referenceCount;
    };

}}
