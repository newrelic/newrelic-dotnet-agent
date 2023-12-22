// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#pragma once

namespace CommonLib
{
    #ifndef ARRAYSIZE
    #define ARRAYSIZE(a)    (sizeof(a)/sizeof((a)[0]))
    #endif

    //
    // This function maps an IID that identifies a particular module (example: IID_IAmSdmObject) to IUnknown
    inline REFIID MapToTrueIID(REFIID riidModIdentifier, REFIID riidRequest)
    {
        return (riidModIdentifier == riidRequest) ? IID_IUnknown : riidRequest;
    }


    //
    // Overloads which accept riidModIdentifier

    template <typename INTERFACE_T>
    inline HRESULT ImplQueryInterface(REFIID riidModIdentifier, _In_ INTERFACE_T *pThis, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        return ImplQueryInterface(pThis, MapToTrueIID(riidModIdentifier, riid), ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2>
    inline HRESULT ImplQueryInterface(REFIID riidModIdentifier, _In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        return ImplQueryInterface(pThis1, pThis2, MapToTrueIID(riidModIdentifier, riid), ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3>
    inline HRESULT ImplQueryInterface(REFIID riidModIdentifier, _In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        return ImplQueryInterface(pThis1, pThis2, pThis3, MapToTrueIID(riidModIdentifier, riid), ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4>
    inline HRESULT ImplQueryInterface(REFIID riidModIdentifier, _In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        return ImplQueryInterface(pThis1, pThis2, pThis3, pThis4, MapToTrueIID(riidModIdentifier, riid), ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5>
    inline HRESULT ImplQueryInterface(REFIID riidModIdentifier, _In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        return ImplQueryInterface(pThis1, pThis2, pThis3, pThis4, pThis5, MapToTrueIID(riidModIdentifier, riid), ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6>
    inline HRESULT ImplQueryInterface(REFIID riidModIdentifier, _In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        return ImplQueryInterface(pThis1, pThis2, pThis3, pThis4, pThis5, pThis6, MapToTrueIID(riidModIdentifier, riid), ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7>
    inline HRESULT ImplQueryInterface(REFIID riidModIdentifier, _In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        return ImplQueryInterface(pThis1, pThis2, pThis3, pThis4, pThis5, pThis6, pThis7, MapToTrueIID(riidModIdentifier, riid), ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8>
    inline HRESULT ImplQueryInterface(REFIID riidModIdentifier, _In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        return ImplQueryInterface(pThis1, pThis2, pThis3, pThis4, pThis5, pThis6, pThis7, pThis8, MapToTrueIID(riidModIdentifier, riid), ppvObject);
    }




    //
    // Functions to implement query interface which take 1-32 interfaces as input

    template <typename INTERFACE_T>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T *pThis, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk = static_cast<IUnknown *>(pThis); // Verify that INTERFACE_T is a COM interface

        return InternalImplQueryInterface::SingleInterfaceImp(pUnk, __uuidof(INTERFACE_T), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[2] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[3] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[4] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[5] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[6] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[7] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[8] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[9] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[10] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[11] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[12] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[13] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[14] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[15] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15, typename INTERFACE_T16>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, _In_ INTERFACE_T16 *pThis16, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface
        IUnknown *pUnk16 = static_cast<IUnknown *>(pThis16); // Verify that INTERFACE_T16 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[16] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
           { pUnk16, &__uuidof(INTERFACE_T16) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15, typename INTERFACE_T16, typename INTERFACE_T17>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, _In_ INTERFACE_T16 *pThis16, _In_ INTERFACE_T17 *pThis17, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface
        IUnknown *pUnk16 = static_cast<IUnknown *>(pThis16); // Verify that INTERFACE_T16 is a COM interface
        IUnknown *pUnk17 = static_cast<IUnknown *>(pThis17); // Verify that INTERFACE_T17 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[17] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
           { pUnk16, &__uuidof(INTERFACE_T16) },
           { pUnk17, &__uuidof(INTERFACE_T17) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15, typename INTERFACE_T16, typename INTERFACE_T17, typename INTERFACE_T18>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, _In_ INTERFACE_T16 *pThis16, _In_ INTERFACE_T17 *pThis17, _In_ INTERFACE_T18 *pThis18, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface
        IUnknown *pUnk16 = static_cast<IUnknown *>(pThis16); // Verify that INTERFACE_T16 is a COM interface
        IUnknown *pUnk17 = static_cast<IUnknown *>(pThis17); // Verify that INTERFACE_T17 is a COM interface
        IUnknown *pUnk18 = static_cast<IUnknown *>(pThis18); // Verify that INTERFACE_T18 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[18] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
           { pUnk16, &__uuidof(INTERFACE_T16) },
           { pUnk17, &__uuidof(INTERFACE_T17) },
           { pUnk18, &__uuidof(INTERFACE_T18) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15, typename INTERFACE_T16, typename INTERFACE_T17, typename INTERFACE_T18, typename INTERFACE_T19>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, _In_ INTERFACE_T16 *pThis16, _In_ INTERFACE_T17 *pThis17, _In_ INTERFACE_T18 *pThis18, _In_ INTERFACE_T19 *pThis19, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface
        IUnknown *pUnk16 = static_cast<IUnknown *>(pThis16); // Verify that INTERFACE_T16 is a COM interface
        IUnknown *pUnk17 = static_cast<IUnknown *>(pThis17); // Verify that INTERFACE_T17 is a COM interface
        IUnknown *pUnk18 = static_cast<IUnknown *>(pThis18); // Verify that INTERFACE_T18 is a COM interface
        IUnknown *pUnk19 = static_cast<IUnknown *>(pThis19); // Verify that INTERFACE_T19 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[19] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
           { pUnk16, &__uuidof(INTERFACE_T16) },
           { pUnk17, &__uuidof(INTERFACE_T17) },
           { pUnk18, &__uuidof(INTERFACE_T18) },
           { pUnk19, &__uuidof(INTERFACE_T19) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15, typename INTERFACE_T16, typename INTERFACE_T17, typename INTERFACE_T18, typename INTERFACE_T19, typename INTERFACE_T20>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, _In_ INTERFACE_T16 *pThis16, _In_ INTERFACE_T17 *pThis17, _In_ INTERFACE_T18 *pThis18, _In_ INTERFACE_T19 *pThis19, _In_ INTERFACE_T20 *pThis20, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface
        IUnknown *pUnk16 = static_cast<IUnknown *>(pThis16); // Verify that INTERFACE_T16 is a COM interface
        IUnknown *pUnk17 = static_cast<IUnknown *>(pThis17); // Verify that INTERFACE_T17 is a COM interface
        IUnknown *pUnk18 = static_cast<IUnknown *>(pThis18); // Verify that INTERFACE_T18 is a COM interface
        IUnknown *pUnk19 = static_cast<IUnknown *>(pThis19); // Verify that INTERFACE_T19 is a COM interface
        IUnknown *pUnk20 = static_cast<IUnknown *>(pThis20); // Verify that INTERFACE_T20 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[20] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
           { pUnk16, &__uuidof(INTERFACE_T16) },
           { pUnk17, &__uuidof(INTERFACE_T17) },
           { pUnk18, &__uuidof(INTERFACE_T18) },
           { pUnk19, &__uuidof(INTERFACE_T19) },
           { pUnk20, &__uuidof(INTERFACE_T20) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15, typename INTERFACE_T16, typename INTERFACE_T17, typename INTERFACE_T18, typename INTERFACE_T19, typename INTERFACE_T20, typename INTERFACE_T21>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, _In_ INTERFACE_T16 *pThis16, _In_ INTERFACE_T17 *pThis17, _In_ INTERFACE_T18 *pThis18, _In_ INTERFACE_T19 *pThis19, _In_ INTERFACE_T20 *pThis20, _In_ INTERFACE_T21 *pThis21, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface
        IUnknown *pUnk16 = static_cast<IUnknown *>(pThis16); // Verify that INTERFACE_T16 is a COM interface
        IUnknown *pUnk17 = static_cast<IUnknown *>(pThis17); // Verify that INTERFACE_T17 is a COM interface
        IUnknown *pUnk18 = static_cast<IUnknown *>(pThis18); // Verify that INTERFACE_T18 is a COM interface
        IUnknown *pUnk19 = static_cast<IUnknown *>(pThis19); // Verify that INTERFACE_T19 is a COM interface
        IUnknown *pUnk20 = static_cast<IUnknown *>(pThis20); // Verify that INTERFACE_T20 is a COM interface
        IUnknown *pUnk21 = static_cast<IUnknown *>(pThis21); // Verify that INTERFACE_T21 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[21] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
           { pUnk16, &__uuidof(INTERFACE_T16) },
           { pUnk17, &__uuidof(INTERFACE_T17) },
           { pUnk18, &__uuidof(INTERFACE_T18) },
           { pUnk19, &__uuidof(INTERFACE_T19) },
           { pUnk20, &__uuidof(INTERFACE_T20) },
           { pUnk21, &__uuidof(INTERFACE_T21) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15, typename INTERFACE_T16, typename INTERFACE_T17, typename INTERFACE_T18, typename INTERFACE_T19, typename INTERFACE_T20, typename INTERFACE_T21, typename INTERFACE_T22>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, _In_ INTERFACE_T16 *pThis16, _In_ INTERFACE_T17 *pThis17, _In_ INTERFACE_T18 *pThis18, _In_ INTERFACE_T19 *pThis19, _In_ INTERFACE_T20 *pThis20, _In_ INTERFACE_T21 *pThis21, _In_ INTERFACE_T22 *pThis22, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface
        IUnknown *pUnk16 = static_cast<IUnknown *>(pThis16); // Verify that INTERFACE_T16 is a COM interface
        IUnknown *pUnk17 = static_cast<IUnknown *>(pThis17); // Verify that INTERFACE_T17 is a COM interface
        IUnknown *pUnk18 = static_cast<IUnknown *>(pThis18); // Verify that INTERFACE_T18 is a COM interface
        IUnknown *pUnk19 = static_cast<IUnknown *>(pThis19); // Verify that INTERFACE_T19 is a COM interface
        IUnknown *pUnk20 = static_cast<IUnknown *>(pThis20); // Verify that INTERFACE_T20 is a COM interface
        IUnknown *pUnk21 = static_cast<IUnknown *>(pThis21); // Verify that INTERFACE_T21 is a COM interface
        IUnknown *pUnk22 = static_cast<IUnknown *>(pThis22); // Verify that INTERFACE_T22 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[22] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
           { pUnk16, &__uuidof(INTERFACE_T16) },
           { pUnk17, &__uuidof(INTERFACE_T17) },
           { pUnk18, &__uuidof(INTERFACE_T18) },
           { pUnk19, &__uuidof(INTERFACE_T19) },
           { pUnk20, &__uuidof(INTERFACE_T20) },
           { pUnk21, &__uuidof(INTERFACE_T21) },
           { pUnk22, &__uuidof(INTERFACE_T22) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15, typename INTERFACE_T16, typename INTERFACE_T17, typename INTERFACE_T18, typename INTERFACE_T19, typename INTERFACE_T20, typename INTERFACE_T21, typename INTERFACE_T22, typename INTERFACE_T23>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, _In_ INTERFACE_T16 *pThis16, _In_ INTERFACE_T17 *pThis17, _In_ INTERFACE_T18 *pThis18, _In_ INTERFACE_T19 *pThis19, _In_ INTERFACE_T20 *pThis20, _In_ INTERFACE_T21 *pThis21, _In_ INTERFACE_T22 *pThis22, _In_ INTERFACE_T23 *pThis23, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface
        IUnknown *pUnk16 = static_cast<IUnknown *>(pThis16); // Verify that INTERFACE_T16 is a COM interface
        IUnknown *pUnk17 = static_cast<IUnknown *>(pThis17); // Verify that INTERFACE_T17 is a COM interface
        IUnknown *pUnk18 = static_cast<IUnknown *>(pThis18); // Verify that INTERFACE_T18 is a COM interface
        IUnknown *pUnk19 = static_cast<IUnknown *>(pThis19); // Verify that INTERFACE_T19 is a COM interface
        IUnknown *pUnk20 = static_cast<IUnknown *>(pThis20); // Verify that INTERFACE_T20 is a COM interface
        IUnknown *pUnk21 = static_cast<IUnknown *>(pThis21); // Verify that INTERFACE_T21 is a COM interface
        IUnknown *pUnk22 = static_cast<IUnknown *>(pThis22); // Verify that INTERFACE_T22 is a COM interface
        IUnknown *pUnk23 = static_cast<IUnknown *>(pThis23); // Verify that INTERFACE_T23 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[23] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
           { pUnk16, &__uuidof(INTERFACE_T16) },
           { pUnk17, &__uuidof(INTERFACE_T17) },
           { pUnk18, &__uuidof(INTERFACE_T18) },
           { pUnk19, &__uuidof(INTERFACE_T19) },
           { pUnk20, &__uuidof(INTERFACE_T20) },
           { pUnk21, &__uuidof(INTERFACE_T21) },
           { pUnk22, &__uuidof(INTERFACE_T22) },
           { pUnk23, &__uuidof(INTERFACE_T23) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15, typename INTERFACE_T16, typename INTERFACE_T17, typename INTERFACE_T18, typename INTERFACE_T19, typename INTERFACE_T20, typename INTERFACE_T21, typename INTERFACE_T22, typename INTERFACE_T23, typename INTERFACE_T24>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, _In_ INTERFACE_T16 *pThis16, _In_ INTERFACE_T17 *pThis17, _In_ INTERFACE_T18 *pThis18, _In_ INTERFACE_T19 *pThis19, _In_ INTERFACE_T20 *pThis20, _In_ INTERFACE_T21 *pThis21, _In_ INTERFACE_T22 *pThis22, _In_ INTERFACE_T23 *pThis23, _In_ INTERFACE_T24 *pThis24, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface
        IUnknown *pUnk16 = static_cast<IUnknown *>(pThis16); // Verify that INTERFACE_T16 is a COM interface
        IUnknown *pUnk17 = static_cast<IUnknown *>(pThis17); // Verify that INTERFACE_T17 is a COM interface
        IUnknown *pUnk18 = static_cast<IUnknown *>(pThis18); // Verify that INTERFACE_T18 is a COM interface
        IUnknown *pUnk19 = static_cast<IUnknown *>(pThis19); // Verify that INTERFACE_T19 is a COM interface
        IUnknown *pUnk20 = static_cast<IUnknown *>(pThis20); // Verify that INTERFACE_T20 is a COM interface
        IUnknown *pUnk21 = static_cast<IUnknown *>(pThis21); // Verify that INTERFACE_T21 is a COM interface
        IUnknown *pUnk22 = static_cast<IUnknown *>(pThis22); // Verify that INTERFACE_T22 is a COM interface
        IUnknown *pUnk23 = static_cast<IUnknown *>(pThis23); // Verify that INTERFACE_T23 is a COM interface
        IUnknown *pUnk24 = static_cast<IUnknown *>(pThis24); // Verify that INTERFACE_T24 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[24] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
           { pUnk16, &__uuidof(INTERFACE_T16) },
           { pUnk17, &__uuidof(INTERFACE_T17) },
           { pUnk18, &__uuidof(INTERFACE_T18) },
           { pUnk19, &__uuidof(INTERFACE_T19) },
           { pUnk20, &__uuidof(INTERFACE_T20) },
           { pUnk21, &__uuidof(INTERFACE_T21) },
           { pUnk22, &__uuidof(INTERFACE_T22) },
           { pUnk23, &__uuidof(INTERFACE_T23) },
           { pUnk24, &__uuidof(INTERFACE_T24) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15, typename INTERFACE_T16, typename INTERFACE_T17, typename INTERFACE_T18, typename INTERFACE_T19, typename INTERFACE_T20, typename INTERFACE_T21, typename INTERFACE_T22, typename INTERFACE_T23, typename INTERFACE_T24, typename INTERFACE_T25>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, _In_ INTERFACE_T16 *pThis16, _In_ INTERFACE_T17 *pThis17, _In_ INTERFACE_T18 *pThis18, _In_ INTERFACE_T19 *pThis19, _In_ INTERFACE_T20 *pThis20, _In_ INTERFACE_T21 *pThis21, _In_ INTERFACE_T22 *pThis22, _In_ INTERFACE_T23 *pThis23, _In_ INTERFACE_T24 *pThis24, _In_ INTERFACE_T25 *pThis25, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface
        IUnknown *pUnk16 = static_cast<IUnknown *>(pThis16); // Verify that INTERFACE_T16 is a COM interface
        IUnknown *pUnk17 = static_cast<IUnknown *>(pThis17); // Verify that INTERFACE_T17 is a COM interface
        IUnknown *pUnk18 = static_cast<IUnknown *>(pThis18); // Verify that INTERFACE_T18 is a COM interface
        IUnknown *pUnk19 = static_cast<IUnknown *>(pThis19); // Verify that INTERFACE_T19 is a COM interface
        IUnknown *pUnk20 = static_cast<IUnknown *>(pThis20); // Verify that INTERFACE_T20 is a COM interface
        IUnknown *pUnk21 = static_cast<IUnknown *>(pThis21); // Verify that INTERFACE_T21 is a COM interface
        IUnknown *pUnk22 = static_cast<IUnknown *>(pThis22); // Verify that INTERFACE_T22 is a COM interface
        IUnknown *pUnk23 = static_cast<IUnknown *>(pThis23); // Verify that INTERFACE_T23 is a COM interface
        IUnknown *pUnk24 = static_cast<IUnknown *>(pThis24); // Verify that INTERFACE_T24 is a COM interface
        IUnknown *pUnk25 = static_cast<IUnknown *>(pThis25); // Verify that INTERFACE_T25 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[25] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
           { pUnk16, &__uuidof(INTERFACE_T16) },
           { pUnk17, &__uuidof(INTERFACE_T17) },
           { pUnk18, &__uuidof(INTERFACE_T18) },
           { pUnk19, &__uuidof(INTERFACE_T19) },
           { pUnk20, &__uuidof(INTERFACE_T20) },
           { pUnk21, &__uuidof(INTERFACE_T21) },
           { pUnk22, &__uuidof(INTERFACE_T22) },
           { pUnk23, &__uuidof(INTERFACE_T23) },
           { pUnk24, &__uuidof(INTERFACE_T24) },
           { pUnk25, &__uuidof(INTERFACE_T25) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15, typename INTERFACE_T16, typename INTERFACE_T17, typename INTERFACE_T18, typename INTERFACE_T19, typename INTERFACE_T20, typename INTERFACE_T21, typename INTERFACE_T22, typename INTERFACE_T23, typename INTERFACE_T24, typename INTERFACE_T25, typename INTERFACE_T26>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, _In_ INTERFACE_T16 *pThis16, _In_ INTERFACE_T17 *pThis17, _In_ INTERFACE_T18 *pThis18, _In_ INTERFACE_T19 *pThis19, _In_ INTERFACE_T20 *pThis20, _In_ INTERFACE_T21 *pThis21, _In_ INTERFACE_T22 *pThis22, _In_ INTERFACE_T23 *pThis23, _In_ INTERFACE_T24 *pThis24, _In_ INTERFACE_T25 *pThis25, _In_ INTERFACE_T26 *pThis26, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface
        IUnknown *pUnk16 = static_cast<IUnknown *>(pThis16); // Verify that INTERFACE_T16 is a COM interface
        IUnknown *pUnk17 = static_cast<IUnknown *>(pThis17); // Verify that INTERFACE_T17 is a COM interface
        IUnknown *pUnk18 = static_cast<IUnknown *>(pThis18); // Verify that INTERFACE_T18 is a COM interface
        IUnknown *pUnk19 = static_cast<IUnknown *>(pThis19); // Verify that INTERFACE_T19 is a COM interface
        IUnknown *pUnk20 = static_cast<IUnknown *>(pThis20); // Verify that INTERFACE_T20 is a COM interface
        IUnknown *pUnk21 = static_cast<IUnknown *>(pThis21); // Verify that INTERFACE_T21 is a COM interface
        IUnknown *pUnk22 = static_cast<IUnknown *>(pThis22); // Verify that INTERFACE_T22 is a COM interface
        IUnknown *pUnk23 = static_cast<IUnknown *>(pThis23); // Verify that INTERFACE_T23 is a COM interface
        IUnknown *pUnk24 = static_cast<IUnknown *>(pThis24); // Verify that INTERFACE_T24 is a COM interface
        IUnknown *pUnk25 = static_cast<IUnknown *>(pThis25); // Verify that INTERFACE_T25 is a COM interface
        IUnknown *pUnk26 = static_cast<IUnknown *>(pThis26); // Verify that INTERFACE_T26 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[26] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
           { pUnk16, &__uuidof(INTERFACE_T16) },
           { pUnk17, &__uuidof(INTERFACE_T17) },
           { pUnk18, &__uuidof(INTERFACE_T18) },
           { pUnk19, &__uuidof(INTERFACE_T19) },
           { pUnk20, &__uuidof(INTERFACE_T20) },
           { pUnk21, &__uuidof(INTERFACE_T21) },
           { pUnk22, &__uuidof(INTERFACE_T22) },
           { pUnk23, &__uuidof(INTERFACE_T23) },
           { pUnk24, &__uuidof(INTERFACE_T24) },
           { pUnk25, &__uuidof(INTERFACE_T25) },
           { pUnk26, &__uuidof(INTERFACE_T26) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15, typename INTERFACE_T16, typename INTERFACE_T17, typename INTERFACE_T18, typename INTERFACE_T19, typename INTERFACE_T20, typename INTERFACE_T21, typename INTERFACE_T22, typename INTERFACE_T23, typename INTERFACE_T24, typename INTERFACE_T25, typename INTERFACE_T26, typename INTERFACE_T27>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, _In_ INTERFACE_T16 *pThis16, _In_ INTERFACE_T17 *pThis17, _In_ INTERFACE_T18 *pThis18, _In_ INTERFACE_T19 *pThis19, _In_ INTERFACE_T20 *pThis20, _In_ INTERFACE_T21 *pThis21, _In_ INTERFACE_T22 *pThis22, _In_ INTERFACE_T23 *pThis23, _In_ INTERFACE_T24 *pThis24, _In_ INTERFACE_T25 *pThis25, _In_ INTERFACE_T26 *pThis26, _In_ INTERFACE_T27 *pThis27, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface
        IUnknown *pUnk16 = static_cast<IUnknown *>(pThis16); // Verify that INTERFACE_T16 is a COM interface
        IUnknown *pUnk17 = static_cast<IUnknown *>(pThis17); // Verify that INTERFACE_T17 is a COM interface
        IUnknown *pUnk18 = static_cast<IUnknown *>(pThis18); // Verify that INTERFACE_T18 is a COM interface
        IUnknown *pUnk19 = static_cast<IUnknown *>(pThis19); // Verify that INTERFACE_T19 is a COM interface
        IUnknown *pUnk20 = static_cast<IUnknown *>(pThis20); // Verify that INTERFACE_T20 is a COM interface
        IUnknown *pUnk21 = static_cast<IUnknown *>(pThis21); // Verify that INTERFACE_T21 is a COM interface
        IUnknown *pUnk22 = static_cast<IUnknown *>(pThis22); // Verify that INTERFACE_T22 is a COM interface
        IUnknown *pUnk23 = static_cast<IUnknown *>(pThis23); // Verify that INTERFACE_T23 is a COM interface
        IUnknown *pUnk24 = static_cast<IUnknown *>(pThis24); // Verify that INTERFACE_T24 is a COM interface
        IUnknown *pUnk25 = static_cast<IUnknown *>(pThis25); // Verify that INTERFACE_T25 is a COM interface
        IUnknown *pUnk26 = static_cast<IUnknown *>(pThis26); // Verify that INTERFACE_T26 is a COM interface
        IUnknown *pUnk27 = static_cast<IUnknown *>(pThis27); // Verify that INTERFACE_T27 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[27] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
           { pUnk16, &__uuidof(INTERFACE_T16) },
           { pUnk17, &__uuidof(INTERFACE_T17) },
           { pUnk18, &__uuidof(INTERFACE_T18) },
           { pUnk19, &__uuidof(INTERFACE_T19) },
           { pUnk20, &__uuidof(INTERFACE_T20) },
           { pUnk21, &__uuidof(INTERFACE_T21) },
           { pUnk22, &__uuidof(INTERFACE_T22) },
           { pUnk23, &__uuidof(INTERFACE_T23) },
           { pUnk24, &__uuidof(INTERFACE_T24) },
           { pUnk25, &__uuidof(INTERFACE_T25) },
           { pUnk26, &__uuidof(INTERFACE_T26) },
           { pUnk27, &__uuidof(INTERFACE_T27) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15, typename INTERFACE_T16, typename INTERFACE_T17, typename INTERFACE_T18, typename INTERFACE_T19, typename INTERFACE_T20, typename INTERFACE_T21, typename INTERFACE_T22, typename INTERFACE_T23, typename INTERFACE_T24, typename INTERFACE_T25, typename INTERFACE_T26, typename INTERFACE_T27, typename INTERFACE_T28>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, _In_ INTERFACE_T16 *pThis16, _In_ INTERFACE_T17 *pThis17, _In_ INTERFACE_T18 *pThis18, _In_ INTERFACE_T19 *pThis19, _In_ INTERFACE_T20 *pThis20, _In_ INTERFACE_T21 *pThis21, _In_ INTERFACE_T22 *pThis22, _In_ INTERFACE_T23 *pThis23, _In_ INTERFACE_T24 *pThis24, _In_ INTERFACE_T25 *pThis25, _In_ INTERFACE_T26 *pThis26, _In_ INTERFACE_T27 *pThis27, _In_ INTERFACE_T28 *pThis28, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface
        IUnknown *pUnk16 = static_cast<IUnknown *>(pThis16); // Verify that INTERFACE_T16 is a COM interface
        IUnknown *pUnk17 = static_cast<IUnknown *>(pThis17); // Verify that INTERFACE_T17 is a COM interface
        IUnknown *pUnk18 = static_cast<IUnknown *>(pThis18); // Verify that INTERFACE_T18 is a COM interface
        IUnknown *pUnk19 = static_cast<IUnknown *>(pThis19); // Verify that INTERFACE_T19 is a COM interface
        IUnknown *pUnk20 = static_cast<IUnknown *>(pThis20); // Verify that INTERFACE_T20 is a COM interface
        IUnknown *pUnk21 = static_cast<IUnknown *>(pThis21); // Verify that INTERFACE_T21 is a COM interface
        IUnknown *pUnk22 = static_cast<IUnknown *>(pThis22); // Verify that INTERFACE_T22 is a COM interface
        IUnknown *pUnk23 = static_cast<IUnknown *>(pThis23); // Verify that INTERFACE_T23 is a COM interface
        IUnknown *pUnk24 = static_cast<IUnknown *>(pThis24); // Verify that INTERFACE_T24 is a COM interface
        IUnknown *pUnk25 = static_cast<IUnknown *>(pThis25); // Verify that INTERFACE_T25 is a COM interface
        IUnknown *pUnk26 = static_cast<IUnknown *>(pThis26); // Verify that INTERFACE_T26 is a COM interface
        IUnknown *pUnk27 = static_cast<IUnknown *>(pThis27); // Verify that INTERFACE_T27 is a COM interface
        IUnknown *pUnk28 = static_cast<IUnknown *>(pThis28); // Verify that INTERFACE_T28 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[28] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
           { pUnk16, &__uuidof(INTERFACE_T16) },
           { pUnk17, &__uuidof(INTERFACE_T17) },
           { pUnk18, &__uuidof(INTERFACE_T18) },
           { pUnk19, &__uuidof(INTERFACE_T19) },
           { pUnk20, &__uuidof(INTERFACE_T20) },
           { pUnk21, &__uuidof(INTERFACE_T21) },
           { pUnk22, &__uuidof(INTERFACE_T22) },
           { pUnk23, &__uuidof(INTERFACE_T23) },
           { pUnk24, &__uuidof(INTERFACE_T24) },
           { pUnk25, &__uuidof(INTERFACE_T25) },
           { pUnk26, &__uuidof(INTERFACE_T26) },
           { pUnk27, &__uuidof(INTERFACE_T27) },
           { pUnk28, &__uuidof(INTERFACE_T28) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15, typename INTERFACE_T16, typename INTERFACE_T17, typename INTERFACE_T18, typename INTERFACE_T19, typename INTERFACE_T20, typename INTERFACE_T21, typename INTERFACE_T22, typename INTERFACE_T23, typename INTERFACE_T24, typename INTERFACE_T25, typename INTERFACE_T26, typename INTERFACE_T27, typename INTERFACE_T28, typename INTERFACE_T29>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, _In_ INTERFACE_T16 *pThis16, _In_ INTERFACE_T17 *pThis17, _In_ INTERFACE_T18 *pThis18, _In_ INTERFACE_T19 *pThis19, _In_ INTERFACE_T20 *pThis20, _In_ INTERFACE_T21 *pThis21, _In_ INTERFACE_T22 *pThis22, _In_ INTERFACE_T23 *pThis23, _In_ INTERFACE_T24 *pThis24, _In_ INTERFACE_T25 *pThis25, _In_ INTERFACE_T26 *pThis26, _In_ INTERFACE_T27 *pThis27, _In_ INTERFACE_T28 *pThis28, _In_ INTERFACE_T29 *pThis29, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface
        IUnknown *pUnk16 = static_cast<IUnknown *>(pThis16); // Verify that INTERFACE_T16 is a COM interface
        IUnknown *pUnk17 = static_cast<IUnknown *>(pThis17); // Verify that INTERFACE_T17 is a COM interface
        IUnknown *pUnk18 = static_cast<IUnknown *>(pThis18); // Verify that INTERFACE_T18 is a COM interface
        IUnknown *pUnk19 = static_cast<IUnknown *>(pThis19); // Verify that INTERFACE_T19 is a COM interface
        IUnknown *pUnk20 = static_cast<IUnknown *>(pThis20); // Verify that INTERFACE_T20 is a COM interface
        IUnknown *pUnk21 = static_cast<IUnknown *>(pThis21); // Verify that INTERFACE_T21 is a COM interface
        IUnknown *pUnk22 = static_cast<IUnknown *>(pThis22); // Verify that INTERFACE_T22 is a COM interface
        IUnknown *pUnk23 = static_cast<IUnknown *>(pThis23); // Verify that INTERFACE_T23 is a COM interface
        IUnknown *pUnk24 = static_cast<IUnknown *>(pThis24); // Verify that INTERFACE_T24 is a COM interface
        IUnknown *pUnk25 = static_cast<IUnknown *>(pThis25); // Verify that INTERFACE_T25 is a COM interface
        IUnknown *pUnk26 = static_cast<IUnknown *>(pThis26); // Verify that INTERFACE_T26 is a COM interface
        IUnknown *pUnk27 = static_cast<IUnknown *>(pThis27); // Verify that INTERFACE_T27 is a COM interface
        IUnknown *pUnk28 = static_cast<IUnknown *>(pThis28); // Verify that INTERFACE_T28 is a COM interface
        IUnknown *pUnk29 = static_cast<IUnknown *>(pThis29); // Verify that INTERFACE_T29 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[29] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
           { pUnk16, &__uuidof(INTERFACE_T16) },
           { pUnk17, &__uuidof(INTERFACE_T17) },
           { pUnk18, &__uuidof(INTERFACE_T18) },
           { pUnk19, &__uuidof(INTERFACE_T19) },
           { pUnk20, &__uuidof(INTERFACE_T20) },
           { pUnk21, &__uuidof(INTERFACE_T21) },
           { pUnk22, &__uuidof(INTERFACE_T22) },
           { pUnk23, &__uuidof(INTERFACE_T23) },
           { pUnk24, &__uuidof(INTERFACE_T24) },
           { pUnk25, &__uuidof(INTERFACE_T25) },
           { pUnk26, &__uuidof(INTERFACE_T26) },
           { pUnk27, &__uuidof(INTERFACE_T27) },
           { pUnk28, &__uuidof(INTERFACE_T28) },
           { pUnk29, &__uuidof(INTERFACE_T29) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15, typename INTERFACE_T16, typename INTERFACE_T17, typename INTERFACE_T18, typename INTERFACE_T19, typename INTERFACE_T20, typename INTERFACE_T21, typename INTERFACE_T22, typename INTERFACE_T23, typename INTERFACE_T24, typename INTERFACE_T25, typename INTERFACE_T26, typename INTERFACE_T27, typename INTERFACE_T28, typename INTERFACE_T29, typename INTERFACE_T30>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, _In_ INTERFACE_T16 *pThis16, _In_ INTERFACE_T17 *pThis17, _In_ INTERFACE_T18 *pThis18, _In_ INTERFACE_T19 *pThis19, _In_ INTERFACE_T20 *pThis20, _In_ INTERFACE_T21 *pThis21, _In_ INTERFACE_T22 *pThis22, _In_ INTERFACE_T23 *pThis23, _In_ INTERFACE_T24 *pThis24, _In_ INTERFACE_T25 *pThis25, _In_ INTERFACE_T26 *pThis26, _In_ INTERFACE_T27 *pThis27, _In_ INTERFACE_T28 *pThis28, _In_ INTERFACE_T29 *pThis29, _In_ INTERFACE_T30 *pThis30, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface
        IUnknown *pUnk16 = static_cast<IUnknown *>(pThis16); // Verify that INTERFACE_T16 is a COM interface
        IUnknown *pUnk17 = static_cast<IUnknown *>(pThis17); // Verify that INTERFACE_T17 is a COM interface
        IUnknown *pUnk18 = static_cast<IUnknown *>(pThis18); // Verify that INTERFACE_T18 is a COM interface
        IUnknown *pUnk19 = static_cast<IUnknown *>(pThis19); // Verify that INTERFACE_T19 is a COM interface
        IUnknown *pUnk20 = static_cast<IUnknown *>(pThis20); // Verify that INTERFACE_T20 is a COM interface
        IUnknown *pUnk21 = static_cast<IUnknown *>(pThis21); // Verify that INTERFACE_T21 is a COM interface
        IUnknown *pUnk22 = static_cast<IUnknown *>(pThis22); // Verify that INTERFACE_T22 is a COM interface
        IUnknown *pUnk23 = static_cast<IUnknown *>(pThis23); // Verify that INTERFACE_T23 is a COM interface
        IUnknown *pUnk24 = static_cast<IUnknown *>(pThis24); // Verify that INTERFACE_T24 is a COM interface
        IUnknown *pUnk25 = static_cast<IUnknown *>(pThis25); // Verify that INTERFACE_T25 is a COM interface
        IUnknown *pUnk26 = static_cast<IUnknown *>(pThis26); // Verify that INTERFACE_T26 is a COM interface
        IUnknown *pUnk27 = static_cast<IUnknown *>(pThis27); // Verify that INTERFACE_T27 is a COM interface
        IUnknown *pUnk28 = static_cast<IUnknown *>(pThis28); // Verify that INTERFACE_T28 is a COM interface
        IUnknown *pUnk29 = static_cast<IUnknown *>(pThis29); // Verify that INTERFACE_T29 is a COM interface
        IUnknown *pUnk30 = static_cast<IUnknown *>(pThis30); // Verify that INTERFACE_T30 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[30] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
           { pUnk16, &__uuidof(INTERFACE_T16) },
           { pUnk17, &__uuidof(INTERFACE_T17) },
           { pUnk18, &__uuidof(INTERFACE_T18) },
           { pUnk19, &__uuidof(INTERFACE_T19) },
           { pUnk20, &__uuidof(INTERFACE_T20) },
           { pUnk21, &__uuidof(INTERFACE_T21) },
           { pUnk22, &__uuidof(INTERFACE_T22) },
           { pUnk23, &__uuidof(INTERFACE_T23) },
           { pUnk24, &__uuidof(INTERFACE_T24) },
           { pUnk25, &__uuidof(INTERFACE_T25) },
           { pUnk26, &__uuidof(INTERFACE_T26) },
           { pUnk27, &__uuidof(INTERFACE_T27) },
           { pUnk28, &__uuidof(INTERFACE_T28) },
           { pUnk29, &__uuidof(INTERFACE_T29) },
           { pUnk30, &__uuidof(INTERFACE_T30) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15, typename INTERFACE_T16, typename INTERFACE_T17, typename INTERFACE_T18, typename INTERFACE_T19, typename INTERFACE_T20, typename INTERFACE_T21, typename INTERFACE_T22, typename INTERFACE_T23, typename INTERFACE_T24, typename INTERFACE_T25, typename INTERFACE_T26, typename INTERFACE_T27, typename INTERFACE_T28, typename INTERFACE_T29, typename INTERFACE_T30, typename INTERFACE_T31>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, _In_ INTERFACE_T16 *pThis16, _In_ INTERFACE_T17 *pThis17, _In_ INTERFACE_T18 *pThis18, _In_ INTERFACE_T19 *pThis19, _In_ INTERFACE_T20 *pThis20, _In_ INTERFACE_T21 *pThis21, _In_ INTERFACE_T22 *pThis22, _In_ INTERFACE_T23 *pThis23, _In_ INTERFACE_T24 *pThis24, _In_ INTERFACE_T25 *pThis25, _In_ INTERFACE_T26 *pThis26, _In_ INTERFACE_T27 *pThis27, _In_ INTERFACE_T28 *pThis28, _In_ INTERFACE_T29 *pThis29, _In_ INTERFACE_T30 *pThis30, _In_ INTERFACE_T31 *pThis31, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface
        IUnknown *pUnk16 = static_cast<IUnknown *>(pThis16); // Verify that INTERFACE_T16 is a COM interface
        IUnknown *pUnk17 = static_cast<IUnknown *>(pThis17); // Verify that INTERFACE_T17 is a COM interface
        IUnknown *pUnk18 = static_cast<IUnknown *>(pThis18); // Verify that INTERFACE_T18 is a COM interface
        IUnknown *pUnk19 = static_cast<IUnknown *>(pThis19); // Verify that INTERFACE_T19 is a COM interface
        IUnknown *pUnk20 = static_cast<IUnknown *>(pThis20); // Verify that INTERFACE_T20 is a COM interface
        IUnknown *pUnk21 = static_cast<IUnknown *>(pThis21); // Verify that INTERFACE_T21 is a COM interface
        IUnknown *pUnk22 = static_cast<IUnknown *>(pThis22); // Verify that INTERFACE_T22 is a COM interface
        IUnknown *pUnk23 = static_cast<IUnknown *>(pThis23); // Verify that INTERFACE_T23 is a COM interface
        IUnknown *pUnk24 = static_cast<IUnknown *>(pThis24); // Verify that INTERFACE_T24 is a COM interface
        IUnknown *pUnk25 = static_cast<IUnknown *>(pThis25); // Verify that INTERFACE_T25 is a COM interface
        IUnknown *pUnk26 = static_cast<IUnknown *>(pThis26); // Verify that INTERFACE_T26 is a COM interface
        IUnknown *pUnk27 = static_cast<IUnknown *>(pThis27); // Verify that INTERFACE_T27 is a COM interface
        IUnknown *pUnk28 = static_cast<IUnknown *>(pThis28); // Verify that INTERFACE_T28 is a COM interface
        IUnknown *pUnk29 = static_cast<IUnknown *>(pThis29); // Verify that INTERFACE_T29 is a COM interface
        IUnknown *pUnk30 = static_cast<IUnknown *>(pThis30); // Verify that INTERFACE_T30 is a COM interface
        IUnknown *pUnk31 = static_cast<IUnknown *>(pThis31); // Verify that INTERFACE_T31 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[31] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
           { pUnk16, &__uuidof(INTERFACE_T16) },
           { pUnk17, &__uuidof(INTERFACE_T17) },
           { pUnk18, &__uuidof(INTERFACE_T18) },
           { pUnk19, &__uuidof(INTERFACE_T19) },
           { pUnk20, &__uuidof(INTERFACE_T20) },
           { pUnk21, &__uuidof(INTERFACE_T21) },
           { pUnk22, &__uuidof(INTERFACE_T22) },
           { pUnk23, &__uuidof(INTERFACE_T23) },
           { pUnk24, &__uuidof(INTERFACE_T24) },
           { pUnk25, &__uuidof(INTERFACE_T25) },
           { pUnk26, &__uuidof(INTERFACE_T26) },
           { pUnk27, &__uuidof(INTERFACE_T27) },
           { pUnk28, &__uuidof(INTERFACE_T28) },
           { pUnk29, &__uuidof(INTERFACE_T29) },
           { pUnk30, &__uuidof(INTERFACE_T30) },
           { pUnk31, &__uuidof(INTERFACE_T31) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }

    template <typename INTERFACE_T1, typename INTERFACE_T2, typename INTERFACE_T3, typename INTERFACE_T4, typename INTERFACE_T5, typename INTERFACE_T6, typename INTERFACE_T7, typename INTERFACE_T8, typename INTERFACE_T9, typename INTERFACE_T10, typename INTERFACE_T11, typename INTERFACE_T12, typename INTERFACE_T13, typename INTERFACE_T14, typename INTERFACE_T15, typename INTERFACE_T16, typename INTERFACE_T17, typename INTERFACE_T18, typename INTERFACE_T19, typename INTERFACE_T20, typename INTERFACE_T21, typename INTERFACE_T22, typename INTERFACE_T23, typename INTERFACE_T24, typename INTERFACE_T25, typename INTERFACE_T26, typename INTERFACE_T27, typename INTERFACE_T28, typename INTERFACE_T29, typename INTERFACE_T30, typename INTERFACE_T31, typename INTERFACE_T32>
    inline HRESULT ImplQueryInterface(_In_ INTERFACE_T1 *pThis1, _In_ INTERFACE_T2 *pThis2, _In_ INTERFACE_T3 *pThis3, _In_ INTERFACE_T4 *pThis4, _In_ INTERFACE_T5 *pThis5, _In_ INTERFACE_T6 *pThis6, _In_ INTERFACE_T7 *pThis7, _In_ INTERFACE_T8 *pThis8, _In_ INTERFACE_T9 *pThis9, _In_ INTERFACE_T10 *pThis10, _In_ INTERFACE_T11 *pThis11, _In_ INTERFACE_T12 *pThis12, _In_ INTERFACE_T13 *pThis13, _In_ INTERFACE_T14 *pThis14, _In_ INTERFACE_T15 *pThis15, _In_ INTERFACE_T16 *pThis16, _In_ INTERFACE_T17 *pThis17, _In_ INTERFACE_T18 *pThis18, _In_ INTERFACE_T19 *pThis19, _In_ INTERFACE_T20 *pThis20, _In_ INTERFACE_T21 *pThis21, _In_ INTERFACE_T22 *pThis22, _In_ INTERFACE_T23 *pThis23, _In_ INTERFACE_T24 *pThis24, _In_ INTERFACE_T25 *pThis25, _In_ INTERFACE_T26 *pThis26, _In_ INTERFACE_T27 *pThis27, _In_ INTERFACE_T28 *pThis28, _In_ INTERFACE_T29 *pThis29, _In_ INTERFACE_T30 *pThis30, _In_ INTERFACE_T31 *pThis31, _In_ INTERFACE_T32 *pThis32, REFIID riid, _Deref_out_ PVOID *ppvObject)
    {
        IUnknown *pUnk1 = static_cast<IUnknown *>(pThis1); // Verify that INTERFACE_T1 is a COM interface
        IUnknown *pUnk2 = static_cast<IUnknown *>(pThis2); // Verify that INTERFACE_T2 is a COM interface
        IUnknown *pUnk3 = static_cast<IUnknown *>(pThis3); // Verify that INTERFACE_T3 is a COM interface
        IUnknown *pUnk4 = static_cast<IUnknown *>(pThis4); // Verify that INTERFACE_T4 is a COM interface
        IUnknown *pUnk5 = static_cast<IUnknown *>(pThis5); // Verify that INTERFACE_T5 is a COM interface
        IUnknown *pUnk6 = static_cast<IUnknown *>(pThis6); // Verify that INTERFACE_T6 is a COM interface
        IUnknown *pUnk7 = static_cast<IUnknown *>(pThis7); // Verify that INTERFACE_T7 is a COM interface
        IUnknown *pUnk8 = static_cast<IUnknown *>(pThis8); // Verify that INTERFACE_T8 is a COM interface
        IUnknown *pUnk9 = static_cast<IUnknown *>(pThis9); // Verify that INTERFACE_T9 is a COM interface
        IUnknown *pUnk10 = static_cast<IUnknown *>(pThis10); // Verify that INTERFACE_T10 is a COM interface
        IUnknown *pUnk11 = static_cast<IUnknown *>(pThis11); // Verify that INTERFACE_T11 is a COM interface
        IUnknown *pUnk12 = static_cast<IUnknown *>(pThis12); // Verify that INTERFACE_T12 is a COM interface
        IUnknown *pUnk13 = static_cast<IUnknown *>(pThis13); // Verify that INTERFACE_T13 is a COM interface
        IUnknown *pUnk14 = static_cast<IUnknown *>(pThis14); // Verify that INTERFACE_T14 is a COM interface
        IUnknown *pUnk15 = static_cast<IUnknown *>(pThis15); // Verify that INTERFACE_T15 is a COM interface
        IUnknown *pUnk16 = static_cast<IUnknown *>(pThis16); // Verify that INTERFACE_T16 is a COM interface
        IUnknown *pUnk17 = static_cast<IUnknown *>(pThis17); // Verify that INTERFACE_T17 is a COM interface
        IUnknown *pUnk18 = static_cast<IUnknown *>(pThis18); // Verify that INTERFACE_T18 is a COM interface
        IUnknown *pUnk19 = static_cast<IUnknown *>(pThis19); // Verify that INTERFACE_T19 is a COM interface
        IUnknown *pUnk20 = static_cast<IUnknown *>(pThis20); // Verify that INTERFACE_T20 is a COM interface
        IUnknown *pUnk21 = static_cast<IUnknown *>(pThis21); // Verify that INTERFACE_T21 is a COM interface
        IUnknown *pUnk22 = static_cast<IUnknown *>(pThis22); // Verify that INTERFACE_T22 is a COM interface
        IUnknown *pUnk23 = static_cast<IUnknown *>(pThis23); // Verify that INTERFACE_T23 is a COM interface
        IUnknown *pUnk24 = static_cast<IUnknown *>(pThis24); // Verify that INTERFACE_T24 is a COM interface
        IUnknown *pUnk25 = static_cast<IUnknown *>(pThis25); // Verify that INTERFACE_T25 is a COM interface
        IUnknown *pUnk26 = static_cast<IUnknown *>(pThis26); // Verify that INTERFACE_T26 is a COM interface
        IUnknown *pUnk27 = static_cast<IUnknown *>(pThis27); // Verify that INTERFACE_T27 is a COM interface
        IUnknown *pUnk28 = static_cast<IUnknown *>(pThis28); // Verify that INTERFACE_T28 is a COM interface
        IUnknown *pUnk29 = static_cast<IUnknown *>(pThis29); // Verify that INTERFACE_T29 is a COM interface
        IUnknown *pUnk30 = static_cast<IUnknown *>(pThis30); // Verify that INTERFACE_T30 is a COM interface
        IUnknown *pUnk31 = static_cast<IUnknown *>(pThis31); // Verify that INTERFACE_T31 is a COM interface
        IUnknown *pUnk32 = static_cast<IUnknown *>(pThis32); // Verify that INTERFACE_T32 is a COM interface

        InternalImplQueryInterface::MULTI_INTERFACE_ELEMENT qiArray[32] =
        {
           { pUnk1, &__uuidof(INTERFACE_T1) },
           { pUnk2, &__uuidof(INTERFACE_T2) },
           { pUnk3, &__uuidof(INTERFACE_T3) },
           { pUnk4, &__uuidof(INTERFACE_T4) },
           { pUnk5, &__uuidof(INTERFACE_T5) },
           { pUnk6, &__uuidof(INTERFACE_T6) },
           { pUnk7, &__uuidof(INTERFACE_T7) },
           { pUnk8, &__uuidof(INTERFACE_T8) },
           { pUnk9, &__uuidof(INTERFACE_T9) },
           { pUnk10, &__uuidof(INTERFACE_T10) },
           { pUnk11, &__uuidof(INTERFACE_T11) },
           { pUnk12, &__uuidof(INTERFACE_T12) },
           { pUnk13, &__uuidof(INTERFACE_T13) },
           { pUnk14, &__uuidof(INTERFACE_T14) },
           { pUnk15, &__uuidof(INTERFACE_T15) },
           { pUnk16, &__uuidof(INTERFACE_T16) },
           { pUnk17, &__uuidof(INTERFACE_T17) },
           { pUnk18, &__uuidof(INTERFACE_T18) },
           { pUnk19, &__uuidof(INTERFACE_T19) },
           { pUnk20, &__uuidof(INTERFACE_T20) },
           { pUnk21, &__uuidof(INTERFACE_T21) },
           { pUnk22, &__uuidof(INTERFACE_T22) },
           { pUnk23, &__uuidof(INTERFACE_T23) },
           { pUnk24, &__uuidof(INTERFACE_T24) },
           { pUnk25, &__uuidof(INTERFACE_T25) },
           { pUnk26, &__uuidof(INTERFACE_T26) },
           { pUnk27, &__uuidof(INTERFACE_T27) },
           { pUnk28, &__uuidof(INTERFACE_T28) },
           { pUnk29, &__uuidof(INTERFACE_T29) },
           { pUnk30, &__uuidof(INTERFACE_T30) },
           { pUnk31, &__uuidof(INTERFACE_T31) },
           { pUnk32, &__uuidof(INTERFACE_T32) },
        };

        return InternalImplQueryInterface::MultiInterfaceImp(qiArray, ARRAYSIZE(qiArray), riid, ppvObject);
    }



    namespace InternalImplQueryInterface
    {
        struct MULTI_INTERFACE_ELEMENT
        {
            IUnknown *pUnknown;
            const IID *iidImplInterface;
        };

        HRESULT MultiInterfaceImp(_In_count_(dwNumElements) const MULTI_INTERFACE_ELEMENT *qiArray, DWORD dwNumElements, REFIID iidReqInterface, _Deref_out_ PVOID *ppvObject);
        HRESULT SingleInterfaceImp(IUnknown *pThis, REFIID iidImplInterface, REFIID iidReqInterface, _Deref_out_ PVOID *ppvObject);

    };
}