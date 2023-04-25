// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#define LOGGER_DEFINE_STDLOG

#include "ClassFactory.hpp"

const IID IID_IUnknown      = { 0x00000000, 0x0000, 0x0000, { 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46 } };

BOOL STDMETHODCALLTYPE DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    (void)hModule;
    (void)ul_reason_for_call;
    (void)lpReserved;
    return TRUE;
}

extern "C"
{
    HRESULT STDMETHODCALLTYPE DllGetClassObject(REFCLSID /* rclsid */, REFIID riid, void** ppv)
    {
        //std::this_thread::sleep_for(std::chrono::milliseconds(1000 * 30));

        auto factory = new NewRelic::Profiler::ClassFactory();
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
