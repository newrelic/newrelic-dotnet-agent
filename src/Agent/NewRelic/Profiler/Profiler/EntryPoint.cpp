// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#define LOGGER_DEFINE_STDLOG

#include "ClassFactory.hpp"

const IID IID_IUnknown      = { 0x00000000, 0x0000, 0x0000, { 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46 } };

// {71DA0A04-7777-4EC6-9643-7D28B46A8A41}
const CLSID CLSID_NewRelicProfiler = {0x71DA0A04,0x7777,0x4EC6,0x96,0x43,0x7D,0x28,0xB4,0x6A,0x8A,0x41};
// {36032161-FFC0-4B61-B559-F6C5D41BAE5A}
const CLSID CLSID_NewRelicCorCLRProfiler = {0x36032161,0xFFC0,0x4B61,0xB5,0x59,0xF6,0xC5,0xD4,0x1B,0xAE,0x5A};

BOOL STDMETHODCALLTYPE DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    (void)hModule;
    (void)ul_reason_for_call;
    (void)lpReserved;
    return TRUE;
}

extern "C" {
    HRESULT STDMETHODCALLTYPE DllGetClassObject(REFCLSID rclsid, REFIID riid, void **ppv)
    {
        //std::this_thread::sleep_for(std::chrono::milliseconds(1000 * 30));
        if (ppv == NULL || !(rclsid == CLSID_NewRelicProfiler || rclsid == CLSID_NewRelicCorCLRProfiler))
            return E_FAIL;

        auto factory = new NewRelic::Profiler::ClassFactory(rclsid == CLSID_NewRelicCorCLRProfiler);
        if (factory == nullptr)
        {
            return E_FAIL;
        }

        return factory->QueryInterface(riid, ppv);
    }

    HRESULT STDMETHODCALLTYPE DllCanUnloadNow()
    {
        return S_OK;
    }
}