// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#include "stdafx.h"
#include "ImplQueryInterface.h"

namespace CommonLib
{
namespace InternalImplQueryInterface
{

HRESULT MultiInterfaceImp(
	_In_count_(dwNumElements) const MULTI_INTERFACE_ELEMENT *qiArray,
    DWORD dwNumElements,
    REFIID iidReqInterface,
    _Deref_out_ PVOID *ppvObject
)
{
    HRESULT hr = S_OK;
    //VSASSERT(dwNumElements > 0, L"");

    if (ppvObject != NULL)
    {
        if (iidReqInterface == IID_IUnknown)
        {
            *ppvObject = qiArray[0].pUnknown;
        }
        else
        {
            PVOID pObject = NULL;

            for (DWORD i = 0; i < dwNumElements; i++)
            {
                if (*qiArray[i].iidImplInterface == iidReqInterface)
                {
                    pObject = qiArray[i].pUnknown;
                }
            }

            if (pObject)
            {
                *ppvObject = pObject;
            }
            else
            {
                *ppvObject = NULL;
                hr = E_NOINTERFACE;
            }
        }
    }
    else
    {
        hr = E_INVALIDARG;
    }

    if (hr == S_OK)
    {
        //VSASSERT(*ppvObject != NULL, L"");
        qiArray[0].pUnknown->AddRef();
    }


    return hr;
}

HRESULT SingleInterfaceImp(IUnknown *pThis, REFIID iidImplInterface, REFIID iidReqInterface, _Deref_out_ PVOID *ppvObject)
{
    HRESULT hr = S_OK;

    if (ppvObject != NULL)
    {
        if (iidReqInterface == iidImplInterface)
        {
            *ppvObject = pThis;
        }
        else if (iidReqInterface == IID_IUnknown)
        {
            *ppvObject = pThis;
        }
        else
        {
            *ppvObject = NULL;
            hr = E_NOINTERFACE;
        }
    }
    else
    {
        hr = E_INVALIDARG;
    }

    if (hr == S_OK)
    {
        //VSASSERT(*ppvObject != NULL, L"");
        pThis->AddRef();
    }

    return hr;
}

}
}
; // end namespace InternalImplQueryInterface