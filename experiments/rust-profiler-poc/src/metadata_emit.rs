// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! IMetaDataEmit and IMetaDataEmit2 COM interface definitions.
//!
//! Derived from Microsoft's cor.h header in the .NET runtime (MIT licensed).
//! Vtable ordering referenced from the Elastic .NET APM agent's Rust profiler
//! (Apache 2.0 licensed) which uses the same `com` crate v0.6.0.
//!
//! IMetaDataEmit provides methods for creating and modifying metadata in the
//! current scope (module). The profiler uses it to define type references,
//! member references, user strings, type specs, and signature tokens needed
//! for the IL injection template.
//!
//! IMetaDataEmit2 extends IMetaDataEmit with DefineMethodSpec for generic
//! method instantiation.

use crate::ffi::*;
use com::{
    interfaces::IUnknown,
    sys::HRESULT,
};
use std::ffi::c_void;

interfaces! {
    /// IMetaDataEmit — metadata writing interface.
    /// GUID: BA3FEE4C-ECB9-4E41-83B7-183FA41CD859
    ///
    /// 49 methods. Methods we call have proper signatures; others use opaque
    /// `*const c_void` parameters for ABI compatibility while maintaining
    /// correct vtable slot positions.
    #[uuid("BA3FEE4C-ECB9-4E41-83B7-183FA41CD859")]
    pub unsafe interface IMetaDataEmit: IUnknown {
        // 1
        pub fn SetModuleProps(&self, szName: LPCWSTR) -> HRESULT;
        // 2
        pub fn Save(&self, szName: LPCWSTR, dwSaveFlags: DWORD) -> HRESULT;
        // 3
        pub fn SaveToStream(&self, pIStream: *const c_void, dwSaveFlags: DWORD) -> HRESULT;
        // 4
        pub fn GetSaveSize(&self, fSave: DWORD, pdwSaveSize: *mut DWORD) -> HRESULT;
        // 5
        pub fn DefineTypeDef(&self,
            szTypeDef: LPCWSTR,
            dwTypeDefFlags: DWORD,
            tkExtends: mdToken,
            rtkImplements: *const mdToken,
            ptd: *mut mdTypeDef,
        ) -> HRESULT;
        // 6
        pub fn DefineNestedType(&self,
            szTypeDef: LPCWSTR,
            dwTypeDefFlags: DWORD,
            tkExtends: mdToken,
            rtkImplements: *const mdToken,
            tdEncloser: mdTypeDef,
            ptd: *mut mdTypeDef,
        ) -> HRESULT;
        // 7
        pub fn SetHandler(&self, pUnk: *const IUnknown) -> HRESULT;
        // 8
        pub fn DefineMethod(&self,
            td: mdTypeDef,
            szName: LPCWSTR,
            dwMethodFlags: DWORD,
            pvSigBlob: PCCOR_SIGNATURE,
            cbSigBlob: ULONG,
            ulCodeRVA: ULONG,
            dwImplFlags: DWORD,
            pmd: *mut mdMethodDef,
        ) -> HRESULT;
        // 9
        pub fn DefineMethodImpl(&self,
            td: mdTypeDef,
            tkBody: mdToken,
            tkDecl: mdToken,
        ) -> HRESULT;
        // 10 ** Used: create TypeRef to external type **
        pub fn DefineTypeRefByName(&self,
            tkResolutionScope: mdToken,
            szName: LPCWSTR,
            ptr: *mut mdTypeRef,
        ) -> HRESULT;
        // 11
        pub fn DefineImportType(&self,
            pAssemImport: *const c_void,
            pbHashValue: *const c_void,
            cbHashValue: ULONG,
            pImport: *const c_void,
            tdImport: mdTypeDef,
            pAssemEmit: *const c_void,
            ptr: *mut mdTypeRef,
        ) -> HRESULT;
        // 12 ** Used: create MemberRef to external method **
        pub fn DefineMemberRef(&self,
            tkImport: mdToken,
            szName: LPCWSTR,
            pvSigBlob: PCCOR_SIGNATURE,
            cbSigBlob: ULONG,
            pmr: *mut mdMemberRef,
        ) -> HRESULT;
        // 13
        pub fn DefineImportMember(&self,
            pAssemImport: *const c_void,
            pbHashValue: *const c_void,
            cbHashValue: ULONG,
            pImport: *const c_void,
            mbMember: mdToken,
            pAssemEmit: *const c_void,
            tkParent: mdToken,
            pmr: *mut mdMemberRef,
        ) -> HRESULT;
        // 14
        pub fn DefineEvent(&self,
            td: mdTypeDef,
            szEvent: LPCWSTR,
            dwEventFlags: DWORD,
            tkEventType: mdToken,
            mdAddOn: mdMethodDef,
            mdRemoveOn: mdMethodDef,
            mdFire: mdMethodDef,
            rmdOtherMethods: *const mdMethodDef,
            pmdEvent: *mut mdToken,
        ) -> HRESULT;
        // 15
        pub fn SetClassLayout(&self,
            td: mdTypeDef,
            dwPackSize: DWORD,
            rFieldOffsets: *const c_void,
            ulClassSize: ULONG,
        ) -> HRESULT;
        // 16
        pub fn DeleteClassLayout(&self, td: mdTypeDef) -> HRESULT;
        // 17
        pub fn SetFieldMarshal(&self,
            tk: mdToken,
            pvNativeType: PCCOR_SIGNATURE,
            cbNativeType: ULONG,
        ) -> HRESULT;
        // 18
        pub fn DeleteFieldMarshal(&self, tk: mdToken) -> HRESULT;
        // 19
        pub fn DefinePermissionSet(&self,
            tk: mdToken,
            dwAction: DWORD,
            pvPermission: *const c_void,
            cbPermission: ULONG,
            ppm: *mut mdToken,
        ) -> HRESULT;
        // 20
        pub fn SetRVA(&self, md: mdMethodDef, ulRVA: ULONG) -> HRESULT;
        // 21 ** Used: get token from local variable signature **
        pub fn GetTokenFromSig(&self,
            pvSig: PCCOR_SIGNATURE,
            cbSig: ULONG,
            pmsig: *mut mdSignature,
        ) -> HRESULT;
        // 22
        pub fn DefineModuleRef(&self, szName: LPCWSTR, pmur: *mut mdToken) -> HRESULT;
        // 23
        pub fn SetParent(&self, mr: mdMemberRef, tk: mdToken) -> HRESULT;
        // 24 ** Used: get/create TypeSpec for generic instantiation **
        pub fn GetTokenFromTypeSpec(&self,
            pvSig: PCCOR_SIGNATURE,
            cbSig: ULONG,
            ptypespec: *mut mdTypeSpec,
        ) -> HRESULT;
        // 25
        pub fn SaveToMemory(&self, pbData: *mut c_void, cbData: ULONG) -> HRESULT;
        // 26 ** Used: create string token for embedded literal **
        pub fn DefineUserString(&self,
            szString: LPCWSTR,
            cchString: ULONG,
            pstk: *mut mdString,
        ) -> HRESULT;
        // 27
        pub fn DeleteToken(&self, tkObj: mdToken) -> HRESULT;
        // 28
        pub fn SetMethodProps(&self,
            md: mdMethodDef,
            dwMethodFlags: DWORD,
            ulCodeRVA: ULONG,
            dwImplFlags: DWORD,
        ) -> HRESULT;
        // 29
        pub fn SetTypeDefProps(&self,
            td: mdTypeDef,
            dwTypeDefFlags: DWORD,
            tkExtends: mdToken,
            rtkImplements: *const mdToken,
        ) -> HRESULT;
        // 30
        pub fn SetEventProps(&self,
            ev: mdToken,
            dwEventFlags: DWORD,
            tkEventType: mdToken,
            mdAddOn: mdMethodDef,
            mdRemoveOn: mdMethodDef,
            mdFire: mdMethodDef,
            rmdOtherMethods: *const mdMethodDef,
        ) -> HRESULT;
        // 31
        pub fn SetPermissionSetProps(&self,
            tk: mdToken,
            dwAction: DWORD,
            pvPermission: *const c_void,
            cbPermission: ULONG,
            ppm: *mut mdToken,
        ) -> HRESULT;
        // 32
        pub fn DefinePinvokeMap(&self,
            tk: mdToken,
            dwMappingFlags: DWORD,
            szImportName: LPCWSTR,
            mrImportDLL: mdToken,
        ) -> HRESULT;
        // 33
        pub fn SetPinvokeMap(&self,
            tk: mdToken,
            dwMappingFlags: DWORD,
            szImportName: LPCWSTR,
            mrImportDLL: mdToken,
        ) -> HRESULT;
        // 34
        pub fn DeletePinvokeMap(&self, tk: mdToken) -> HRESULT;
        // 35
        pub fn DefineCustomAttribute(&self,
            tkOwner: mdToken,
            tkCtor: mdToken,
            pCustomAttribute: *const c_void,
            cbCustomAttribute: ULONG,
            pcv: *mut mdToken,
        ) -> HRESULT;
        // 36
        pub fn SetCustomAttributeValue(&self,
            pcv: mdToken,
            pCustomAttribute: *const c_void,
            cbCustomAttribute: ULONG,
        ) -> HRESULT;
        // 37
        pub fn DefineField(&self,
            td: mdTypeDef,
            szName: LPCWSTR,
            dwFieldFlags: DWORD,
            pvSigBlob: PCCOR_SIGNATURE,
            cbSigBlob: ULONG,
            dwCPlusTypeFlag: DWORD,
            pValue: *const c_void,
            cchValue: ULONG,
            pmd: *mut mdFieldDef,
        ) -> HRESULT;
        // 38
        pub fn DefineProperty(&self,
            td: mdTypeDef,
            szProperty: LPCWSTR,
            dwPropFlags: DWORD,
            pvSig: PCCOR_SIGNATURE,
            cbSig: ULONG,
            dwCPlusTypeFlag: DWORD,
            pValue: *const c_void,
            cchValue: ULONG,
            mdSetter: mdMethodDef,
            mdGetter: mdMethodDef,
            rmdOtherMethods: *const mdMethodDef,
            pmdProp: *mut mdToken,
        ) -> HRESULT;
        // 39
        pub fn DefineParam(&self,
            md: mdMethodDef,
            ulParamSeq: ULONG,
            szName: LPCWSTR,
            dwParamFlags: DWORD,
            dwCPlusTypeFlag: DWORD,
            pValue: *const c_void,
            cchValue: ULONG,
            ppd: *mut mdToken,
        ) -> HRESULT;
        // 40
        pub fn SetFieldProps(&self,
            fd: mdFieldDef,
            dwFieldFlags: DWORD,
            dwCPlusTypeFlag: DWORD,
            pValue: *const c_void,
            cchValue: ULONG,
        ) -> HRESULT;
        // 41
        pub fn SetPropertyProps(&self,
            pr: mdToken,
            dwPropFlags: DWORD,
            dwCPlusTypeFlag: DWORD,
            pValue: *const c_void,
            cchValue: ULONG,
            mdSetter: mdMethodDef,
            mdGetter: mdMethodDef,
            rmdOtherMethods: *const mdMethodDef,
        ) -> HRESULT;
        // 42
        pub fn SetParamProps(&self,
            pd: mdToken,
            szName: LPCWSTR,
            dwParamFlags: DWORD,
            dwCPlusTypeFlag: DWORD,
            pValue: *mut c_void,
            cchValue: ULONG,
        ) -> HRESULT;
        // 43
        pub fn DefineSecurityAttributeSet(&self,
            tkObj: mdToken,
            rSecAttrs: *const c_void,
            cSecAttrs: ULONG,
            pulErrorAttr: *mut ULONG,
        ) -> HRESULT;
        // 44
        pub fn ApplyEditAndContinue(&self, pImport: *const IUnknown) -> HRESULT;
        // 45
        pub fn TranslateSigWithScope(&self,
            pAssemImport: *const c_void,
            pbHashValue: *const c_void,
            cbHashValue: ULONG,
            import: *const c_void,
            pbSigBlob: PCCOR_SIGNATURE,
            cbSigBlob: ULONG,
            pAssemEmit: *const c_void,
            emit: *const c_void,
            pvTranslatedSig: *mut u8,
            cbTranslatedSigMax: ULONG,
            pcbTranslatedSig: *mut ULONG,
        ) -> HRESULT;
        // 46
        pub fn SetMethodImplFlags(&self, md: mdMethodDef, dwImplFlags: DWORD) -> HRESULT;
        // 47
        pub fn SetFieldRVA(&self, fd: mdFieldDef, ulRVA: ULONG) -> HRESULT;
        // 48
        pub fn Merge(&self,
            pImport: *const c_void,
            pHostMapToken: *const c_void,
            pHandler: *const IUnknown,
        ) -> HRESULT;
        // 49
        pub fn MergeEnd(&self) -> HRESULT;
    }

    /// IMetaDataEmit2 — extends IMetaDataEmit with generic method support.
    /// GUID: F5DD9950-F693-42e6-830E-7B833E8146A9
    ///
    /// 8 additional methods. DefineMethodSpec is the key one for the profiler.
    #[uuid("F5DD9950-F693-42e6-830E-7B833E8146A9")]
    pub unsafe interface IMetaDataEmit2: IMetaDataEmit {
        // 1 ** Used: create MethodSpec for generic method instantiation **
        pub fn DefineMethodSpec(&self,
            tkParent: mdToken,
            pvSigBlob: PCCOR_SIGNATURE,
            cbSigBlob: ULONG,
            pmi: *mut mdToken,
        ) -> HRESULT;
        // 2
        pub fn GetDeltaSaveSize(&self, fSave: DWORD, pdwSaveSize: *mut DWORD) -> HRESULT;
        // 3
        pub fn SaveDelta(&self, szFile: LPCWSTR, dwSaveFlags: DWORD) -> HRESULT;
        // 4
        pub fn SaveDeltaToStream(&self, pIStream: *const c_void, dwSaveFlags: DWORD) -> HRESULT;
        // 5
        pub fn SaveDeltaToMemory(&self, pbData: *mut c_void, cbData: ULONG) -> HRESULT;
        // 6
        pub fn DefineGenericParam(&self,
            tk: mdToken,
            ulParamSeq: ULONG,
            dwParamFlags: DWORD,
            szname: LPCWSTR,
            reserved: DWORD,
            rtkConstraints: *const mdToken,
            pgp: *mut mdToken,
        ) -> HRESULT;
        // 7
        pub fn SetGenericParamProps(&self,
            gp: mdToken,
            dwParamFlags: DWORD,
            szName: LPCWSTR,
            reserved: DWORD,
            rtkConstraints: *const mdToken,
        ) -> HRESULT;
        // 8
        pub fn ResetENCLog(&self) -> HRESULT;
    }
}
